[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BuildAssembly,
    [string]$DotNet = 'dotnet',
    [string]$ScriptOutput,
    [switch]$RefreshMotions,
    [switch]$RefreshCharacters,
    [switch]$RefreshStages,
    [switch]$RefreshThemes,
    [switch]$RefreshEffects,
    [switch]$RefreshSample
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$engineAssemblies = Split-Path -Parent (Resolve-Path -LiteralPath $BuildAssembly).Path
$resourceArguments = @($root)
if (-not $ScriptOutput) { $ScriptOutput = Join-Path $root 'artifacts/scripts' }
$ScriptOutput = (Resolve-Path -LiteralPath $ScriptOutput).Path
if (-not $ScriptOutput.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Resource writer script assemblies must come from inside this repository.' }
if ($RefreshMotions) { $resourceArguments += '--refresh-motions' }
if ($RefreshCharacters) { $resourceArguments += '--refresh-characters' }
if ($RefreshStages) { $resourceArguments += '--refresh-stages' }
if ($RefreshThemes) { $resourceArguments += '--refresh-themes' }
if ($RefreshEffects) { $resourceArguments += '--refresh-effects' }
if ($RefreshSample) { $resourceArguments += '--refresh-sample' }
& $DotNet run --project (Join-Path $PSScriptRoot 'BuiltInResources/BuiltInResources.csproj') `
    --artifacts-path (Join-Path $root 'artifacts/built-in-resources') "-p:NovelEngineAssemblies=$engineAssemblies" `
    "-p:NovelScriptOutput=$ScriptOutput" "-p:NovelDrawingAssembly=$(Join-Path $PSHOME 'System.Drawing.Common.dll')" -- @resourceArguments
if ($LASTEXITCODE -ne 0) { throw 'Original native resource generation failed.' }
