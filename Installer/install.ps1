$ErrorActionPreference = 'Stop'
$repository = 'realTimexo/TXL-framework'
$installRoot = Join-Path $env:USERPROFILE '.dotnet'
$toolRoot = Join-Path $env:USERPROFILE '.dotnet\tools'

Write-Host 'TXL setup: checking for a .NET 9 SDK...' -ForegroundColor Cyan
$sdkList = @()
$dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCommand) { $sdkList = & dotnet --list-sdks 2>$null }
$hasSdk9 = $sdkList | Where-Object { $_ -match '^9\.' } | Select-Object -First 1
if (-not $hasSdk9) {
    Write-Host 'Installing the .NET 9 SDK to your user profile. Administrator rights are not required.'
    $bootstrap = Join-Path $env:TEMP 'dotnet-install-txl.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $bootstrap
    & $bootstrap -Channel '9.0' -InstallDir $installRoot -Architecture 'x64' -NoPath
    $sdkList = & (Join-Path $installRoot 'dotnet.exe') --list-sdks 2>$null
    if (-not ($sdkList | Where-Object { $_ -match '^9\.' })) { throw 'The .NET 9 SDK installer did not install a usable SDK.' }
}
if (-not (Test-Path (Join-Path $installRoot 'dotnet.exe')) -and $dotnetCommand) {
    $installRoot = Split-Path $dotnetCommand.Source -Parent
}
$env:DOTNET_ROOT = $installRoot
$env:PATH = "$installRoot;$toolRoot;$env:PATH"

Write-Host 'Looking up the latest TXL compiler release...' -ForegroundColor Cyan
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repository/releases/latest" -Headers @{ 'User-Agent' = 'TXL-Installer' }
$tag = [string]$release.tag_name
$tagMatch = [regex]::Match($tag, '^v(?<version>\d+\.\d+\.\d+)$')
if (-not $tagMatch.Success) { throw "The GitHub release tag '$tag' is not a stable semantic version." }
$version = $tagMatch.Groups['version'].Value
$packageName = "Txl.Cli.$version.nupkg"
$asset = $release.assets | Where-Object { $_.name -eq $packageName } | Select-Object -First 1
if (-not $asset) { throw "Release $($release.tag_name) does not contain $packageName. The release package may not have been uploaded yet." }
$expectedUrlPrefix = "https://github.com/$repository/releases/download/$tag/"
if (-not ([string]$asset.browser_download_url).StartsWith($expectedUrlPrefix, [StringComparison]::Ordinal)) { throw 'The GitHub release returned an unexpected TXL package URL.' }
$expectedDigest = [string]$asset.digest
if ($expectedDigest -notmatch '^sha256:[a-fA-F0-9]{64}$') { throw 'The GitHub release does not provide a valid SHA-256 package digest.' }
$tempFolder = Join-Path $env:TEMP ("txl-install-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempFolder | Out-Null
try {
    $packagePath = Join-Path $tempFolder $packageName
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $packagePath
    $actualDigest = 'sha256:' + (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualDigest -ne $expectedDigest.ToLowerInvariant()) { throw 'The downloaded TXL package failed its SHA-256 integrity check.' }
    $installed = & dotnet tool list --global 2>$null | Select-String -Pattern '^Txl\.Cli\s'
    if ($installed) { & dotnet tool update --global Txl.Cli --version $version --add-source $tempFolder --ignore-failed-sources }
    else { & dotnet tool install --global Txl.Cli --version $version --add-source $tempFolder --ignore-failed-sources }
    if ($LASTEXITCODE -ne 0) { throw 'The TXL global tool installation failed.' }
} finally {
    Remove-Item -LiteralPath $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
}

$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$pathEntries = @($userPath -split ';' | Where-Object { $_ })
foreach ($entry in @($installRoot, $toolRoot)) {
    if (-not ($pathEntries | Where-Object { $_.TrimEnd('\') -ieq $entry.TrimEnd('\') })) { $pathEntries += $entry }
}
[Environment]::SetEnvironmentVariable('Path', ($pathEntries -join ';'), 'User')
Write-Host "TXL $version is installed as a per-user .NET global tool." -ForegroundColor Green
Write-Host 'Open a new terminal, change to a TXL project folder, then run: txl --build'
Write-Host 'Update later with: txl --update'
Write-Host 'Other commands: txl --check, txl --version, and txl --help'
