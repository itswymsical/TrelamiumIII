<#
.SYNOPSIS
    Generates the Ingress realm headlessly and renders each seed to PNGs plus a report.

.DESCRIPTION
    Copies the built TrelamiumIII.tmod and its two dependencies into a throwaway tModLoader save folder, enables only
    those, and starts a dedicated server with TRELAMIUM_INGRESS_HARNESS set. IngressGenHarness (Content/DevTools) then
    generates every seed after content loads, writes ingress_<seed>_*.png and ingress_<seed>_report.txt to the output
    folder, and exits the server before any world loads. Your own saves, worlds and enabled mods are never touched.

    Build the mod first (dotnet build in src/TrelamiumIII), which deploys TrelamiumIII.tmod to your Mods folder.

.EXAMPLE
    ./Run-IngressHarness.ps1 -Seeds 1,2,3
#>
param(
    [string]$Output = (Join-Path $PSScriptRoot 'out'),
    [string[]]$Seeds = @('1', '2', '3'),
    [string]$TModLoader = 'C:\Program Files (x86)\Steam\steamapps\common\tModLoader',
    [string]$Workshop = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\1281930',
    [string]$SaveDir = (Join-Path $env:TEMP 'TrelamiumIngressHarness'),
    [int]$TimeoutSeconds = 900
)

$ErrorActionPreference = 'Stop'

$userMods = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'My Games\Terraria\tModLoader\Mods'
$mods = Join-Path $SaveDir 'Mods'
New-Item -ItemType Directory -Force $mods | Out-Null
Copy-Item (Join-Path $userMods 'TrelamiumIII.tmod') $mods -Force

# Dependencies come from the workshop, newest tModLoader version folder first.
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

$outputPath = (New-Item -ItemType Directory -Force $Output).FullName
Get-ChildItem $outputPath -Filter 'ingress_*' | Remove-Item -Force

$env:TRELAMIUM_INGRESS_HARNESS = $outputPath
$env:TRELAMIUM_INGRESS_SEEDS = $Seeds -join ','

$dotnet = Join-Path $TModLoader 'dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) {
    $dotnet = 'dotnet'
}

$stdout = Join-Path $SaveDir 'server-stdout.txt'
$stderr = Join-Path $SaveDir 'server-stderr.txt'
$watch = [Diagnostics.Stopwatch]::StartNew()

$server = Start-Process $dotnet -ArgumentList @('tModLoader.dll', '-server', '-tmlsavedirectory', "`"$SaveDir`"") `
    -WorkingDirectory $TModLoader -PassThru -NoNewWindow -RedirectStandardOutput $stdout -RedirectStandardError $stderr

# Touching the handle keeps it open, or ExitCode comes back empty once the process is gone.
$null = $server.Handle

if (-not $server.WaitForExit($TimeoutSeconds * 1000)) {
    $server.Kill()
    throw "Server didn't finish in $TimeoutSeconds s; see $stdout"
}

Write-Host "Server exited with $($server.ExitCode) after $([int]$watch.Elapsed.TotalSeconds) s"
Get-ChildItem $outputPath -Filter 'ingress_*' | ForEach-Object { Write-Host "  $($_.FullName)" }

if (-not (Get-ChildItem $outputPath -Filter 'ingress_*_report.txt')) {
    Write-Host "No reports written; the server log is in $stdout"
    exit 1
}

exit $server.ExitCode
