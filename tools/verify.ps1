<#
.SYNOPSIS
Runs format, strict Clippy, the locked build and every test against this checkout's source.

.DESCRIPTION
Every checkout of this repository shares the main checkout's prepared Cargo target, so
dependency artifacts are built once. Cargo names workspace artifacts by paths relative to the
workspace root and judges them fresh by timestamp, so it can reuse another worktree's newer
artifacts as if they were built from this checkout. Cargo's own lock also ends before test
executables run, so a later build can replace them mid-run.

This script holds a verification lock on the shared target while it compiles, sets this
checkout's crate sources to the current time so Cargo rebuilds every workspace crate from them,
and fails if Cargo reuses a workspace artifact that this run did not build. It copies every
executable as Cargo reports it, releases the lock, and runs the test copies, so other checkouts
can build while these tests run. Dependency artifacts, profiles, flags and the two-job cap are
unchanged.

Copies stay under out/verify in this checkout: build/ holds the product binaries for smoke and
capture runs, and test/ holds the test executables and the client they launch.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$checkout = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
# Cargo finds the workspace and .cargo/config.toml from the current directory.
Set-Location -LiteralPath $checkout
$crates = Join-Path $checkout 'crates'
$commonDir = git -C $checkout rev-parse --path-format=absolute --git-common-dir
if ($LASTEXITCODE -ne 0) { throw "Cannot find the main checkout of $checkout." }
$target = [IO.Path]::GetFullPath((Join-Path (Split-Path $commonDir -Parent) 'out\cargo-target'))
if (-not (Test-Path -LiteralPath $target -PathType Container)) {
    throw "The prepared Cargo target $target is missing. A cold build needs the notice AGENTS.md requires."
}
$env:CARGO_TARGET_DIR = $target

$snapshot = Join-Path $checkout 'out\verify'
if (Test-Path -LiteralPath $snapshot) { Remove-Item -LiteralPath $snapshot -Recurse -Force }
$null = New-Item -ItemType Directory -Path (Join-Path $snapshot 'build'), (Join-Path $snapshot 'test')

function Test-WorkspaceArtifact($Message) {
    $Message.manifest_path.StartsWith("$crates\", [StringComparison]::OrdinalIgnoreCase)
}

# Size and write time of every file this run's Cargo commands wrote. Cargo reports one file
# under its deps path in one command and under its uplifted hardlink in another, so the
# signature identifies the file rather than its path.
$built = [Collections.Generic.HashSet[string]]::new()

function Get-Signature([string]$File) {
    $item = Get-Item -LiteralPath $File
    "$($item.Length)@$($item.LastWriteTimeUtc.Ticks)"
}

function Assert-BuiltHere($Message) {
    foreach ($file in $Message.filenames) {
        if (-not $Message.fresh) {
            $null = $built.Add((Get-Signature $file))
        } elseif (-not $built.Contains((Get-Signature $file))) {
            throw "Cargo reused $file, which this verification did not build. " +
                "Another checkout built into $target without tools/verify.ps1; run this script again."
        }
    }
}

function Save-Executable($Message, [string]$Directory) {
    $source = $Message.executable
    $copy = Join-Path $Directory (Split-Path $source -Leaf)
    Copy-Item -LiteralPath $source -Destination $copy
    if (-not $built.Contains((Get-Signature $source))) {
        throw "$source changed while it was being copied; run this script again."
    }
    $copy
}

function Invoke-Cargo([string]$Command, [string[]]$Arguments, [scriptblock]$OnArtifact) {
    Write-Host "cargo $Command $Arguments"
    & cargo $Command --message-format=json-render-diagnostics @Arguments | ForEach-Object {
        if (-not $_.StartsWith('{')) { Write-Host $_; return }
        $message = $_ | ConvertFrom-Json
        if ($message.reason -eq 'compiler-artifact' -and (Test-WorkspaceArtifact $message)) {
            Assert-BuiltHere $message
            if ($OnArtifact) { & $OnArtifact $message }
        }
    }
    if ($LASTEXITCODE -ne 0) { throw "cargo $Command failed with exit code $LASTEXITCODE." }
}

cargo fmt --all -- --check
if ($LASTEXITCODE -ne 0) { throw 'cargo fmt --check failed.' }

$tests = [Collections.Generic.List[object]]::new()
$client = $null
$lockPath = Join-Path $target 'quarrel-verify.lock'
$lock = $null
while (-not $lock) {
    try {
        $lock = [IO.File]::Open($lockPath, 'OpenOrCreate', 'ReadWrite', 'None')
    } catch [IO.IOException] {
        Write-Host "Waiting for another checkout's verification to finish compiling..."
        Start-Sleep -Seconds 10
    }
}
try {
    $now = [DateTime]::UtcNow
    Get-ChildItem -LiteralPath $crates -Recurse -File | ForEach-Object { $_.LastWriteTimeUtc = $now }

    Invoke-Cargo clippy @('--workspace', '--all-targets', '--locked', '--', '-D', 'warnings')
    Invoke-Cargo build @('--workspace', '--locked') {
        param($message)
        if ($message.executable) { $null = Save-Executable $message (Join-Path $snapshot 'build') }
    }
    Invoke-Cargo test @('--workspace', '--locked', '--no-run') {
        param($message)
        if (-not $message.executable) { return }
        if ($message.profile.test) {
            $tests.Add([pscustomobject]@{
                Name = "$($message.target.name) ($(@($message.target.kind) -join ', '))"
                Executable = Save-Executable $message (Join-Path $snapshot 'test')
                Package = Split-Path $message.manifest_path -Parent
            })
        } elseif ($message.target.name -eq 'quarrel-client') {
            $script:client = Save-Executable $message (Join-Path $snapshot 'test')
        }
    }
    # Doctests read the workspace libraries from the shared target, so they run under the lock.
    Invoke-Cargo test @('--workspace', '--locked', '--doc')
} finally {
    $lock.Dispose()
}

if (-not $client) { throw 'cargo test --no-run did not report the quarrel-client executable.' }
$env:QUARREL_TEST_CLIENT = $client
$env:RUST_TEST_THREADS = '1'
$failed = @()
foreach ($test in $tests) {
    Write-Host "Running $($test.Name) from $($test.Executable)"
    $env:CARGO_MANIFEST_DIR = $test.Package
    Push-Location -LiteralPath $test.Package
    try { & $test.Executable } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { $failed += $test.Name }
}
if ($failed) { throw "Tests failed in: $($failed -join '; ')." }
Write-Host "Verified $checkout. Product binaries: $(Join-Path $snapshot 'build')"
