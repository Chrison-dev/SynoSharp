namespace SynoSharp;

/// <summary>A read-only snapshot of DSM state (discover output).</summary>
public sealed record SynologySnapshot
{
    public string? DsmVersion { get; init; }
    public string? Hostname { get; init; }
    public IReadOnlyList<string> Shares { get; init; } = [];
    public IReadOnlyList<string> Users { get; init; } = [];
}
