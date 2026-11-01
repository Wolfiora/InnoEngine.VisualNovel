[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$EngineRoot,
    [Parameter(Mandatory)][string]$BuildAssembly,
    [string]$DotNet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$probe = Join-Path $root 'artifacts/AcceptanceProjects/RuntimeSerialization'
$assetRoot = Join-Path $probe 'Assets'
New-Item -ItemType Directory -Path $assetRoot -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'Tests/Fixtures/RuntimeSerializationProbe.cs.txt') -Destination (Join-Path $assetRoot 'RuntimeSerializationProbe.cs')
$report = Join-Path $root 'artifacts/RuntimeSerializationProbe.txt'
& $DotNet run --project (Join-Path $PSScriptRoot 'Bootstrap/Bootstrap.csproj') `
    --artifacts-path (Join-Path $root 'artifacts/bootstrap') "-p:NovelEngineAssemblies=$(Split-Path -Parent $BuildAssembly)" -- `
    $root --scripts $probe (Join-Path $probe 'artifacts/scripts') $DotNet 2>&1 | Tee-Object -FilePath $report
$probeExit = $LASTEXITCODE
if ($probeExit -eq 0) {
    Write-Host 'Runtime serializer probe compiled; reevaluate the previously recorded API gap.'
} elseif ([IO.File]::ReadAllText($report).Contains('SerializationRegistry')) {
    Write-Host 'Confirmed: the selected formal Runtime scripting surface does not expose SerializationRegistry.'
} else {
    throw 'Probe failed for an unrelated reason; inspect the report before claiming an API blocker.'
}
