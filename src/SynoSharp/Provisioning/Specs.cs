namespace SynoSharp.Provisioning;

/// <summary>Desired state for a shared folder. <see cref="Present"/> false = ensure absent.</summary>
public sealed record ShareSpec
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";

    /// <summary>Full on-box path, e.g. <c>/volume1/MyShare</c>.</summary>
    public required string Path { get; init; }

    public bool Present { get; init; } = true;

    /// <summary>
    /// When deleting (<see cref="Present"/> false), also remove the share's data.
    /// DSM shares are btrfs subvolumes, so the default keep-data delete leaves the
    /// subvolume behind (plain <c>rm</c> can't remove it) — set true for a full
    /// <c>synoshare --del TRUE</c>. <b>Destructive; off by default.</b>
    /// </summary>
    public bool DeleteData { get; init; }
}

/// <summary>Desired state for a local user. <see cref="Present"/> false = ensure absent.</summary>
public sealed record UserSpec
{
    public required string Name { get; init; }
    public string FullName { get; init; } = "";
    public string? Email { get; init; }

    /// <summary>Used only when creating the user (never read back); required for a create.</summary>
    public string? Password { get; init; }

    public bool Present { get; init; } = true;
}

/// <summary>Desired state for a local group. <see cref="Present"/> false = ensure absent.</summary>
public sealed record GroupSpec
{
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public bool Present { get; init; } = true;
}

/// <summary>A bundle of desired DSM state — the input to the reconciler.</summary>
public sealed record SynologyDesiredState
{
    public IReadOnlyList<GroupSpec> Groups { get; init; } = [];
    public IReadOnlyList<UserSpec> Users { get; init; } = [];
    public IReadOnlyList<ShareSpec> Shares { get; init; } = [];
}
