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

    public SynologyReconciler(ISshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
        _shares = new SynoShareTool(runner);
        _users = new SynoUserTool(runner);
        _groups = new SynoGroupTool(runner);
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
            actions.Add(PlanGroup(g, Contains(existingGroups, g.Name)));
        }
        foreach (var u in desired.Users)
        {
            actions.Add(PlanUser(u, Contains(existingUsers, u.Name)));
        }
        foreach (var s in desired.Shares)
        {
            actions.Add(PlanShare(s, Contains(existingShares, s.Name)));
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

    private static PlannedAction PlanGroup(GroupSpec g, bool exists)
    {
        if (g.Present && !exists)
        {
            return PlannedAction.Create("group", g.Name, SynoGroupTool.AddCommand(g), "absent → create");
        }
        if (!g.Present && exists)
        {
            return PlannedAction.Delete("group", g.Name, SynoGroupTool.DeleteCommand(g.Name), "present → delete");
        }
        return PlannedAction.Skip("group", g.Name, g.Present ? "already present" : "already absent");
    }

    private static PlannedAction PlanUser(UserSpec u, bool exists)
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
        return PlannedAction.Skip("user", u.Name, u.Present ? "already present" : "already absent");
    }

    private static PlannedAction PlanShare(ShareSpec s, bool exists)
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
        return PlannedAction.Skip("share", s.Name, s.Present ? "already present" : "already absent");
    }
}
