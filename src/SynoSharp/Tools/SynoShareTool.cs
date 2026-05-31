using SynoSharp.Provisioning;
using SynoSharp.Ssh;

namespace SynoSharp.Tools;

/// <summary>
/// Typed wrapper over the on-box <c>synoshare</c> CLI — encodes its positional argv
/// once (DSM 7.1) so callers never hand-write it. Read (<c>--enum</c>) executes;
/// mutations are returned as <see cref="SynologyCommand"/>s for the reconciler to
/// plan/apply, so nothing here mutates on its own.
/// </summary>
public sealed class SynoShareTool
{
    private readonly ISshRunner _runner;

    public SynoShareTool(ISshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    public async Task<IReadOnlyList<string>> EnumAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(SynologyCommand.Create("synoshare", "--enum", "ALL"), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new SynologyToolException("synoshare --enum ALL", result);
        }
        return EnumOutput.ParseNames(result.StandardOutput);
    }

    /// <summary>Read current state via <c>synoshare --get</c>; null if the share doesn't exist.</summary>
    public async Task<ShareInfo?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(SynologyCommand.Create("synoshare", "--get", name), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }
        var fields = GetFields.Parse(result.StandardOutput);
        return new ShareInfo
        {
            Name = fields.GetValueOrDefault("Name") ?? name,
            Description = fields.GetValueOrDefault("Comment") ?? "",
            Path = fields.GetValueOrDefault("Path") ?? "",
        };
    }

    /// <summary>
    /// <c>synoshare --add name desc path na rw ro browsable{0|1} adv_privilege{0~7}</c>.
    /// na/rw/ro are comma-separated user lists (empty = none); created browsable, basic privilege.
    /// </summary>
    public static SynologyCommand AddCommand(ShareSpec spec)
        => SynologyCommand.Create("synoshare", "--add", spec.Name, spec.Description, spec.Path, "", "", "", "1", "0");

    /// <summary><c>synoshare --setdesc name desc</c>.</summary>
    public static SynologyCommand SetDescriptionCommand(string name, string description)
        => SynologyCommand.Create("synoshare", "--setdesc", name, description);

    /// <summary><c>synoshare --del {TRUE|FALSE} name</c> — FALSE keeps the underlying data dir.</summary>
    public static SynologyCommand DeleteCommand(string name, bool deleteData = false)
        => SynologyCommand.Create("synoshare", "--del", deleteData ? "TRUE" : "FALSE", name);
}
