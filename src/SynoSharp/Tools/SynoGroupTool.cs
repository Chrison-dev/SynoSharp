using SynoSharp.Provisioning;
using SynoSharp.Ssh;

namespace SynoSharp.Tools;

/// <summary>Typed wrapper over the on-box <c>synogroup</c> CLI (DSM 7.1).</summary>
public sealed class SynoGroupTool
{
    private readonly ISshRunner _runner;

    public SynoGroupTool(ISshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    public async Task<IReadOnlyList<string>> EnumAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(SynologyCommand.Create("synogroup", "--enum", "local"), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new SynologyToolException("synogroup --enum local", result);
        }
        return EnumOutput.ParseNames(result.StandardOutput);
    }

    /// <summary><c>synogroup --add groupname [members…]</c> — created empty here.</summary>
    public static SynologyCommand AddCommand(GroupSpec spec)
        => SynologyCommand.Create("synogroup", "--add", spec.Name);

    /// <summary><c>synogroup --del groupname</c>.</summary>
    public static SynologyCommand DeleteCommand(string name)
        => SynologyCommand.Create("synogroup", "--del", name);
}
