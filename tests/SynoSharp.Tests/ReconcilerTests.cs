using SynoSharp.Provisioning;
using SynoSharp.Ssh;
using Xunit;

namespace SynoSharp.Tests;

/// <summary>
/// A scripted <see cref="ISshRunner"/> — returns canned enum output for the read
/// commands and records every command it's asked to run, so the reconciler can be
/// tested without a NAS.
/// </summary>
internal sealed class FakeSshRunner : ISshRunner
{
    public List<SynologyCommand> Ran { get; } = [];

    public Task<SshCommandResult> RunAsync(SynologyCommand command, CancellationToken cancellationToken = default)
    {
        Ran.Add(command);
        var args = string.Join(' ', command.Args);
        var stdout = (command.Executable, args) switch
        {
            ("synogroup", "--enum local") => "3 Group Listed:\nadministrators\nhttp\nusers\n",
            ("synouser", "--enum local") => "2 User Listed:\nadmin\nhomelab\n",
            ("synoshare", "--enum ALL") => "Share Enum Arguments: [0xFF0F] ALL\n2 Listed:\nVolume-1\nweb\n",
            // --get / --descget for drift (canned "current" state)
            ("synoshare", "--get Volume-1") => "\t Name .......[Volume-1]\n\t Comment ....[old desc]\n\t Path .......[/volume1/Volume-1]\n",
            ("synouser", "--get homelab") => "User Name   : [homelab]\nFullname    : [Old Name]\nExpired     : [false]\nUser Mail   : [old@x.test]\n",
            ("synogroup", "--descget users") => "users:[old group desc]\n",
            _ => "",
        };
        return Task.FromResult(new SshCommandResult(0, stdout, ""));
    }
}

public class ReconcilerTests
{
    private static SynologyReconciler ReconcilerWith(out FakeSshRunner fake)
    {
        fake = new FakeSshRunner();
        return new SynologyReconciler(fake);
    }

    [Fact]
    public async Task Plan_creates_missing_and_skips_existing()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Shares =
            [
                new ShareSpec { Name = "Volume-1", Path = "/volume1/Volume-1" }, // exists → skip
                new ShareSpec { Name = "NewShare", Path = "/volume1/NewShare" },  // missing → create
            ],
        };

        var plan = await reconciler.PlanAsync(desired);

        var create = Assert.Single(plan.Mutations);
        Assert.Equal(ActionKind.Create, create.Kind);
        Assert.Equal("NewShare", create.Name);
        Assert.Equal("synoshare --add NewShare '' /volume1/NewShare '' '' '' 1 0", create.Command!.Render());
    }

    [Fact]
    public async Task Plan_deletes_when_present_false()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Groups = [new GroupSpec { Name = "http", Present = false }], // exists → delete
        };

        var plan = await reconciler.PlanAsync(desired);

        var delete = Assert.Single(plan.Mutations);
        Assert.Equal(ActionKind.Delete, delete.Kind);
        Assert.Equal("synogroup --del http", delete.Command!.Render());
    }

    [Fact]
    public async Task Plan_share_description_drift_emits_setdesc()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "Volume-1", Path = "/volume1/Volume-1", Description = "new desc" }],
        };

        var plan = await reconciler.PlanAsync(desired);

        var modify = Assert.Single(plan.Mutations);
        Assert.Equal(ActionKind.Modify, modify.Kind);
        Assert.Equal("synoshare --setdesc Volume-1 'new desc'", modify.Command!.Render());
    }

    [Fact]
    public async Task Plan_no_drift_when_description_matches()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "Volume-1", Path = "/volume1/Volume-1", Description = "old desc" }],
        };

        var plan = await reconciler.PlanAsync(desired);

        Assert.False(plan.HasChanges);
        Assert.Equal("in sync", Assert.Single(plan.Actions).Reason);
    }

    [Fact]
    public async Task Plan_empty_description_is_unmanaged()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "Volume-1", Path = "/volume1/Volume-1" }], // no Description
        };

        var plan = await reconciler.PlanAsync(desired);

        Assert.False(plan.HasChanges); // current "old desc" left untouched
    }

    [Fact]
    public async Task Plan_user_fullname_drift_emits_modify_preserving_email()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Users = [new UserSpec { Name = "homelab", FullName = "New Name" }], // email unset → preserve current
        };

        var plan = await reconciler.PlanAsync(desired);

        var modify = Assert.Single(plan.Mutations);
        Assert.Equal(ActionKind.Modify, modify.Kind);
        // expired preserved (0), email preserved (quoted — '@' isn't a bare-word char), fullname updated.
        Assert.Equal("synouser --modify homelab 'New Name' 0 'old@x.test'", modify.Command!.Render());
    }

    [Fact]
    public async Task Plan_group_description_drift_emits_descset()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Groups = [new GroupSpec { Name = "users", Description = "new group desc" }],
        };

        var plan = await reconciler.PlanAsync(desired);

        var modify = Assert.Single(plan.Mutations);
        Assert.Equal("synogroup --descset users 'new group desc'", modify.Command!.Render());
    }

    [Fact]
    public async Task Plan_share_delete_keeps_data_by_default()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "Volume-1", Path = "/volume1/Volume-1", Present = false }],
        };

        var plan = await reconciler.PlanAsync(desired);

        var delete = Assert.Single(plan.Mutations);
        Assert.Equal("synoshare --del FALSE Volume-1", delete.Command!.Render());
        Assert.Contains("keep data", delete.Reason);
    }

    [Fact]
    public async Task Plan_share_delete_with_DeleteData_removes_data()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "Volume-1", Path = "/volume1/Volume-1", Present = false, DeleteData = true }],
        };

        var plan = await reconciler.PlanAsync(desired);

        var delete = Assert.Single(plan.Mutations);
        Assert.Equal("synoshare --del TRUE Volume-1", delete.Command!.Render());
        Assert.Contains("incl. data", delete.Reason);
    }

    [Fact]
    public async Task Plan_blocks_user_create_without_password()
    {
        var reconciler = ReconcilerWith(out _);
        var desired = new SynologyDesiredState
        {
            Users = [new UserSpec { Name = "svc-new" }], // missing + no password
        };

        var plan = await reconciler.PlanAsync(desired);

        Assert.False(plan.HasChanges);
        var skip = Assert.Single(plan.Actions);
        Assert.Equal(ActionKind.Skip, skip.Kind);
        Assert.Contains("BLOCKED", skip.Reason);
    }

    [Fact]
    public async Task DryRun_apply_runs_no_mutating_commands()
    {
        var reconciler = ReconcilerWith(out var fake);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "NewShare", Path = "/volume1/NewShare" }],
        };

        var plan = await reconciler.PlanAsync(desired);
        var readsAfterPlan = fake.Ran.Count;

        var result = await reconciler.ApplyAsync(plan, apply: false);

        // Dry-run must not have run anything beyond the read/enum done during planning.
        Assert.Equal(readsAfterPlan, fake.Ran.Count);
        Assert.DoesNotContain(fake.Ran, c => c.Args.Contains("--add"));
        Assert.All(result.Outcomes.Where(o => o.Action.Kind != ActionKind.Skip),
            o => Assert.Contains("dry-run", o.Message));
    }

    [Fact]
    public async Task Apply_runs_mutating_commands_when_confirmed()
    {
        var reconciler = ReconcilerWith(out var fake);
        var desired = new SynologyDesiredState
        {
            Shares = [new ShareSpec { Name = "NewShare", Path = "/volume1/NewShare" }],
        };

        var plan = await reconciler.PlanAsync(desired);
        var result = await reconciler.ApplyAsync(plan, apply: true);

        Assert.True(result.AllSucceeded);
        Assert.Contains(fake.Ran, c => c.Executable == "synoshare" && c.Args.Contains("--add"));
    }
}
