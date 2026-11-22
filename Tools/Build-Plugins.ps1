# Builds every plugin in dependency order with the engine plugin exporter.
param(
    [string] $EngineRoot = (Join-Path $PSScriptRoot '../../InnoEngine'),
    [string] $ToolsTarget = $(if ($IsMacOS) { 'macos-arm64' } else { 'windows-x64' }),
    [string[]] $Only = @(),
    [switch] $Install
)

$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..')
$engine = Resolve-Path $EngineRoot
$artifacts = Join-Path $repo 'Artifacts'
$suite = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Plugins.psd1')
New-Item -ItemType Directory -Force $artifacts | Out-Null

foreach ($plugin in $suite.Plugins) {
    if ($Only.Count -gt 0 -and $Only -notcontains $plugin.Name) {
        continue
    }
    $project = Join-Path $repo "Plugins/$($plugin.Name)"
    $installed = Join-Path $project 'Plugins'
    Remove-Item -Recurse -Force $installed -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $installed | Out-Null
    foreach ($dependency in $plugin.Dependencies) {
        Copy-Item (Join-Path $artifacts "$dependency.iplugin") $installed
    }
    $arguments = @(
        'run', '--project', (Join-Path $engine 'entry/InnoEngine.Build'), '--',
        'plugin',
        '--tools-target', $ToolsTarget,
        '--project', $project,
        '--output', (Join-Path $artifacts "$($plugin.Id).iplugin"),
        '--display-name', $plugin.Name
    )
    if ($plugin.Dependencies.Count -gt 0) {
        $arguments += @('--dependencies', ($plugin.Dependencies -join ','))
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Plugin build failed: $($plugin.Name)"
    }
}

if ($Install) {
    $sandboxPlugins = Join-Path $repo 'Sandbox/Plugins'
    New-Item -ItemType Directory -Force $sandboxPlugins | Out-Null
    Copy-Item (Join-Path $artifacts '*.iplugin') $sandboxPlugins -Force
}
