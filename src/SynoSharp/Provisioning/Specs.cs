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

/// <summary>
/// One NFS export rule for a share — the load/save shape of
/// <c>SYNO.Core.FileServ.NFS.SharePrivilege</c> (DSM 7.x). Defaults match the homelab
/// model (BL-016): a CIDR client, read-write, all-users-squashed-to-admin, AUTH_SYS.
/// </summary>
public sealed record NfsRuleSpec
{
    /// <summary>Client host or network, e.g. <c>10.10.0.0/16</c> or a single IP.</summary>
    public required string Client { get; init; }

    /// <summary><c>rw</c> or <c>ro</c>.</summary>
    public string Privilege { get; init; } = "rw";

    /// <summary>
    /// Squash mapping (<c>root_squash</c> in the API): <c>no</c> | <c>admin</c> | <c>guest</c> |
    /// <c>all_admin</c> | <c>all_guest</c>. <c>all_admin</c> = map every user to admin
    /// (the <c>all_squash</c> + anonuid/anongid=admin model).
    /// </summary>
    public string Squash { get; init; } = "all_admin";

    public bool Async { get; init; } = true;

    /// <summary>Allow connections from non-privileged ports (NFS <c>insecure</c>).</summary>
    public bool Insecure { get; init; } = true;

    /// <summary>Allow access to mounted subfolders (NFS <c>crossmnt</c>).</summary>
    public bool Crossmnt { get; init; } = true;

    /// <summary>Security flavor: <c>sys</c> | <c>krb5</c> | <c>krb5i</c> | <c>krb5p</c>.</summary>
    public string Security { get; init; } = "sys";
}

/// <summary>
/// Desired NFS export rules for a share. DSM's <c>save</c> is a whole-list REPLACE, so
/// <see cref="Rules"/> is the full intended rule set. <see cref="Present"/> false clears
/// all rules for the share.
/// </summary>
public sealed record NfsExportSpec
{
    public required string Share { get; init; }
    public IReadOnlyList<NfsRuleSpec> Rules { get; init; } = [];
    public bool Present { get; init; } = true;
}

/// <summary>A bundle of desired DSM state — the input to the reconciler.</summary>
public sealed record SynologyDesiredState
{
    public IReadOnlyList<GroupSpec> Groups { get; init; } = [];
    public IReadOnlyList<UserSpec> Users { get; init; } = [];
    public IReadOnlyList<ShareSpec> Shares { get; init; } = [];
    public IReadOnlyList<NfsExportSpec> NfsExports { get; init; } = [];
}
