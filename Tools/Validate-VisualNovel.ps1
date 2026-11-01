[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EngineRoot,
    [Parameter(Mandatory)][string]$BuildAssembly,
    [string]$DotNet = 'dotnet',
    [switch]$CompileOnly
)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$enginePath = (Resolve-Path -LiteralPath $EngineRoot).Path
$buildPath = (Resolve-Path -LiteralPath $BuildAssembly).Path
$dotnetPath = (Get-Command $DotNet -ErrorAction Stop).Source
$engineAssemblies = Split-Path -Parent $buildPath
$outputRoot = Join-Path $projectRoot 'artifacts'
$scripts = Join-Path $outputRoot 'scripts'

& $dotnetPath run --project (Join-Path $PSScriptRoot 'Bootstrap/Bootstrap.csproj') `
    --artifacts-path (Join-Path $outputRoot 'bootstrap') "-p:NovelEngineAssemblies=$engineAssemblies" -- $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Native project bootstrap failed.' }

& $dotnetPath run --project (Join-Path $PSScriptRoot 'Bootstrap/Bootstrap.csproj') `
    --artifacts-path (Join-Path $outputRoot 'bootstrap') "-p:NovelEngineAssemblies=$engineAssemblies" -- `
    $projectRoot --scripts $projectRoot $scripts $dotnetPath
if ($LASTEXITCODE -ne 0) { throw 'Formal scripting API compilation failed.' }

if (-not $CompileOnly) {
& $dotnetPath build (Join-Path $projectRoot 'Tests/Contracts/Contracts.csproj') -t:Rebuild `
    --artifacts-path (Join-Path $outputRoot 'contracts') "-p:NovelEngineAssemblies=$engineAssemblies" `
    "-p:NovelScriptOutput=$scripts"
if ($LASTEXITCODE -ne 0) { throw 'Contract harness build failed.' }
& $dotnetPath (Join-Path $outputRoot 'contracts/bin/Contracts/debug/Contracts.dll') --write-sample $projectRoot
if ($LASTEXITCODE -ne 0) { throw 'Behavioral contract validation failed.' }
}

& $dotnetPath run --project (Join-Path $PSScriptRoot 'ArchitectureCheck/ArchitectureCheck.csproj') `
    --artifacts-path (Join-Path $outputRoot 'architecture') -- $projectRoot $scripts
if ($LASTEXITCODE -ne 0) { throw 'Compiled architecture validation failed.' }
if ($CompileOnly) { Write-Host 'Formal script compilation and architecture checks only; behavioral tests were not run.' }
