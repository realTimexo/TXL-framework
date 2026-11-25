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
    $selectedToolRoot = $toolRoot
    if ($installed) {
        $globalOutput = & dotnet tool update --global Txl.Cli --version $version --add-source $tempFolder --ignore-failed-sources 2>&1
        $globalUpdateExitCode = $LASTEXITCODE
        $globalUpdateText = ($globalOutput | Out-String)
        if ($globalUpdateExitCode -eq 0) {
            $globalOutput | ForEach-Object { Write-Host $_ }
        }
        else {
            $globalOutput | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
            $hasTlxStorePath = $globalUpdateText -match '(?i)[\\/]\.store[\\/].*txl\.cli'
            $isLocalizedAccessDenied = ($globalUpdateText -match '(?i)access to the path.{0,500}denied') -or ($globalUpdateText -match '(?i)zugriff auf den pfad.{0,500}(verweigert|nicht zulässig)')
            if (-not ($hasTlxStorePath -and $isLocalizedAccessDenied)) {
                throw "The global TXL update failed for a reason other than the known .NET tool-store access-denied issue. No isolated fallback was attempted. $globalUpdateText"
            }
            Write-Host 'The existing global TXL package is locked or inaccessible. Installing into a fresh versioned per-user TXL folder instead.' -ForegroundColor Yellow
            $selectedToolRoot = Join-Path $env:USERPROFILE ".txl\tools\$version"
            New-Item -ItemType Directory -Force -Path $selectedToolRoot | Out-Null
            & dotnet tool install Txl.Cli --tool-path $selectedToolRoot --version $version --add-source $tempFolder --ignore-failed-sources
            if ($LASTEXITCODE -ne 0) { throw 'The old global package could not be updated, and the isolated per-user TXL install also failed.' }
        }
    }
    else {
        & dotnet tool install --global Txl.Cli --version $version --add-source $tempFolder --ignore-failed-sources
        if ($LASTEXITCODE -ne 0) { throw 'The TXL global tool installation failed.' }
    }
} finally {
    Remove-Item -LiteralPath $tempFolder -Recurse -Force -ErrorAction SilentlyContinue
}

try {
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $pathEntries = @($userPath -split ';' | Where-Object { $_ -and $_.TrimEnd('\') -ine $selectedToolRoot.TrimEnd('\') })
    $pathEntries = @($selectedToolRoot) + $pathEntries
    foreach ($entry in @($installRoot, $toolRoot)) {
        if (-not ($pathEntries | Where-Object { $_.TrimEnd('\') -ieq $entry.TrimEnd('\') })) { $pathEntries += $entry }
    }
    [Environment]::SetEnvironmentVariable('Path', ($pathEntries -join ';'), 'User')
} catch {
    Write-Warning "TXL $version is installed in '$selectedToolRoot', but the user PATH could not be saved. Add that folder to PATH manually. $($_.Exception.Message)"
}
Write-Host "TXL $version is installed for the current Windows user in '$selectedToolRoot'." -ForegroundColor Green
Write-Host 'Open a new terminal, change to a TXL project folder, then run: txl --build'
Write-Host 'Update later with: txl --update'
Write-Host 'Other commands: txl --check, txl --version, and txl --help'
