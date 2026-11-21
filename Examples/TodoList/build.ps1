$ErrorActionPreference = 'Stop'
$kitRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$compiler = Join-Path $kitRoot 'Compiler\Txl.Cli\Txl.Cli.csproj'

Write-Host 'Checking TXL project...'
dotnet run --project $compiler -- check $PSScriptRoot
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Building self-contained Windows x64 application...'
dotnet run --project $compiler -- build $PSScriptRoot
exit $LASTEXITCODE
