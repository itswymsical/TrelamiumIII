<#
.SYNOPSIS
    Exports fields from the VFX Lab into a copy of the repository, rebuilds it, and runs what came out.

.DESCRIPTION
    The test of phase 4: an export has to build first time and run with the lab unused. Everything happens inside the copy, so make one
    you don't mind losing, from a clean checkout or with git archive. It has to be in the state you want to test, and it mustn't hold an
    earlier export. Two game windows open in turn, showing steady muted colours. Steam has to be running.

.PARAMETER Copy
    A copy of the repository: the folder that holds TrelamiumIII.sln. Never point this at your real checkout.

.PARAMETER Output
    Where the reports, screenshots and built mods go.

.PARAMETER SaveDir
    A throwaway tModLoader save folder, so as not to share one with anything else running.

.PARAMETER Driver
    Forces an FNA3D backend, such as OpenGL, for both clients.

.EXAMPLE
    ./Run-ExportRoundTrip.ps1 -Copy $env:TEMP\TrelamiumExport\repo
#>
param(
    [Parameter(Mandatory)]
    [string]$Copy,
    [string]$Output = (Join-Path $PSScriptRoot 'out\export'),
    [string]$SaveDir = (Join-Path $env:TEMP 'TrelamiumVfxExportHarness'),
    [string]$Driver = ''
)

$ErrorActionPreference = 'Stop'

$source = (Resolve-Path (Join-Path $Copy 'src\TrelamiumIII')).Path
$project = Join-Path $source 'TrelamiumIII.csproj'
$outputPath = (New-Item -ItemType Directory -Force $Output).FullName
$harness = Join-Path $PSScriptRoot 'Run-Harness.ps1'
$compiled = Join-Path $source 'Assets\Shaders\Vfx\ExportProbe.fxc'

if (Test-Path $compiled) {
    throw "$compiled is already there, so this copy has been through an export. Make a new one."
}

function Build-Copy([string]$name) {
    $tmod = Join-Path $outputPath "$name.tmod"

    dotnet build $project -c Release -nologo -v quiet "-p:TmodOutputPath=$tmod" | Select-Object -Last 3 | ForEach-Object { Write-Host $_ }

    if ($LASTEXITCODE -ne 0) {
        throw "The $name build failed."
    }

    return $tmod
}

function Run-Script([string]$script, [string]$tmod) {
    & $harness -Script $script -ShaderSource $source -Tmod $tmod -Output (Join-Path $outputPath $script) -SaveDir $SaveDir -Driver $Driver

    if ($LASTEXITCODE -ne 0) {
        throw "The $script script didn't finish."
    }

    if (Select-String -Path (Join-Path $outputPath "$script\vfx_report.txt") -Pattern '^FAIL' -Quiet) {
        throw "The $script script reported failures."
    }
}

Write-Host '== Building the copy as it is =='
$before = Build-Copy 'before'

Write-Host '== Exporting from the lab =='
Run-Script 'export' $before

$exportedHash = (Get-FileHash $compiled).Hash

Write-Host '== Rebuilding, once, with the exports in it =='
$after = Build-Copy 'after'

if ((Get-FileHash $compiled).Hash -ne $exportedHash) {
    throw "The build rewrote $compiled, so the lab's compile doesn't match the SDK's."
}

Write-Host 'PASS: one build was enough, and it left the exported shader byte for byte as the lab wrote it'

Write-Host '== Running what came out =='
Run-Script 'exported' $after

Write-Host 'PASS: the round trip works'
