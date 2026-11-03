[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Project,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = [IO.Path]::GetFullPath((Join-Path $repository '..'))
$target = [IO.Path]::GetFullPath($Project).TrimEnd([IO.Path]::DirectorySeparatorChar)
$allowed = @(
    [IO.Path]::GetFullPath((Join-Path $workspace 'InnoEngine.TestProject')),
    [IO.Path]::GetFullPath((Join-Path $workspace 'InnoEngine.Samples/NeonZero'))
)
if ($target -notin $allowed) { throw 'Only the two explicitly requested consumer workspaces may receive this layout.' }
$ini = Join-Path $target 'editor.ini'
$original = [IO.File]::ReadAllText($ini)
$sections = [ordered]@{}
$header = $null
foreach ($line in ($original -split '\r?\n')) {
    if ($line -match '^\[.+\]$') { $header = $line; $sections[$header] = [Collections.Generic.List[string]]::new() }
    elseif ($null -ne $header -and $line.Length -gt 0) { $sections[$header].Add($line) }
}
$viewport = $sections['[Window][WindowOverViewport_11111111]']
$size = @($viewport | Where-Object { $_ -match '^Size=' })[0]
if ($size -notmatch '^Size=(\d+),(\d+)$') { throw 'The existing main viewport has no valid size.' }
$width = [int]$Matches[1]; $height = [int]$Matches[2]
if ($width -lt 1000 -or $height -lt 600) { throw 'This workspace preset requires at least 1000 by 600 logical pixels.' }
$top = 35
$left = [int][Math]::Round($width * .15)
$right = [int][Math]::Round($width * .24)
$center = $width - $left - $right - 4
$upper = [int][Math]::Round($height * .60)
$lower = $height - $upper - 2
$centerX = $left + 2; $rightX = $width - $right; $bottomY = $top + $upper + 2
function Set-Window([string]$Name, [int]$X, [int]$Y, [int]$W, [int]$H, [string]$Dock, [int]$Tab) {
    $sections["[Window][$Name]"] = @("Pos=$X,$Y", "Size=$W,$H", 'Collapsed=0', "DockId=$Dock,$Tab")
}
# Retire obsolete translated plugin window identities without changing unrelated editor module state.
foreach ($key in @($sections.Keys)) {
    if ($key -match '^\[Window\]\[VN .*[^\x00-\x7F]') { $sections.Remove($key) }
}
Set-Window 'Hierarchy' 0 $top $left $height '0x00007101' 0
Set-Window 'Inspector' $rightX $top $right $height '0x00007102' 0
Set-Window 'Scene' $centerX $top $center $upper '0x00007103' 0
Set-Window 'Game' $centerX $top $center $upper '0x00007103' 1
Set-Window 'Visual Novel' $centerX $bottomY $center $lower '0x00007104' 0
Set-Window 'File' $centerX $bottomY $center $lower '0x00007104' 1
Set-Window 'Console' $centerX $bottomY $center $lower '0x00007104' 2
$remaining = $width - $left - 2
$sections['[Docking][Data]'] = @(
    "DockSpace       ID=0x08BD597D Window=0x1BBC0F80 Pos=0,$top Size=$width,$height Split=X",
    "  DockNode      ID=0x00007101 Parent=0x08BD597D SizeRef=$left,$height",
    "  DockNode      ID=0x00007105 Parent=0x08BD597D SizeRef=$remaining,$height Split=X",
    "    DockNode    ID=0x00007106 Parent=0x00007105 SizeRef=$center,$height Split=Y",
    "      DockNode  ID=0x00007103 Parent=0x00007106 SizeRef=$center,$upper CentralNode=1",
    "      DockNode  ID=0x00007104 Parent=0x00007106 SizeRef=$center,$lower",
    "    DockNode    ID=0x00007102 Parent=0x00007105 SizeRef=$right,$height"
)
$panels = [ordered]@{}
foreach ($line in $sections['[InnoEditor][Panels]']) {
    if ($line -match '^([^=]+)=') { $panels[$Matches[1]] = $false }
}
foreach ($name in @('scene.hierarchy', 'scene.inspector', 'rendering.scene-view', 'rendering.game-view', 'inno.vn.workspace', 'asset.file-browser', 'diagnostics.console')) { $panels[$name] = $true }
$sections['[InnoEditor][Panels]'] = @($panels.GetEnumerator() | ForEach-Object { $_.Key + '=' + $_.Value.ToString().ToLowerInvariant() })
# Hidden windows must not reference dock nodes removed by this preset.
foreach ($key in @($sections.Keys)) {
    if ($key -like '[[]Window[]]*' -and $key -notin @('[Window][Hierarchy]', '[Window][Inspector]', '[Window][Scene]', '[Window][Game]', '[Window][Visual Novel]', '[Window][File]', '[Window][Console]')) {
        $sections[$key] = @($sections[$key] | Where-Object { $_ -notmatch '^DockId=' })
    }
}
$result = ($sections.GetEnumerator() | ForEach-Object { $_.Key + "`r`n" + ($_.Value -join "`r`n") }) -join "`r`n`r`n"
$folder = Join-Path $repository ('artifacts/ConsumerWorkspaces/' + (Split-Path $target -Leaf) + '/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
[IO.Directory]::CreateDirectory($folder) | Out-Null
[IO.File]::WriteAllText((Join-Path $folder 'editor.previous.ini'), $original)
[IO.File]::WriteAllText((Join-Path $folder 'editor.proposed.ini'), $result + "`r`n")
if ($Apply) {
    if ([IO.File]::ReadAllText($ini) -cne $original) { throw 'The editor layout changed during preparation; the current file was preserved.' }
    [IO.File]::WriteAllText($ini, $result + "`r`n")
}
Write-Output "Layout: $folder; applied=$Apply; canvas=$center x $upper; story=$center x $lower"
