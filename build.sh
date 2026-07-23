#!/usr/bin/env bash
# SynoSharp build entrypoint (Fallout build). Requires the .NET 10 SDK on PATH
# (see global.json) and, for restoring the Fallout.* build packages, GITHUB_PACKAGES_PAT
# in the environment (a PAT with read:packages on the Fallout-build org; see nuget.config).
#
#   ./build.sh                              # default target: Test
#   ./build.sh Pack --version-suffix preview.42
#   ./build.sh Publish --nuget-api-key <key>
set -eo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec dotnet run --project "$SCRIPT_DIR/build/_build.csproj" -- "$@"
