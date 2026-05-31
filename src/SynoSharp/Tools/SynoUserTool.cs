using SynoSharp.Provisioning;
using SynoSharp.Ssh;

namespace SynoSharp.Tools;

/// <summary>Typed wrapper over the on-box <c>synouser</c> CLI (DSM 7.1).</summary>
public sealed class SynoUserTool
{
    private readonly ISshRunner _runner;

    public SynoUserTool(ISshRunner runner)
    {
        ArgumentNullException.ThrowIfNull(runner);
        _runner = runner;
    }

    public async Task<IReadOnlyList<string>> EnumAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(SynologyCommand.Create("synouser", "--enum", "local"), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new SynologyToolException("synouser --enum local", result);
        }
        return EnumOutput.ParseNames(result.StandardOutput);
    }

    /// <summary>
    /// <c>synouser --add username pwd "full name" expired{0|1} mail privilege</c>.
    /// A password is required to create a user; <paramref name="spec"/>.Password must be set.
    /// </summary>
    public static SynologyCommand AddCommand(UserSpec spec)
    {
        if (string.IsNullOrEmpty(spec.Password))
        {
            throw new InvalidOperationException($"Cannot create user '{spec.Name}' without a Password.");
        }
        return SynologyCommand.Create("synouser", "--add", spec.Name, spec.Password, spec.FullName, "0", spec.Email ?? "", "");
    }

    /// <summary><c>synouser --del username</c>.</summary>
    public static SynologyCommand DeleteCommand(string name)
        => SynologyCommand.Create("synouser", "--del", name);
}
