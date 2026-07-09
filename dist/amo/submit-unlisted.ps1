param(
    [string]$SourceDir = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) "extension"),
    [string]$ArtifactsDir = (Join-Path $PSScriptRoot "signed")
)

$ErrorActionPreference = "Stop"

if (-not $env:AMO_JWT_ISSUER -or -not $env:AMO_JWT_SECRET) {
    throw "Bitte AMO_JWT_ISSUER und AMO_JWT_SECRET als Umgebungsvariablen setzen."
}

New-Item -ItemType Directory -Path $ArtifactsDir -Force | Out-Null

npx --yes web-ext@latest sign `
    --source-dir $SourceDir `
    --artifacts-dir $ArtifactsDir `
    --channel unlisted `
    --api-key $env:AMO_JWT_ISSUER `
    --api-secret $env:AMO_JWT_SECRET
