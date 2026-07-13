param(
    [string]$Version = "0.3.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$projectPath = Join-Path $repoRoot "helper\NextcloudExplorerHost\NextcloudExplorerHost.csproj"
$publishDir = Join-Path $repoRoot "dist\installer\app"
$releaseDir = Join-Path $repoRoot "dist\releases"
$amoBuildDir = Join-Path $repoRoot "dist\amo\build"
$installerScript = Join-Path $repoRoot "installer\NextcloudExplorerOpen.iss"

$localDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path -LiteralPath $localDotnet) {
    $dotnet = $localDotnet
} else {
    $dotnetCommand = Get-Command dotnet -ErrorAction Stop
    $dotnet = $dotnetCommand.Source
}

$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "Inno Setup 6 wurde nicht gefunden. Installiere JRSoftware.InnoSetup ueber winget."
}

foreach ($directory in @($publishDir, $releaseDir, $amoBuildDir)) {
    $fullPath = [IO.Path]::GetFullPath($directory)
    if (-not $fullPath.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Unsicherer Build-Pfad: $fullPath"
    }

    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

& $dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    throw "Der Windows-Helper konnte nicht erstellt werden."
}

$publishedHost = Join-Path $publishDir "NextcloudExplorerHost.exe"
if (-not (Test-Path -LiteralPath $publishedHost)) {
    throw "Die veroeffentlichte Helper-EXE fehlt."
}

$selfTest = Start-Process `
    -FilePath $publishedHost `
    -ArgumentList "--self-test" `
    -NoNewWindow `
    -Wait `
    -PassThru
if ($selfTest.ExitCode -ne 0) {
    throw "Der Helper-Selbsttest ist fehlgeschlagen."
}

& npx --yes web-ext@latest lint --source-dir (Join-Path $repoRoot "extension")
if ($LASTEXITCODE -ne 0) {
    throw "Die Firefox-Erweiterung hat die Validierung nicht bestanden."
}

& npx --yes web-ext@latest build `
    --source-dir (Join-Path $repoRoot "extension") `
    --artifacts-dir $amoBuildDir `
    --filename "nextcloud_explorer_open-$Version.zip" `
    --overwrite-dest
if ($LASTEXITCODE -ne 0) {
    throw "Die Firefox-Erweiterung konnte nicht paketiert werden."
}

& $iscc "/DAppVersion=$Version" $installerScript
if ($LASTEXITCODE -ne 0) {
    throw "Der Windows-Installer konnte nicht erstellt werden."
}

$artifacts = @(
    (Join-Path $releaseDir "nextcloud-explorer-open-setup-$Version.exe"),
    (Join-Path $amoBuildDir "nextcloud_explorer_open-$Version.zip")
)

foreach ($artifact in $artifacts) {
    if (-not (Test-Path -LiteralPath $artifact)) {
        throw "Build-Artefakt fehlt: $artifact"
    }
}

$checksums = foreach ($artifact in $artifacts) {
    $hash = Get-FileHash -LiteralPath $artifact -Algorithm SHA256
    "{0}  {1}" -f $hash.Hash.ToLowerInvariant(), (Split-Path -Leaf $artifact)
}
$checksums | Set-Content -LiteralPath (Join-Path $releaseDir "SHA256SUMS.txt") -Encoding ascii

Write-Host "Release $Version wurde erstellt:"
$artifacts | ForEach-Object { Write-Host "  $_" }
Write-Host "  $(Join-Path $releaseDir 'SHA256SUMS.txt')"
