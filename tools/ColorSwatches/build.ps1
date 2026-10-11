# Builds ColorSwatches.vsix with Visual Studio's MSBuild. Double-click the .vsix to install it, then restart VS.
$ErrorActionPreference = 'Stop'

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found; is Visual Studio installed?' }

& $msbuild (Join-Path $PSScriptRoot 'ColorSwatches.csproj') -restore -p:Configuration=Release -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem (Join-Path $PSScriptRoot 'bin\Release') -Recurse -Filter *.vsix | ForEach-Object { $_.FullName }
