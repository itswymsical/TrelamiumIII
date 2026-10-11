<#
.SYNOPSIS
    Forwards committed changes between Substrate and TrelamiumIII.

.DESCRIPTION
    Diffs the source repo from its last synced commit to HEAD, maps each changed file onto the target's layout
    (folders, namespaces, generated asset keys, mod name) and three-way merges it into the target working tree, so
    edits made only on the target side survive. Dry run unless -Apply. Nothing is staged or committed.

    ToTrelamium forwards every change, new files included, except Substrate-only files ([SubstrateExclusive] or the
    "substrate" excludes). ToSubstrate only touches files Substrate already has, unless -IncludeNew.
    Localization is never written; changed .hjson files are listed for a manual merge.

.EXAMPLE
    .\Sync-Substrate.ps1                            # preview Substrate -> TrelamiumIII
    .\Sync-Substrate.ps1 -Apply                     # write it, then record Substrate's HEAD as synced
    .\Sync-Substrate.ps1 -Direction ToSubstrate     # preview TrelamiumIII -> Substrate
    .\Sync-Substrate.ps1 -SetBaseline               # record both HEADs as synced without copying anything
#>
param(
    [ValidateSet('ToTrelamium', 'ToSubstrate')]
    [string]$Direction = 'ToTrelamium',

    [switch]$Apply,

    # ToSubstrate only: also create files Substrate doesn't have yet.
    [switch]$IncludeNew,

    [switch]$SetBaseline,

    # Deleting a file in one repo often just means that repo doesn't need it, so deletes are reported, not applied.
    [switch]$AllowDeletes,

    # Prints a unified diff of every file that would be written.
    [switch]$ShowDiff,

    # Overrides the recorded base commit of the source repo, e.g. to replay an older range.
    [string]$Since
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2

$configPath = Join-Path $PSScriptRoot 'sync.json'
$config = Get-Content $configPath -Raw | ConvertFrom-Json

$substrateRepo = (Resolve-Path (Join-Path $PSScriptRoot $config.substrateRepo)).Path
$trelamiumRepo = (Resolve-Path (Join-Path $PSScriptRoot $config.trelamiumRepo)).Path

$textExtensions = @('.cs', '.fx', '.hjson', '.json', '.txt', '.md', '.ps1', '.xml', '.props', '.targets')
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

#region git

function Invoke-Git([string]$Repo, [string[]]$GitArgs) {
    $quoted = $GitArgs | ForEach-Object { if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ } }

    $psi = New-Object System.Diagnostics.ProcessStartInfo 'git'
    $psi.Arguments = "-C `"$Repo`" " + ($quoted -join ' ')
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $process = [System.Diagnostics.Process]::Start($psi)
    $stdout = New-Object System.IO.MemoryStream
    $errTask = $process.StandardError.ReadToEndAsync()
    $process.StandardOutput.BaseStream.CopyTo($stdout)
    $process.WaitForExit()

    [pscustomobject]@{ Code = $process.ExitCode; Bytes = $stdout.ToArray(); Error = $errTask.Result }
}

function Get-GitText([string]$Repo, [string[]]$GitArgs) {
    $result = Invoke-Git $Repo $GitArgs
    if ($result.Code -ne 0) { throw "git $($GitArgs -join ' ') failed in ${Repo}: $($result.Error)" }
    [Text.Encoding]::UTF8.GetString($result.Bytes).Trim()
}

function Get-Blob([string]$Repo, [string]$Commit, [string]$RepoPath) {
    $result = Invoke-Git $Repo @('show', "${Commit}:$RepoPath")
    if ($result.Code -ne 0) { return $null }
    , $result.Bytes
}

#endregion

#region text

function Test-Text([string]$Path) { $textExtensions -contains [IO.Path]::GetExtension($Path).ToLowerInvariant() }

# Decoded content with the BOM removed and line endings normalised to LF, plus the BOM flag.
function ConvertFrom-Bytes([byte[]]$Bytes) {
    if ($null -eq $Bytes) { return $null }
    $text = [Text.Encoding]::UTF8.GetString($Bytes)
    $bom = $text.Length -gt 0 -and $text[0] -eq [char]0xFEFF
    if ($bom) { $text = $text.Substring(1) }
    [pscustomobject]@{ Text = $text.Replace("`r`n", "`n"); Bom = $bom }
}

function ConvertTo-Bytes([string]$Text, [bool]$Bom) {
    $body = $utf8NoBom.GetBytes($Text.Replace("`n", "`r`n"))
    if (-not $Bom) { return , $body }
    , ([byte[]](0xEF, 0xBB, 0xBF) + $body)
}

function Test-SameText($A, $B) {
    if ($null -eq $A -or $null -eq $B) { return $false }
    [string]::Equals($A, $B, [StringComparison]::Ordinal)
}

function Test-SameBytes([byte[]]$A, [byte[]]$B) {
    if ($null -eq $A -or $null -eq $B) { return $false }
    [System.Collections.StructuralComparisons]::StructuralEqualityComparer.Equals($A, $B)
}

#endregion

#region mapping

function ConvertTo-GlobRegex([string]$Glob) {
    $escaped = [regex]::Escape($Glob) -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*' -replace '\\\?', '.'
    "^$escaped$"
}

function Test-Excluded([string]$Path, [string[]]$Globs) {
    foreach ($glob in $Globs) {
        if ($Path -match (ConvertTo-GlobRegex $glob)) { return $true }
    }
    $false
}

$toTrelamium = $Direction -eq 'ToTrelamium'

if ($toTrelamium) {
    $sourceRepo = $substrateRepo; $targetRepo = $trelamiumRepo
    $sourceMod = $config.substrateMod; $targetMod = $config.trelamiumMod
    $sourceName = 'SubstrateMod'; $targetName = 'TrelamiumIII'
    $sourceKey = 'substrate'; $targetKey = 'trelamium'
} else {
    $sourceRepo = $trelamiumRepo; $targetRepo = $substrateRepo
    $sourceMod = $config.trelamiumMod; $targetMod = $config.substrateMod
    $sourceName = 'TrelamiumIII'; $targetName = 'SubstrateMod'
    $sourceKey = 'trelamium'; $targetKey = 'substrate'
}

# Many-to-one folders (Common/Globals/Tiles and /Items -> Common/Globals) map back to the first one listed.
$folders = @($config.folders | ForEach-Object { [pscustomobject]@{ From = $_.$sourceKey; To = $_.$targetKey } }) |
    Sort-Object { $_.From.Length } -Descending
$namespaces = @($config.namespaces | ForEach-Object { [pscustomobject]@{ From = $_.$sourceKey; To = $_.$targetKey } }) |
    Sort-Object { $_.From.Length } -Descending

$excludes = @($config.exclude.both) + @($config.exclude.$sourceKey)

function Get-MappedPath([string]$Path) {
    foreach ($folder in $folders) {
        if ($Path.StartsWith($folder.From + '/', [StringComparison]::OrdinalIgnoreCase)) {
            return $folder.To + $Path.Substring($folder.From.Length)
        }
    }
    $Path
}

# Ordered text rewrites: namespaces, generated asset keys (path-derived), string paths, then the mod name itself.
$rules = New-Object System.Collections.Generic.List[object]
$boundaryStart = '(?<![A-Za-z0-9_])'
$boundaryEnd = '(?![A-Za-z0-9_])'

$seenNamespaces = @{}
foreach ($pair in $namespaces) {
    if ($seenNamespaces.ContainsKey($pair.From)) { continue }
    $seenNamespaces[$pair.From] = $true
    $rules.Add(@(($boundaryStart + [regex]::Escape($pair.From) + $boundaryEnd), $pair.To))
}

$seenFolders = @{}
foreach ($folder in $folders) {
    if ($seenFolders.ContainsKey($folder.From)) { continue }
    $seenFolders[$folder.From] = $true

    $fromDots = $folder.From.Replace('/', '.')
    $toDots = $folder.To.Replace('/', '.')
    # PowerShell's comma binds tighter than +, so every concatenated pattern is parenthesised.
    $rules.Add(@(($boundaryStart + 'AssetReferences\.' + [regex]::Escape($fromDots) + $boundaryEnd), "AssetReferences.$toDots"))

    if ($folder.From.StartsWith('Assets/') -and $folder.To.StartsWith('Assets/')) {
        $rules.Add(@(('(?<![A-Za-z0-9_.])Assets\.' + [regex]::Escape($fromDots.Substring(7)) + $boundaryEnd), ('Assets.' + $toDots.Substring(7))))
    }

    $rules.Add(@(('(?<=' + $sourceName + '\)?\}?/)' + [regex]::Escape($folder.From) + '(?=/)'), $folder.To))
}

$rules.Add(@(($boundaryStart + $sourceName + $boundaryEnd), $targetName))

function Convert-SourceText([string]$Text) {
    foreach ($rule in $rules) {
        if ($null -eq $Text) { throw 'Convert-SourceText: null text' }
        if ($null -eq $rule[1]) { throw "Convert-SourceText: null replacement for $($rule[0])" }
        $Text = [regex]::Replace($Text, [string]$rule[0], [string]$rule[1])
    }
    $Text
}

#endregion

#region baseline

# Rewrites only the two commit hashes, so the file keeps its hand formatting.
function Save-Config {
    $text = [IO.File]::ReadAllText($configPath)
    foreach ($key in 'substrate', 'trelamium') {
        $text = [regex]::Replace($text, "(`"$key`"\s*:\s*`")[0-9a-f]{40}(`")", "`${1}$($config.lastSynced.$key)`${2}")
    }
    [IO.File]::WriteAllText($configPath, $text, $utf8NoBom)
}

if ($SetBaseline) {
    $config.lastSynced.substrate = Get-GitText $substrateRepo @('rev-parse', 'HEAD')
    $config.lastSynced.trelamium = Get-GitText $trelamiumRepo @('rev-parse', 'HEAD')
    Save-Config
    "Baseline set: Substrate $($config.lastSynced.substrate.Substring(0, 7)), TrelamiumIII $($config.lastSynced.trelamium.Substring(0, 7))"
    return
}

#endregion

$base = if ($Since) { Get-GitText $sourceRepo @('rev-parse', $Since) } else { $config.lastSynced.$sourceKey }
$head = Get-GitText $sourceRepo @('rev-parse', 'HEAD')

if ($base -eq $head) {
    "Nothing to sync: $sourceName is at the last synced commit ($($head.Substring(0, 7)))."
    return
}

$targetRoot = Join-Path $targetRepo $targetMod

# Target files by name, for pairing a source file with wherever its twin lives.
$targetIndex = @{}
Get-ChildItem $targetRoot -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | ForEach-Object {
    $relative = $_.FullName.Substring($targetRoot.Length + 1).Replace('\', '/')
    if (-not $targetIndex.ContainsKey($_.Name)) { $targetIndex[$_.Name] = New-Object System.Collections.Generic.List[string] }
    $targetIndex[$_.Name].Add($relative)
}

function Resolve-TargetPath([string]$Path) {
    $mapped = Get-MappedPath $Path
    $name = Split-Path $Path -Leaf
    $candidates = $targetIndex[$name]

    if ($null -eq $candidates -or $candidates.Count -eq 0) { return [pscustomobject]@{ Path = $mapped; Exists = $false; Ambiguous = $false } }
    if ($candidates.Count -eq 1) { return [pscustomobject]@{ Path = $candidates[0]; Exists = $true; Ambiguous = $false } }
    if ($candidates -contains $mapped) { return [pscustomobject]@{ Path = $mapped; Exists = $true; Ambiguous = $false } }

    [pscustomobject]@{ Path = ($candidates -join ', '); Exists = $true; Ambiguous = $true }
}

function Merge-Text([string]$Ours, [string]$Base, [string]$Theirs) {
    $dir = Join-Path $env:TEMP 'substrate-sync'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $files = 'ours', 'base', 'theirs' | ForEach-Object { Join-Path $dir $_ }
    [IO.File]::WriteAllText($files[0], $Ours, $utf8NoBom)
    [IO.File]::WriteAllText($files[1], $Base, $utf8NoBom)
    [IO.File]::WriteAllText($files[2], $Theirs, $utf8NoBom)

    $result = Invoke-Git $targetRepo @('merge-file', '-p', '-L', $targetName, '-L', 'last sync', '-L', $sourceName, $files[0], $files[1], $files[2])
    if ($result.Code -lt 0) { throw "git merge-file failed: $($result.Error)" }

    [pscustomobject]@{ Text = [Text.Encoding]::UTF8.GetString($result.Bytes).Replace("`r`n", "`n"); Conflicts = $result.Code }
}

$diff = Get-GitText $sourceRepo @('diff', '--name-status', '-M', $base, $head, '--', $sourceMod)
$plan = New-Object System.Collections.Generic.List[object]

function Add-Plan($Action, $Source, $Target, $Note, $Write, $Remove) {
    $plan.Add([pscustomobject]@{ Action = $Action; Source = $Source; Target = $Target; Note = $Note; Write = $Write; Remove = $Remove })
}

foreach ($line in ($diff -split "`n" | Where-Object { $_ })) {
    $parts = $line -split "`t"
    $status = $parts[0].Substring(0, 1)
    $oldRepoPath = $parts[1]
    $newRepoPath = if ($parts.Count -gt 2) { $parts[2] } else { $parts[1] }

    $oldPath = $oldRepoPath.Substring($sourceMod.Length + 1)
    $newPath = $newRepoPath.Substring($sourceMod.Length + 1)

    if ($newPath.StartsWith('Localization/')) {
        Add-Plan 'MANUAL' $newPath '' 'localization: merge keys by hand' $null $null
        continue
    }

    if ((Test-Excluded $newPath $excludes) -or (Test-Excluded $oldPath $excludes)) {
        Add-Plan 'SKIP' $newPath '' 'excluded' $null $null
        continue
    }

    $baseBytes = if ($status -eq 'A') { $null } else { Get-Blob $sourceRepo $base $oldRepoPath }
    $headBytes = if ($status -eq 'D') { $null } else { Get-Blob $sourceRepo $head $newRepoPath }
    $isText = Test-Text $newPath

    if ($toTrelamium -and $isText) {
        $probe = if ($null -ne $headBytes) { $headBytes } else { $baseBytes }
        if ([Text.Encoding]::UTF8.GetString($probe) -match '\[SubstrateExclusive\]') {
            Add-Plan 'SKIP' $newPath '' 'Substrate-exclusive' $null $null
            continue
        }
    }

    $target = Resolve-TargetPath $oldPath
    if ($target.Ambiguous) {
        Add-Plan 'AMBIGUOUS' $newPath $target.Path 'several target files share this name' $null $null
        continue
    }

    if (-not $target.Exists -and -not $toTrelamium -and -not $IncludeNew -and $status -ne 'D') {
        Add-Plan 'SKIP' $newPath $target.Path 'not in Substrate (use -IncludeNew)' $null $null
        continue
    }

    # A rename lands at the mapped new path; the old target file is removed once the content is written.
    $writePath = if ($status -eq 'R') { (Resolve-TargetPath $newPath).Path } else { $target.Path }
    if ($status -eq 'R' -and (Resolve-TargetPath $newPath).Ambiguous) { $writePath = Get-MappedPath $newPath }
    $removeOld = if ($status -eq "R" -and $target.Exists -and $writePath -ne $target.Path) { $target.Path } else { $null }

    $targetFile = Join-Path $targetRoot $target.Path
    $oursBytes = if ($target.Exists -and (Test-Path -LiteralPath $targetFile)) { , [IO.File]::ReadAllBytes($targetFile) } else { $null }

    if ($status -eq 'D') {
        if ($null -eq $oursBytes) { Add-Plan 'SKIP' $newPath $target.Path 'already absent' $null $null; continue }

        $unchanged = if ($isText) {
            Test-SameText (ConvertFrom-Bytes $oursBytes).Text (Convert-SourceText (ConvertFrom-Bytes $baseBytes).Text)
        } else { Test-SameBytes $oursBytes $baseBytes }

        if ($unchanged -and $AllowDeletes) { Add-Plan 'DELETE' $newPath $target.Path '' $null $target.Path }
        elseif ($unchanged) { Add-Plan 'DELETED?' $newPath $target.Path "deleted in $sourceName; -AllowDeletes to remove" $null $null }
        else { Add-Plan 'CONFLICT' $newPath $target.Path "deleted in $sourceName, edited in $targetName" $null $null }
        continue
    }

    if (-not $isText) {
        if (Test-SameBytes $oursBytes $headBytes) {
            if ($removeOld) { Add-Plan 'MOVE' $newPath $writePath "from $removeOld" $oursBytes $removeOld }
            else { Add-Plan 'SAME' $newPath $writePath '' $null $null }
            continue
        }

        if ($null -eq $oursBytes) { Add-Plan 'ADD' $newPath $writePath 'binary' $headBytes $null }
        elseif (Test-SameBytes $oursBytes $baseBytes) { Add-Plan 'UPDATE' $newPath $writePath 'binary' $headBytes $removeOld }
        else { Add-Plan 'CONFLICT' $newPath $writePath "binary changed on both sides; $targetName kept" $null $null }
        continue
    }

    $theirsDoc = ConvertFrom-Bytes $headBytes
    $theirs = Convert-SourceText $theirsDoc.Text
    $baseText = if ($null -ne $baseBytes) { Convert-SourceText (ConvertFrom-Bytes $baseBytes).Text } else { $null }

    if ($null -eq $oursBytes) {
        Add-Plan 'ADD' $newPath $writePath '' (ConvertTo-Bytes $theirs $theirsDoc.Bom) $null
        continue
    }

    $oursDoc = ConvertFrom-Bytes $oursBytes

    if (Test-SameText $oursDoc.Text $theirs) {
        if ($removeOld) { Add-Plan 'MOVE' $newPath $writePath "from $removeOld" $oursBytes $removeOld }
        else { Add-Plan 'SAME' $newPath $writePath '' $null $null }
        continue
    }

    if (Test-SameText $oursDoc.Text $baseText) {
        Add-Plan 'UPDATE' $newPath $writePath '' (ConvertTo-Bytes $theirs $oursDoc.Bom) $removeOld
        continue
    }

    $mergeBase = if ($null -ne $baseText) { $baseText } else { '' }
    $merged = Merge-Text $oursDoc.Text $mergeBase $theirs
    $bytes = ConvertTo-Bytes $merged.Text $oursDoc.Bom

    if ($merged.Conflicts -eq 0 -and (Test-SameText $merged.Text $oursDoc.Text)) {
        if ($removeOld) { Add-Plan 'MOVE' $newPath $writePath "from $removeOld" $oursBytes $removeOld }
        else { Add-Plan 'SAME' $newPath $writePath "$sourceName's changes already present" $null $null }
        continue
    }

    if ($merged.Conflicts -gt 0) { Add-Plan 'CONFLICT' $newPath $writePath "$($merged.Conflicts) conflict(s), markers written" $bytes $null }
    else { Add-Plan 'MERGE' $newPath $writePath "kept $targetName-side edits" $bytes $removeOld }
}

"$Direction  $($base.Substring(0, 7))..$($head.Substring(0, 7))  $(if ($Apply) { 'APPLY' } else { 'dry run' })"
$plan | Sort-Object Action, Source | Format-Table Action, Source, Target, Note -AutoSize -Wrap | Out-String -Width 220

if ($ShowDiff) {
    # Relative paths under one folder keep the diff headers readable: current/<target> vs synced/<target>.
    $diffRoot = Join-Path $env:TEMP 'substrate-sync\diff'

    foreach ($item in $plan | Where-Object { $null -ne $_.Write -and (Test-Text $_.Target) }) {
        if (Test-Path $diffRoot) { Remove-Item $diffRoot -Recurse -Force }

        $before = Join-Path $diffRoot "current/$($item.Target)"
        $after = Join-Path $diffRoot "synced/$($item.Target)"
        New-Item -ItemType Directory -Force (Split-Path $before), (Split-Path $after) | Out-Null
        [IO.File]::WriteAllBytes($after, $item.Write)

        $current = Join-Path $targetRoot $item.Target
        if (Test-Path -LiteralPath $current) { Copy-Item -LiteralPath $current $before -Force } else { [IO.File]::WriteAllText($before, '') }

        $result = Invoke-Git $diffRoot @('diff', '--no-index', '--ignore-cr-at-eol', '--no-color', '--no-prefix', "current/$($item.Target)", "synced/$($item.Target)")
        "===== $($item.Action) $($item.Target)"
        [Text.Encoding]::UTF8.GetString($result.Bytes)
    }
}

if (-not $Apply) {
    'Dry run: nothing written. Re-run with -Apply to write these changes.'
    return
}

foreach ($item in $plan) {
    if ($null -ne $item.Write) {
        $path = Join-Path $targetRoot $item.Target
        New-Item -ItemType Directory -Force (Split-Path $path) | Out-Null
        [IO.File]::WriteAllBytes($path, $item.Write)
    }

    # DELETE removes its target; a rename removes the pre-rename file after the new one is written.
    if ($item.Remove -and ($item.Action -eq 'DELETE' -or $item.Remove -ne $item.Target)) {
        $old = Join-Path $targetRoot $item.Remove
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old }
    }
}

$config.lastSynced.$sourceKey = $head
Save-Config

$conflicts = @($plan | Where-Object { $_.Action -eq 'CONFLICT' })
"Recorded $sourceName $($head.Substring(0, 7)) as synced."
if ($conflicts.Count -gt 0) { "$($conflicts.Count) conflict(s): search the target for '<<<<<<< $targetName'." }
"Build $targetName before committing."
