#!/usr/bin/env bash
# SynoSharp build entrypoint (Fallout build). Requires the .NET 10 SDK on PATH
# (see global.json). Everything, including the Fallout.* build packages, restores from
# nuget.org with no credentials (see nuget.config).
#
#   ./build.sh                              # default target: Test
#   ./build.sh Pack --version-suffix preview.42
#   ./build.sh Publish --nuget-api-key <key>
set -eo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$SCRIPT_DIR/build/_build.csproj" -- "$@"
