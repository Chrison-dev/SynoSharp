namespace SynoSharp.Ssh;

/// <summary>
/// Connection options for the DSM SSH-runner. Distinct from <see cref="SynologyOptions"/>
/// (the HTTP Web-API read client): SSH is a separate transport/port and may use a
/// key. The login account also supplies the <c>sudo</c> password (same account),
/// since <c>syno*</c> commands need root.
/// </summary>
public sealed record SynologySshOptions
{
    public required string Host { get; init; }

    public int Port { get; init; } = 22;

    public required string Username { get; init; }

    /// <summary>Login + sudo password. Optional if <see cref="PrivateKeyPath"/> is set — but sudo still needs it.</summary>
    public string? Password { get; init; }

    public string? PrivateKeyPath { get; init; }

    /// <summary>
    /// Accept any SSH host key — the homelab pragmatic default for a self-managed
    /// box (mirrors <c>VerifyTls=false</c> on the Web-API client).
    /// </summary>
    public bool AcceptAnyHostKey { get; init; } = true;

    /// <summary>
    /// Build from env: <c>SYNOLOGY_SSH_HOST</c> (falls back to the host of
    /// <c>SYNOLOGY_BASE_URL</c>), <c>SYNOLOGY_SSH_PORT</c> (default 22),
    /// <c>SYNOLOGY_USER</c>, <c>SYNOLOGY_PASSWORD</c>, <c>SYNOLOGY_SSH_KEY</c>.
    /// Null if host or user can't be resolved.
    /// </summary>
    public static SynologySshOptions? TryFromEnvironment()
    {
        var host = Environment.GetEnvironmentVariable("SYNOLOGY_SSH_HOST");
        if (string.IsNullOrEmpty(host))
        {
            var baseUrl = Environment.GetEnvironmentVariable("SYNOLOGY_BASE_URL");
            if (!string.IsNullOrEmpty(baseUrl) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            {
                host = uri.Host;
            }
        }

        var user = Environment.GetEnvironmentVariable("SYNOLOGY_USER");
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user))
        {
            return null;
        }

        var port = int.TryParse(Environment.GetEnvironmentVariable("SYNOLOGY_SSH_PORT"), out var p) ? p : 22;

        return new SynologySshOptions
        {
            Host = host,
            Port = port,
            Username = user,
            Password = Environment.GetEnvironmentVariable("SYNOLOGY_PASSWORD"),
            PrivateKeyPath = Environment.GetEnvironmentVariable("SYNOLOGY_SSH_KEY"),
        };
    }

    /// <summary>
    /// Redacted representation. The synthesized record <c>ToString()</c> would
    /// otherwise print <see cref="Password"/> (the login + sudo password),
    /// leaking it into any log or interpolated string. The password is never emitted.
    /// </summary>
    public override string ToString() =>
        $"SynologySshOptions {{ Host = {Host}, Port = {Port}, Username = {Username}, " +
        $"Password = {(Password is null ? "null" : "***")}, PrivateKeyPath = {PrivateKeyPath}, " +
        $"AcceptAnyHostKey = {AcceptAnyHostKey} }}";
}
