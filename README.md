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
| `src/SynoSharp/` | The client — `SynologyApiClient` (Web-API read), `SynologyDiscovery` → `SynologySnapshot`, and `Ssh/` (the SSH-runner: `ISshRunner`/`SshRunner`, `SynologyCommand`). SemVer. |
| `src/SynoSharp.Cli/` | `synosharp` dotnet tool — `discover`, `ssh-check`. |
| `tests/SynoSharp.Tests/` | Unit + skippable live tests (Web-API + SSH). |

## Build / use

```bash
dotnet build && dotnet test
export SYNOLOGY_BASE_URL=https://nas:5001 SYNOLOGY_USER=… SYNOLOGY_PASSWORD=… SYNOLOGY_VERIFY_TLS=false
synosharp discover     # JSON snapshot: model, serial, DSM version, shares, users
synosharp ssh-check    # prove the SSH-runner: login + sudo-to-root + read-only `synoshare --enum`
```

The SSH-runner reuses `SYNOLOGY_USER`/`SYNOLOGY_PASSWORD` (the account also supplies
the `sudo` password — `syno*` need root) and derives the host from `SYNOLOGY_BASE_URL`;
override with `SYNOLOGY_SSH_HOST`/`SYNOLOGY_SSH_PORT`/`SYNOLOGY_SSH_KEY`. DSM 7 disables
direct root SSH, so the runner logs in as an admin user and `sudo -S` (password over
stdin, never in the command line), running tools through `env PATH=/usr/syno/sbin:…`
since sudo's `secure_path` excludes the syno dirs.

Packages publish to GitHub Packages (chrison-dev) like the siblings: prerelease
on push to `main`, stable on `v*` tag.

## Status

**Read/discover — verified against the live NAS (2026-05-31, DS1813+ / DSM
7.1.1-42962).** `discover` returns model, serial, DSM version, share names and
user names (`SYNO.Core.System` / `Share` / `User`). The `SYNO.Core.*` reads are
defensive (degrade to empty on shape mismatch).

**SSH-runner transport — verified against the live NAS (2026-05-31).** `ssh-check`
proves the full stack end-to-end (SSH login → sudo-to-root → on-box `syno*`):
`synoshare --enum ALL` lists the live shares with **zero mutation**. The runner
(`ISshRunner`/`SshRunner` over SSH.NET) + structured `SynologyCommand` (shell-quoted
argv) are the transport for the write path.

**Next:** the first typed mutation — `EnsureShareAsync` with **read-before-write**
(diff the discover snapshot, emit only the needed command) and **dry-run by default**.
NFS exports come last (highest-risk; prove on Virtual DSM, not the live box).
