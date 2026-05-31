namespace SynoSharp.Tools;

/// <summary>Current on-box state of a share (from <c>synoshare --get</c>) — for drift.</summary>
public sealed record ShareInfo
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string Path { get; init; } = "";
}

/// <summary>Current on-box state of a user (from <c>synouser --get</c>) — for drift.</summary>
public sealed record UserInfo
{
    public required string Name { get; init; }
    public string FullName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool Expired { get; init; }
}
