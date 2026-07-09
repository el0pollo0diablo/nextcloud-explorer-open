param(
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$hostName = "org.covasala.nextcloud_explorer"
$extensionId = "nextcloud-explorer-open@covasala.org"
$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "helper\NextcloudExplorerHost\NextcloudExplorerHost.csproj"
$publishDir = Join-Path $repoRoot "dist\host"
$manifestPath = Join-Path $repoRoot "dist\$hostName.json"
$exePath = Join-Path $publishDir "NextcloudExplorerHost.exe"
$registryKey = "HKCU\Software\Mozilla\NativeMessagingHosts\$hostName"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet SDK wurde nicht gefunden. Bitte .NET 8 SDK installieren oder den Helper anderweitig publishen."
}

$publishArgs = @(
    "publish",
    $projectPath,
    "-c",
    "Release",
    "-r",
    $RuntimeIdentifier,
    "-o",
    $publishDir
)

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
} else {
    $publishArgs += "--self-contained"
    $publishArgs += "false"
}

& dotnet @publishArgs

if (-not (Test-Path $exePath)) {
    throw "Helper-EXE wurde nicht erstellt: $exePath"
}

$manifest = [ordered]@{
    name = $hostName
    description = "Opens Nextcloud WebDAV folders in Windows Explorer."
    path = $exePath
    type = "stdio"
    allowed_extensions = @($extensionId)
}

New-Item -ItemType Directory -Path (Split-Path -Parent $manifestPath) -Force | Out-Null
$manifest | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8

& reg.exe add $registryKey /ve /t REG_SZ /d $manifestPath /f | Out-Null

Write-Host "Native-Messaging-Host installiert."
Write-Host "Host-Manifest: $manifestPath"
Write-Host "Registry-Key:  $registryKey"
Write-Host ""
Write-Host "Naechster Schritt: Firefox -> about:debugging -> Diese Firefox-Installation -> Temporaeres Add-on laden -> extension/manifest.json"
