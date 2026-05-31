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
synosharp discover     # JSON snapshot: model, serial, DSM version, shares, users
```

Packages publish to GitHub Packages (chrison-dev) like the siblings: prerelease
on push to `main`, stable on `v*` tag.

## Status

**Read/discover — verified against the live NAS (2026-05-31, DS1813+ / DSM
7.1.1-42962).** `discover` returns model, serial, DSM version, share names and
user names (`SYNO.Core.System` / `Share` / `User`). The `SYNO.Core.*` reads are
defensive (degrade to empty on shape mismatch). **Next:** the SSH-runner for the
write path (shares/NFS/users via on-box `syno*` + `synowebapi`).
