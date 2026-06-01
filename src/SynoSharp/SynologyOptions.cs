namespace SynoSharp;

/// <summary>
/// Connection options for a Synology DSM Web API. Authenticates with a DSM
/// account (ideally a dedicated read-only one) via <c>SYNO.API.Auth</c>.
/// </summary>
public sealed record SynologyOptions
{
    /// <summary>Base URL of DSM, e.g. <c>https://nas:5001</c> (or <c>http://nas:5000</c>).</summary>
    public required Uri BaseUrl { get; init; }

    public required string Username { get; init; }
    public required string Password { get; init; }

    /// <summary>Verify TLS. DSM commonly uses a self-signed cert — set false on the LAN.</summary>
    public bool VerifyTls { get; init; } = true;

    /// <summary>
    /// Build options from <c>SYNOLOGY_BASE_URL</c> / <c>SYNOLOGY_USER</c> /
    /// <c>SYNOLOGY_PASSWORD</c> / <c>SYNOLOGY_VERIFY_TLS</c>; null if any required value is missing.
    /// </summary>
    public static SynologyOptions? TryFromEnvironment()
    {
        var baseUrl = Environment.GetEnvironmentVariable("SYNOLOGY_BASE_URL");
        var user = Environment.GetEnvironmentVariable("SYNOLOGY_USER");
        var pass = Environment.GetEnvironmentVariable("SYNOLOGY_PASSWORD");
        if (string.IsNullOrEmpty(baseUrl) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            return null;
        }

        var verifyTls = !string.Equals(
            Environment.GetEnvironmentVariable("SYNOLOGY_VERIFY_TLS"), "false", StringComparison.OrdinalIgnoreCase);

        return new SynologyOptions { BaseUrl = new Uri(baseUrl), Username = user, Password = pass, VerifyTls = verifyTls };
    }

    /// <summary>
    /// Redacted representation. The synthesized record <c>ToString()</c> would
    /// otherwise print <see cref="Password"/>, leaking it into any log or
    /// interpolated string. The password is never emitted.
    /// </summary>
    public override string ToString() =>
        $"SynologyOptions {{ BaseUrl = {BaseUrl}, Username = {Username}, Password = ***, VerifyTls = {VerifyTls} }}";
}
