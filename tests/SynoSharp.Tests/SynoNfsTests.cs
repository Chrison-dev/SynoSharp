using SynoSharp.Provisioning;
using SynoSharp.Ssh;
using SynoSharp.Tools;
using Xunit;

namespace SynoSharp.Tests;

public class SynoNfsTests
{
    [Fact]
    public void SaveCommand_emits_the_expected_synowebapi_argv()
    {
        var spec = new NfsExportSpec
        {
            Share = "data",
            Rules = [new NfsRuleSpec { Client = "10.10.0.0/16" }], // defaults: rw / all_admin / sys / async+insecure+crossmnt
        };

        var rendered = SynoNfsTool.SaveCommand(spec).Render();

        Assert.Contains("api=SYNO.Core.FileServ.NFS.SharePrivilege", rendered);
        Assert.Contains("method=save", rendered);
        Assert.Contains("share_name=data", rendered);
        // The on-wire rule object — exact DSM keys, security_flavor as an OBJECT (not array).
        Assert.Contains("\"client\":\"10.10.0.0/16\"", rendered);
        Assert.Contains("\"privilege\":\"rw\"", rendered);
        Assert.Contains("\"root_squash\":\"all_admin\"", rendered);
        Assert.Contains("\"async\":true", rendered);
        Assert.Contains("\"security_flavor\":{\"sys\":true,\"kerberos\":false", rendered);
    }

    [Fact]
    public void SaveCommand_maps_security_flavor_to_the_right_flag()
    {
        var rendered = SynoNfsTool.SaveCommand(new NfsExportSpec
        {
            Share = "s",
            Rules = [new NfsRuleSpec { Client = "1.2.3.4", Security = "krb5i" }],
        }).Render();

        Assert.Contains("\"kerberos_integrity\":true", rendered);
        Assert.Contains("\"sys\":false", rendered);
    }

    [Fact]
    public void ParseRules_reads_the_canonical_load_output_ignoring_trailing_noise()
    {
        // Real synowebapi stdout: pretty JSON then diagnostic "[Line …]" noise.
        const string stdout = """
        {
           "data" : { "rule" : [ {
              "async" : true, "client" : "10.10.0.0/16", "crossmnt" : true,
              "insecure" : true, "privilege" : "rw", "root_squash" : "all_admin",
              "security_flavor" : { "kerberos" : false, "kerberos_integrity" : false, "kerberos_privacy" : false, "sys" : true }
           } ] },
           "success" : true
        }
        [Line 265] Not a json value: data
        [Line 295] Exec WebAPI: api=SYNO.Core.FileServ.NFS.SharePrivilege
        """;

        var r = Assert.Single(SynoNfsTool.ParseRules(stdout));
        Assert.Equal("10.10.0.0/16", r.Client);
        Assert.Equal("rw", r.Privilege);
        Assert.Equal("all_admin", r.Squash);
        Assert.True(r.Async);
        Assert.True(r.Insecure);
        Assert.True(r.Crossmnt);
        Assert.Equal("sys", r.Security);
    }

    [Fact]
    public void ParseRules_returns_empty_for_a_share_with_no_rules()
        => Assert.Empty(SynoNfsTool.ParseRules("""{"data":{"rule":[]},"success":true}"""));

    [Fact]
    public async Task Reconciler_creates_export_when_the_share_has_no_rules()
    {
        var runner = new NfsFakeRunner("""{"data":{"rule":[]},"success":true}""");
        var plan = await new SynologyReconciler(runner).PlanAsync(new SynologyDesiredState
        {
            NfsExports = [new NfsExportSpec { Share = "data", Rules = [new NfsRuleSpec { Client = "10.10.0.0/16" }] }],
        });

        var action = Assert.Single(plan.Actions);
        Assert.Equal(ActionKind.Create, action.Kind);
        Assert.Equal("nfs-export", action.ResourceType);
        Assert.Contains("method=save", action.Command!.Render());
    }

    [Fact]
    public async Task Reconciler_skips_when_the_rule_set_already_matches()
    {
        const string canned = """{"data":{"rule":[{"client":"10.10.0.0/16","privilege":"rw","root_squash":"all_admin","async":true,"insecure":true,"crossmnt":true,"security_flavor":{"sys":true,"kerberos":false,"kerberos_integrity":false,"kerberos_privacy":false}}]},"success":true}""";
        var plan = await new SynologyReconciler(new NfsFakeRunner(canned)).PlanAsync(new SynologyDesiredState
        {
            NfsExports = [new NfsExportSpec { Share = "data", Rules = [new NfsRuleSpec { Client = "10.10.0.0/16" }] }],
        });

        Assert.Equal(ActionKind.Skip, Assert.Single(plan.Actions).Kind);
    }

    /// <summary>Minimal scripted runner: returns canned JSON for <c>load</c>, success for anything else.</summary>
    private sealed class NfsFakeRunner(string loadJson) : ISshRunner
    {
        public Task<SshCommandResult> RunAsync(SynologyCommand command, CancellationToken cancellationToken = default)
        {
            var args = string.Join(' ', command.Args);
            var stdout = args.Contains("method=load") ? loadJson : "";
            return Task.FromResult(new SshCommandResult(0, stdout, ""));
        }
    }
}
