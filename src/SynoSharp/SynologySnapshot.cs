namespace SynoSharp;

/// <summary>A read-only snapshot of DSM state (discover output).</summary>
public sealed record SynologySnapshot
{
    public string? Model { get; init; }
    public string? Serial { get; init; }
    public string? DsmVersion { get; init; }
    public IReadOnlyList<string> Shares { get; init; } = [];
    public IReadOnlyList<string> Users { get; init; } = [];
}
