using SynoSharp.Ssh;
using SynoSharp.Tools;

namespace SynoSharp.Provisioning;

/// <summary>
/// The IaC heart (ADR-0002): diffs desired state against what's live on the box and
/// emits only the needed actions — so the non-idempotent <c>syno*</c> CLIs become
/// safe (re-running <c>--add</c> on an existing resource would otherwise error).
/// <para>
/// <b>Dry-run by default</b> (like <c>Deploy-Shape.ps1</c>): <see cref="ApplyAsync"/>
/// only mutates when <c>apply: true</c>. Unmanaged resources are never pruned — a
/// resource is deleted only when a spec explicitly sets <c>Present = false</c>.
/// </para>
/// <para>
/// Phase A scope: existence reconciliation for shares/users/groups (create-if-missing,
/// delete-if-marked-absent, skip-if-present). Field-level drift (desc/ACLs) is a
/// follow-up — see the issue #57 plan.
/// </para>
/// </summary>
public sealed class SynologyReconciler
{
    private readonly ISshRunner _runner;
    private readonly SynoShareTool _shares;
    private readonly SynoUserTool _users;
    private readonly SynoGroupTool _groups;
    private readonly SynoNfsTool _nfs;

    public SynologyReconciler(ISshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _shares = new SynoShareTool(runner);
        _users = new SynoUserTool(runner);
        _groups = new SynoGroupTool(runner);
        _nfs = new SynoNfsTool(runner);
    }

    /// <summary>Read live state, diff against <paramref name="desired"/>, and return the plan.</summary>
    public async Task<SynologyPlan> PlanAsync(SynologyDesiredState desired, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(desired);

        // Read-before-write. Groups → users → shares (a dependency-friendly order).
        var existingGroups = await _groups.EnumAsync(cancellationToken).ConfigureAwait(false);
        var existingUsers = await _users.EnumAsync(cancellationToken).ConfigureAwait(false);
        var existingShares = await _shares.EnumAsync(cancellationToken).ConfigureAwait(false);

        var actions = new List<PlannedAction>();
        foreach (var g in desired.Groups)
        {
            actions.Add(await PlanGroupAsync(g, Contains(existingGroups, g.Name), cancellationToken).ConfigureAwait(false));
        }
        foreach (var u in desired.Users)
        {
            actions.Add(await PlanUserAsync(u, Contains(existingUsers, u.Name), cancellationToken).ConfigureAwait(false));
        }
        foreach (var s in desired.Shares)
        {
            actions.Add(await PlanShareAsync(s, Contains(existingShares, s.Name), cancellationToken).ConfigureAwait(false));
        }
        // NFS exports last — they depend on the share existing (read-before-write per share).
        foreach (var x in desired.NfsExports)
        {
            actions.Add(await PlanNfsAsync(x, cancellationToken).ConfigureAwait(false));
        }

        return new SynologyPlan { Actions = actions };
    }

    /// <summary>
    /// Execute the plan. With <paramref name="apply"/> false (default) this is a
    /// dry-run: nothing runs, each mutation is reported as "would run".
    /// </summary>
    public async Task<SynologyApplyResult> ApplyAsync(SynologyPlan plan, bool apply = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var outcomes = new List<ActionOutcome>();
        foreach (var action in plan.Actions)
        {
            if (action.Command is null)
            {
                outcomes.Add(new ActionOutcome(action, Applied: false, $"skipped: {action.Reason}"));
                continue;
            }
            if (!apply)
            {
                outcomes.Add(new ActionOutcome(action, Applied: false, $"dry-run: would run `{action.Command.Render()}`"));
                continue;
            }

            var result = await _runner.RunAsync(action.Command, cancellationToken).ConfigureAwait(false);
            outcomes.Add(result.Success
                ? new ActionOutcome(action, Applied: true, "applied")
                : new ActionOutcome(action, Applied: false, $"FAILED (exit {result.ExitCode}): {result.StandardError.Trim()}"));
        }

        return new SynologyApplyResult { Outcomes = outcomes };
    }

    private static bool Contains(IReadOnlyList<string> names, string name)
        => names.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool Differs(string current, string desired)
        => !string.Equals(current, desired, StringComparison.Ordinal);

    private async Task<PlannedAction> PlanGroupAsync(GroupSpec g, bool exists, CancellationToken ct)
    {
        if (g.Present && !exists)
        {
            return PlannedAction.Create("group", g.Name, SynoGroupTool.AddCommand(g), "absent → create");
        }
        if (!g.Present && exists)
        {
            return PlannedAction.Delete("group", g.Name, SynoGroupTool.DeleteCommand(g.Name), "present → delete");
        }
        if (!g.Present)
        {
            return PlannedAction.Skip("group", g.Name, "already absent");
        }

        // Present + exists → field drift. Empty Description = unmanaged (don't clobber).
        if (!string.IsNullOrEmpty(g.Description))
        {
            var current = await _groups.GetDescriptionAsync(g.Name, ct).ConfigureAwait(false);
            if (current is not null && Differs(current, g.Description))
            {
                return PlannedAction.Modify("group", g.Name, SynoGroupTool.SetDescriptionCommand(g.Name, g.Description),
                    $"desc '{current}' → '{g.Description}'");
            }
        }
        return PlannedAction.Skip("group", g.Name, "in sync");
    }

    private async Task<PlannedAction> PlanUserAsync(UserSpec u, bool exists, CancellationToken ct)
    {
        if (u.Present && !exists)
        {
            if (string.IsNullOrEmpty(u.Password))
            {
                return PlannedAction.Skip("user", u.Name, "BLOCKED: create needs a Password");
            }
            return PlannedAction.Create("user", u.Name, SynoUserTool.AddCommand(u), "absent → create");
        }
        if (!u.Present && exists)
        {
            return PlannedAction.Delete("user", u.Name, SynoUserTool.DeleteCommand(u.Name), "present → delete");
        }
        if (!u.Present)
        {
            return PlannedAction.Skip("user", u.Name, "already absent");
        }

        // Present + exists → field drift on FullName / Email (empty/null = unmanaged).
        var current = await _users.GetAsync(u.Name, ct).ConfigureAwait(false);
        if (current is not null)
        {
            var fullNameDrift = !string.IsNullOrEmpty(u.FullName) && Differs(current.FullName, u.FullName);
            var emailDrift = u.Email is not null && Differs(current.Email, u.Email);
            if (fullNameDrift || emailDrift)
            {
                var reasons = new List<string>();
                if (fullNameDrift) reasons.Add($"name '{current.FullName}' → '{u.FullName}'");
                if (emailDrift) reasons.Add($"mail '{current.Email}' → '{u.Email}'");
                return PlannedAction.Modify("user", u.Name, SynoUserTool.ModifyCommand(u, current), string.Join(", ", reasons));
            }
        }
        return PlannedAction.Skip("user", u.Name, "in sync");
    }

    private async Task<PlannedAction> PlanShareAsync(ShareSpec s, bool exists, CancellationToken ct)
    {
        if (s.Present && !exists)
        {
            return PlannedAction.Create("share", s.Name, SynoShareTool.AddCommand(s), "absent → create");
        }
        if (!s.Present && exists)
        {
            var reason = s.DeleteData ? "present → delete (incl. data)" : "present → delete (keep data)";
            return PlannedAction.Delete("share", s.Name, SynoShareTool.DeleteCommand(s.Name, s.DeleteData), reason);
        }
        if (!s.Present)
        {
            return PlannedAction.Skip("share", s.Name, "already absent");
        }

        // Present + exists → field drift on Description (empty = unmanaged).
        if (!string.IsNullOrEmpty(s.Description))
        {
            var current = await _shares.GetAsync(s.Name, ct).ConfigureAwait(false);
            if (current is not null && Differs(current.Description, s.Description))
            {
                return PlannedAction.Modify("share", s.Name, SynoShareTool.SetDescriptionCommand(s.Name, s.Description),
                    $"desc '{current.Description}' → '{s.Description}'");
            }
        }
        return PlannedAction.Skip("share", s.Name, "in sync");
    }

    private async Task<PlannedAction> PlanNfsAsync(NfsExportSpec x, CancellationToken ct)
    {
        IReadOnlyList<NfsRuleSpec> current;
        try
        {
            current = await _nfs.LoadAsync(x.Share, ct).ConfigureAwait(false);
        }
        catch (SynologyToolException)
        {
            // Don't abort the whole plan — surface it as a blocked skip (share missing / NFS off).
            return PlannedAction.Skip("nfs-export", x.Share, "BLOCKED: cannot read NFS rules (share exists + NFS enabled?)");
        }

        var desired = x.Present ? x.Rules : [];
        if (RulesEqual(current, desired))
        {
            return PlannedAction.Skip("nfs-export", x.Share, x.Present ? "in sync" : "already empty");
        }

        // DSM `save` is a whole-list replace; Present=false → replace with an empty set (clear).
        var cmd = SynoNfsTool.SaveCommand(x.Present ? x : x with { Rules = [] });
        if (current.Count == 0)
        {
            return PlannedAction.Create("nfs-export", x.Share, cmd, $"absent → set {desired.Count} rule(s)");
        }
        if (desired.Count == 0)
        {
            return PlannedAction.Delete("nfs-export", x.Share, cmd, $"{current.Count} rule(s) → clear");
        }
        return PlannedAction.Modify("nfs-export", x.Share, cmd, $"{current.Count} → {desired.Count} rule(s)");
    }

    // Order-insensitive value comparison (NfsRuleSpec is a record → structural equality).
    private static bool RulesEqual(IReadOnlyList<NfsRuleSpec> a, IReadOnlyList<NfsRuleSpec> b)
        => a.Count == b.Count
        && a.OrderBy(r => r.Client, StringComparer.Ordinal)
            .SequenceEqual(b.OrderBy(r => r.Client, StringComparer.Ordinal));
}
