#!/usr/bin/env pwsh
# SynoSharp build entrypoint (Fallout build). Requires the .NET 10 SDK on PATH
# (see global.json). Everything, including the Fallout.* build packages, restores from
# nuget.org with no credentials (see nuget.config).
#
#   ./build.ps1                              # default target: Test
#   ./build.ps1 Pack --version-suffix preview.42
#   ./build.ps1 Publish --nuget-api-key <key>
$ErrorActionPreference = 'Stop'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
& dotnet run --project "$ScriptDir/build/_build.csproj" -- $args
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
