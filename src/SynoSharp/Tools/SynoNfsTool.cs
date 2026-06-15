using System.Text.Json;
using SynoSharp.Provisioning;
using SynoSharp.Ssh;

namespace SynoSharp.Tools;

/// <summary>
/// Typed wrapper over DSM's NFS export API (<c>SYNO.Core.FileServ.NFS.SharePrivilege</c>,
/// driven through <c>synowebapi</c> over SSH — ADR-0002, plan #057 Phase C). <c>load</c>
/// reads a share's rules; mutations are returned as <see cref="SynologyCommand"/>s for the
/// reconciler to plan/apply, so nothing here mutates on its own.
/// <para>
/// The rule shape (reverse-engineered on a DSM 7.x VDSM, 2026-06-15): each rule is
/// <c>{client, privilege, root_squash, async, insecure, crossmnt, security_flavor:{sys,
/// kerberos, kerberos_integrity, kerberos_privacy}}</c>. <c>security_flavor</c> is an OBJECT
/// of bool flags (not an array). <c>save</c> is a whole-list REPLACE for the share.
/// </para>
/// </summary>
public sealed class SynoNfsTool
{
    private const string Api = "api=SYNO.Core.FileServ.NFS.SharePrivilege";
    private readonly ISshRunner _runner;

    public SynoNfsTool(ISshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    /// <summary>Read the share's current NFS rules via <c>load</c> (empty list if none).</summary>
    public async Task<IReadOnlyList<NfsRuleSpec>> LoadAsync(string share, CancellationToken cancellationToken = default)
    {
        var cmd = SynologyCommand.Create("synowebapi", "--exec", Api, "method=load", "version=1", $"share_name={share}");
        var result = await _runner.RunAsync(cmd, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new SynologyToolException($"synowebapi NFS load {share}", result);
        }
        return ParseRules(result.StandardOutput);
    }

    /// <summary>
    /// <c>synowebapi … method=save share_name=&lt;share&gt; rule=&lt;json&gt;</c> — replaces the
    /// share's entire rule set (empty list clears it).
    /// </summary>
    public static SynologyCommand SaveCommand(NfsExportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var json = JsonSerializer.Serialize(spec.Rules.Select(ToWire).ToArray());
        return SynologyCommand.Create("synowebapi", "--exec", Api, "method=save", "version=1",
            $"share_name={spec.Share}", $"rule={json}");
    }

    /// <summary>Enable the NFS service (v3 + v4) — a prerequisite for any export to apply.</summary>
    public static SynologyCommand EnableServiceCommand()
        => SynologyCommand.Create("synowebapi", "--exec", "api=SYNO.Core.FileServ.NFS",
            "method=set", "version=1", "enable_nfs=true", "enable_nfs_v4=true");

    // The on-wire rule object. Member names ARE the JSON keys (no naming policy), so
    // `root_squash`/`security_flavor`/`kerberos_integrity` must match DSM verbatim; `@async`
    // serialises as "async" (C# keyword).
    private static object ToWire(NfsRuleSpec r) => new
    {
        client = r.Client,
        privilege = r.Privilege,
        root_squash = r.Squash,
        @async = r.Async,
        insecure = r.Insecure,
        crossmnt = r.Crossmnt,
        security_flavor = new
        {
            sys = r.Security == "sys",
            kerberos = r.Security == "krb5",
            kerberos_integrity = r.Security == "krb5i",
            kerberos_privacy = r.Security == "krb5p",
        },
    };

    /// <summary>
    /// Parse <c>{data:{rule:[…]}}</c> out of synowebapi's stdout (which may trail diagnostic
    /// <c>[Line …]</c> noise) into <see cref="NfsRuleSpec"/>s.
    /// </summary>
    public static IReadOnlyList<NfsRuleSpec> ParseRules(string stdout)
    {
        var json = ExtractFirstJsonObject(stdout);
        if (json is null)
        {
            return [];
        }
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("rule", out var rules) ||
            rules.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var list = new List<NfsRuleSpec>();
        foreach (var e in rules.EnumerateArray())
        {
            list.Add(new NfsRuleSpec
            {
                Client = Str(e, "client"),
                Privilege = Str(e, "privilege", "rw"),
                Squash = Str(e, "root_squash", "all_admin"),
                Async = Bool(e, "async"),
                Insecure = Bool(e, "insecure"),
                Crossmnt = Bool(e, "crossmnt"),
                Security = SecurityFromFlavor(e),
            });
        }
        return list;
    }

    private static string SecurityFromFlavor(JsonElement rule)
    {
        if (!rule.TryGetProperty("security_flavor", out var sf) || sf.ValueKind != JsonValueKind.Object)
        {
            return "sys";
        }
        if (Bool(sf, "kerberos_privacy")) return "krb5p";
        if (Bool(sf, "kerberos_integrity")) return "krb5i";
        if (Bool(sf, "kerberos")) return "krb5";
        return "sys";
    }

    private static string Str(JsonElement e, string name, string fallback = "")
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? fallback) : fallback;

    private static bool Bool(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && (v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.False ? false : v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b));

    /// <summary>Return the first balanced top-level <c>{…}</c> object in <paramref name="s"/>, or null.</summary>
    private static string? ExtractFirstJsonObject(string s)
    {
        var start = s.IndexOf('{');
        if (start < 0) return null;
        int depth = 0;
        bool inStr = false, esc = false;
        for (int i = start; i < s.Length; i++)
        {
            var c = s[i];
            if (inStr)
            {
                if (esc) esc = false;
                else if (c == '\\') esc = true;
                else if (c == '"') inStr = false;
                continue;
            }
            if (c == '"') inStr = true;
            else if (c == '{') depth++;
            else if (c == '}' && --depth == 0) return s.Substring(start, i - start + 1);
        }
        return null;
    }
}
