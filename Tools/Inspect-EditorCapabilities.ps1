[CmdletBinding()]
param([string]$DotNet = 'dotnet')

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ideProject = Join-Path $root 'Inno.VisualNovel.Editor.csproj'
if (!(Test-Path -LiteralPath $ideProject)) { throw 'Generate formal scripting IDE projects before inspecting their exact API reference.' }
[xml]$projectXml = Get-Content -LiteralPath $ideProject -Raw
$reference = @($projectXml.SelectNodes('//Reference') | Where-Object { $_.Include -eq 'Inno.ScriptApi.Editor' })
if ($reference.Count -ne 1) { throw 'The generated IDE project must select exactly one formal Editor API reference.' }
$api = [string]$reference[0].HintPath
& $DotNet run --project (Join-Path $PSScriptRoot 'ArchitectureCheck/ArchitectureCheck.csproj') `
    --artifacts-path (Join-Path $root 'artifacts/architecture') -- $root (Join-Path $root 'artifacts/scripts') $api
if ($LASTEXITCODE -ne 0) { throw 'Formal Editor API capability inspection failed.' }
