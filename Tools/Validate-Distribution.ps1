[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EngineRoot,
    [Parameter(Mandatory)][string]$BuildAssembly,
    [string]$DotNet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$package = Join-Path $root 'artifacts/Distribution/InnoEngine.VisualNovel.iplugin'
$consumer = Join-Path $root ('artifacts/AcceptanceProjects/CleanConsumer-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Split-Path $package), (Join-Path $consumer 'Assets'), (Join-Path $consumer 'Plugins') -Force | Out-Null

& $DotNet run --project (Join-Path $PSScriptRoot 'Bootstrap/Bootstrap.csproj') `
    --artifacts-path (Join-Path $root 'artifacts/bootstrap') "-p:NovelEngineAssemblies=$(Split-Path -Parent $BuildAssembly)" -- `
    $root --plugin $root $package $DotNet
if ($LASTEXITCODE -ne 0) { throw 'Formal plugin export failed.' }
Copy-Item -LiteralPath $package -Destination (Join-Path $consumer 'Plugins/InnoEngine.VisualNovel.iplugin') -Force
& $DotNet run --project (Join-Path $PSScriptRoot 'DistributionCheck/DistributionCheck.csproj') `
    --artifacts-path (Join-Path $root 'artifacts/distribution-check') "-p:NovelEngineAssemblies=$(Split-Path -Parent $BuildAssembly)" -- $consumer
if ($LASTEXITCODE -ne 0) { throw 'Native package manifest validation failed.' }
& $DotNet run --project (Join-Path $PSScriptRoot 'Bootstrap/Bootstrap.csproj') `
    --artifacts-path (Join-Path $root 'artifacts/bootstrap') "-p:NovelEngineAssemblies=$(Split-Path -Parent $BuildAssembly)" -- `
    $root --scripts $consumer (Join-Path $consumer 'artifacts/scripts') $DotNet
if ($LASTEXITCODE -ne 0) { throw 'Clean consumer plugin activation failed.' }
Write-Host "Package: $package"
Write-Host "Clean consumer: $consumer"
