<#
.SYNOPSIS
    Regenerate the Analytics Frontend C# client SDK using AutoRest.

.DESCRIPTION
    Reads the literate AutoRest configuration from README.md in this folder
    (which points at ../../openapi/V2026_03_01_Preview/frontend.yaml) and emits
    generated C# sources into ./auto-generated/. The generated code is compiled
    into the Azure.Cleanroom.Analytics.Frontend.Client NuGet package via the
    .csproj in this folder.

.PARAMETER UseDocker
    When set, invokes AutoRest inside a Node.js Docker container instead of
    requiring a local Node.js / autorest install.

.EXAMPLE
    pwsh ./generate.ps1
    pwsh ./generate.ps1 -UseDocker
#>

[CmdletBinding()]
param (
    [switch] $UseDocker
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptRoot = $PSScriptRoot
$outputDir = Join-Path $scriptRoot 'generated'

Write-Host "Cleaning previous AutoRest output: $outputDir"
if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
}
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

if ($UseDocker) {
    Write-Host 'Running AutoRest in Docker (node:20-bullseye)...'
    $repoRoot = (Resolve-Path (Join-Path $scriptRoot '..\..\..\..')).Path
    $relConfig = (Resolve-Path -Relative $scriptRoot)
    docker run --rm `
        -v "${repoRoot}:/work" `
        -w "/work/$relConfig" `
        node:20-bullseye `
        bash -c 'npm install -g autorest@3.7.1 && autorest --csharp ./README.md'
}
else {
    if (-not (Get-Command autorest -ErrorAction SilentlyContinue)) {
        Write-Host 'AutoRest CLI not found; installing globally via npm...'
        npm install -g autorest@3.7.1
    }

    Write-Host 'Running AutoRest...'
    autorest --csharp (Join-Path $scriptRoot 'README.md')
}

if ($LASTEXITCODE -ne 0) {
    throw "AutoRest generation failed with exit code $LASTEXITCODE."
}

# The @autorest/csharp generator emits a starter .csproj next to README.md.
# We replace it with our hand-curated NuGet-packageable csproj, so delete the
# generator's one to avoid two csproj files living side-by-side.
$generatedCsproj = Join-Path $scriptRoot 'AnalyticsFrontendAPI.csproj'
if (Test-Path $generatedCsproj) {
    Write-Host "Removing generator-emitted starter csproj: $generatedCsproj"
    Remove-Item -Force $generatedCsproj
}

Write-Host 'AutoRest generation complete.' -ForegroundColor Green
Write-Host "Output: $outputDir"
