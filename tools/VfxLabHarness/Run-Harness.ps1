<#
.SYNOPSIS
    Runs one of VfxLabHarness's scripts in the real game client.

.DESCRIPTION
    Uses a throwaway tModLoader save folder, so your own saves and settings are never touched. The world and player come from
    the Ingress client harness's folder, so run tools/IngressHarness/Run-ClientHarness.ps1 once first. VfxLabHarness
    (Content/DevTools/VfxLab) writes vfx_report.txt and vfx_*.png to the output folder and quits.

    A game window opens while it runs, and shows steady muted colours, never a flash. Steam has to be running.

.PARAMETER Script
    parity holds generated shaders to the ones they replace. spike proves live shader swapping works, and measures it. bench
    drives the Bench's panels with scripted input. lab walks through the lab realm: rooms, designing a field, its physics, residue,
    undo, save and load. export drives an export's review and writes two fields. exported runs those fields once the copy has been
    rebuilt; Run-ExportRoundTrip.ps1 does all of that in order.

.PARAMETER ShaderSource
    For bench, lab, export and exported: a copy of src/TrelamiumIII whose files they may edit, to test hot reload, save recipes into and
    export to. Never point this at your real source.

.PARAMETER Tmod
    The built mod to test. Defaults to the one in your Mods folder.

.PARAMETER Driver
    Forces an FNA3D backend, such as OpenGL. Defaults to whatever FNA picks.

.EXAMPLE
    ./Run-Harness.ps1 -Script parity -Driver OpenGL
#>
param(
    [ValidateSet('parity', 'spike', 'bench', 'lab', 'export', 'exported')]
    [string]$Script = 'parity',
    [string]$ShaderSource = '',
    [string]$Tmod = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\Terraria\tModLoader\Mods\TrelamiumIII.tmod'),
    [string]$Output = (Join-Path $PSScriptRoot 'out'),
    [string]$TModLoader = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader',
    [string]$Workshop = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1281930',
    [string]$SaveDir = (Join-Path $env:TEMP 'TrelamiumVfxHarness'),
    [string]$WorldSource = (Join-Path $env:TEMP 'TrelamiumClientHarness'),
    [string]$Driver = '',
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$mods = Join-Path $SaveDir 'Mods'
$worlds = Join-Path $SaveDir 'Worlds'
New-Item -ItemType Directory -Force $mods, $worlds | Out-Null
Copy-Item $Tmod (Join-Path $mods 'TrelamiumIII.tmod') -Force

foreach ($dependency in 'SubworldLibrary', 'ModLiquidLib') {
    $file = Get-ChildItem $Workshop -Recurse -Filter "$dependency.tmod" |
        Sort-Object { [version]($_.Directory.Name + '.0') } -Descending |
        Select-Object -First 1

    if (-not $file) {
        throw "Couldn't find $dependency.tmod under $Workshop"
    }

    Copy-Item $file.FullName $mods -Force
}

Set-Content (Join-Path $mods 'enabled.json') '["TrelamiumIII", "SubworldLibrary", "ModLiquidLib"]' -Encoding ascii

$world = Join-Path $WorldSource 'Worlds\Harness.wld'

if (-not (Test-Path $world)) {
    throw "No world at $world; run tools/IngressHarness/Run-ClientHarness.ps1 once first."
}

Copy-Item (Join-Path $WorldSource 'Worlds\Harness.*') $worlds -Force
Copy-Item (Join-Path $WorldSource 'config.json') $SaveDir -Force

$dotnet = Join-Path $TModLoader 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

$outputPath = (New-Item -ItemType Directory -Force $Output).FullName
Get-ChildItem $outputPath -Filter 'vfx_*' | Remove-Item -Force

$env:TRELAMIUM_VFX_HARNESS = $outputPath
$env:TRELAMIUM_VFX_SCRIPT = $Script
$env:TRELAMIUM_VFX_SOURCE = $ShaderSource

if ($Driver) {
    $env:FNA3D_FORCE_DRIVER = $Driver
}

$watch = [Diagnostics.Stopwatch]::StartNew()

$client = Start-Process $dotnet -ArgumentList @('tModLoader.dll', '-tmlsavedirectory', "`"$SaveDir`"", '-skipselect', 'Harness:Harness') `
    -WorkingDirectory $TModLoader -PassThru -RedirectStandardOutput (Join-Path $SaveDir 'client-stdout.txt') -RedirectStandardError (Join-Path $SaveDir 'client-stderr.txt')

$null = $client.Handle

if (-not $client.WaitForExit($TimeoutSeconds * 1000)) {
    $client.Kill()
    Write-Host "The client didn't finish in $TimeoutSeconds s; see $SaveDir\client-stdout.txt"
}

Write-Host "Client exited after $([int]$watch.Elapsed.TotalSeconds) s, code $($client.ExitCode)"

$report = Join-Path $outputPath 'vfx_report.txt'

if (-not (Test-Path $report)) {
    Write-Host "No report written; the client log is in $SaveDir\client-stdout.txt"
    exit 1
}

Get-Content $report

if (-not (Select-String -Path $report -Pattern '^done$' -Quiet)) {
    Write-Host "The report doesn't end in done; the client log is in $SaveDir\client-stdout.txt"
    exit 1
}

exit 0
