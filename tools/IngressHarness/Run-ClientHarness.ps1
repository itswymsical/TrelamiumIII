<#
.SYNOPSIS
    Launches the real game client into Ingress and saves screenshots of its landmarks.

.DESCRIPTION
    Uses a throwaway tModLoader save folder: copies the built TrelamiumIII.tmod and its two dependencies in, enables
    only those, writes a muted windowed config, and generates a small world there once with a dedicated server. The
    client is then started with -skipselect into that world and TRELAMIUM_CLIENT_HARNESS set; IngressClientHarness
    (Content/DevTools) enters Ingress, holds the camera on each landmark at several times of day and in the rain,
    writes client_*.png and client_report.txt to the output folder, and quits without saving.

    A game window opens while it runs. Steam has to be running. Your own saves and settings are never touched.

.EXAMPLE
    ./Run-ClientHarness.ps1
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot 'out'),
    [string]$TModLoader = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader',
    [string]$Workshop = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1281930',
    [string]$SaveDir = (Join-Path $env:TEMP 'TrelamiumClientHarness'),
    [int]$Width = 1600,
    [int]$Height = 900,
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'

$userMods = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\Terraria\tModLoader\Mods'
$mods = Join-Path $SaveDir 'Mods'
$worlds = Join-Path $SaveDir 'Worlds'
New-Item -ItemType Directory -Force $mods, $worlds | Out-Null
Copy-Item (Join-Path $userMods 'TrelamiumIII.tmod') $mods -Force

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

# Muted, windowed, colour lighting, English, so nothing waits on a first-launch prompt.
$config = [ordered]@{
    Language = 1
    VolumeSound = 0.0
    VolumeAmbient = 0.0
    VolumeMusic = 0.0
    Fullscreen = $false
    WindowMaximized = $false
    DisplayWidth = $Width
    DisplayHeight = $Height
    LightingMode = 3
    Zoom = 1.0
    CloudSavingDefault = $false
}
$config | ConvertTo-Json | Set-Content (Join-Path $SaveDir 'config.json') -Encoding ascii

$dotnet = Join-Path $TModLoader 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

$world = Join-Path $worlds 'Harness.wld'

if (-not (Test-Path $world)) {
    Write-Host 'Generating the throwaway main world (once)...'
    $serverLog = Join-Path $SaveDir 'worldgen-stdout.txt'

    $server = Start-Process $dotnet -ArgumentList @('tModLoader.dll', '-server', '-tmlsavedirectory', "`"$SaveDir`"", '-autocreate', '1',
        '-worldname', 'Harness', '-world', "`"$world`"", '-difficulty', '0', '-seed', 'harness', '-port', '7791') `
        -WorkingDirectory $TModLoader -PassThru -NoNewWindow `
        -RedirectStandardOutput $serverLog -RedirectStandardError (Join-Path $SaveDir 'worldgen-stderr.txt')

    # The world is saved before the server starts listening, so it can be stopped as soon as it says so.
    $watch = [Diagnostics.Stopwatch]::StartNew()

    while (-not $server.HasExited -and -not ((Test-Path $serverLog) -and (Select-String -Path $serverLog -Pattern 'Server started' -Quiet))) {
        if ($watch.Elapsed.TotalSeconds -gt $TimeoutSeconds) {
            $server.Kill()
            throw "World generation didn't finish; see $serverLog"
        }

        Start-Sleep -Seconds 2
    }

    if (-not $server.HasExited) {
        $server.Kill()
    }

    if (-not (Test-Path $world)) {
        throw "No world was written; see $SaveDir\worldgen-stdout.txt"
    }
}

# SubworldLibrary keeps a generated realm in a folder beside the main world and reloads it on entry, which would show the
# build that generated it rather than the one under test. Backups is tModLoader's own folder.
Get-ChildItem $worlds -Directory | Where-Object Name -ne 'Backups' | Remove-Item -Recurse -Force

$outputPath = (New-Item -ItemType Directory -Force $Output).FullName
Get-ChildItem $outputPath -Filter 'client_*' | Remove-Item -Force

$env:TRELAMIUM_CLIENT_HARNESS = $outputPath
$watch = [Diagnostics.Stopwatch]::StartNew()

$client = Start-Process $dotnet -ArgumentList @('tModLoader.dll', '-tmlsavedirectory', "`"$SaveDir`"", '-skipselect', 'Harness:Harness') `
    -WorkingDirectory $TModLoader -PassThru -RedirectStandardOutput (Join-Path $SaveDir 'client-stdout.txt') -RedirectStandardError (Join-Path $SaveDir 'client-stderr.txt')

$null = $client.Handle

if (-not $client.WaitForExit($TimeoutSeconds * 1000)) {
    $client.Kill()
    throw "The client didn't finish in $TimeoutSeconds s; see $SaveDir\client-stdout.txt"
}

Write-Host "Client exited after $([int]$watch.Elapsed.TotalSeconds) s"
Get-ChildItem $outputPath -Filter 'client_*' | ForEach-Object { Write-Host "  $($_.FullName)" }

# The harness ends its own process once it has written its report, so the report is what says it worked.
if (-not (Test-Path (Join-Path $outputPath 'client_report.txt'))) {
    Write-Host "No report written; the client log is in $SaveDir\client-stdout.txt"
    exit 1
}

exit 0
