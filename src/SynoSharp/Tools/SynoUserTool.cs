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

    /// <summary>Read current state via <c>synouser --get</c>; null if the user doesn't exist.</summary>
    public async Task<UserInfo?> GetAsync(string name, CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(SynologyCommand.Create("synouser", "--get", name), cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return null;
        }
        var fields = GetFields.Parse(result.StandardOutput);
        return new UserInfo
        {
            Name = fields.GetValueOrDefault("User Name") ?? name,
            FullName = fields.GetValueOrDefault("Fullname") ?? "",
            Email = fields.GetValueOrDefault("User Mail") ?? "",
            Expired = string.Equals(fields.GetValueOrDefault("Expired"), "true", StringComparison.OrdinalIgnoreCase),
        };
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

    /// <summary>
    /// <c>synouser --modify username "full name" expired{0|1} mail</c>. Empty/null
    /// spec fields fall back to <paramref name="current"/> so unmanaged values aren't
    /// clobbered; <c>expired</c> is always preserved (not modelled in the spec).
    /// </summary>
    public static SynologyCommand ModifyCommand(UserSpec spec, UserInfo current)
    {
        var fullName = string.IsNullOrEmpty(spec.FullName) ? current.FullName : spec.FullName;
        var email = spec.Email ?? current.Email;
        return SynologyCommand.Create("synouser", "--modify", spec.Name, fullName, current.Expired ? "1" : "0", email);
    }

    /// <summary><c>synouser --del username</c>.</summary>
    public static SynologyCommand DeleteCommand(string name)
        => SynologyCommand.Create("synouser", "--del", name);
}
