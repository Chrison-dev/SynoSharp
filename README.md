# SynoSharp

A C# client for **Synology DSM** IaC. Sibling to ProxmoxSharp/UnifiSharp — but
**not code-generated**: Synology publishes no settings/deploy API schema, so per
[ADR-0002](https://github.com/chrison-dev/Homelab/blob/main/docs/adr/ADR-0002-synosharp.md)
this is a hand-written **read-API client** (now) + an **SSH-runner** for mutations (later).

## Approach

- **Read / discover** → the DSM **Web API** (`SYNO.API.Auth` → `entry.cgi` →
  `SYNO.Core.*`), returning a structured `SynologySnapshot`. *(This repo.)*
- **Mutations** (shares, NFS, users, network) → an **SSH-runner** over the on-box
  `syno*` CLI + `synowebapi` (added with the write phase). The `SYNO.Core.*`
  endpoints are undocumented/version-fragile — pinned to **DSM 7.1** (the DS1813+
  is EOL there).

## Projects

| Project | What |
| --- | --- |
| `src/SynoSharp/` | The client — `SynologyApiClient` (Web-API read), `SynologyDiscovery` → `SynologySnapshot`. SemVer. |
| `src/SynoSharp.Cli/` | `synosharp` dotnet tool — `discover`. |
| `tests/SynoSharp.Tests/` | Unit + skippable live test. |

## Build / use

```bash
dotnet build && dotnet test
export SYNOLOGY_BASE_URL=https://nas:5001 SYNOLOGY_USER=… SYNOLOGY_PASSWORD=… SYNOLOGY_VERIFY_TLS=false
synosharp discover     # JSON snapshot: DSM version, shares, users
```

Packages publish to GitHub Packages (chrison-dev) like the siblings: prerelease
on push to `main`, stable on `v*` tag.

## Status

**Read/discover scaffold — UNVERIFIED.** The Virtual DSM test container needs
KVM/x86 so it can't run on Apple Silicon; the discover path is wired but must be
verified against a DSM target (a Linux-hosted Virtual DSM, or the live NAS
read-only). The `SYNO.Core.*` reads are defensive (degrade to empty on shape
mismatch). **Next:** verify discover, then the SSH-runner for the write path.
