<#
.SYNOPSIS
Runs format, strict Clippy, the locked build and every test against this checkout's source.

.DESCRIPTION
Every checkout of this repository shares the main checkout's prepared Cargo target, so
dependency artifacts are built once. Cargo names workspace artifacts by paths relative to the
workspace root and judges them fresh by timestamp, so it can reuse another worktree's newer
artifacts as if they were built from this checkout. Cargo's own lock also ends before test
executables run, so a later build can replace them mid-run.

This script holds a verification lock on the shared target from its first compile to its last
test and fails if Cargo reuses a workspace artifact that it cannot attribute to this checkout. It
copies every executable as Cargo reports it and runs the test copies, so a raw Cargo build from
another checkout cannot replace them mid-run. Dependency artifacts, profiles and flags are
unchanged.

After compiling, it records in out/verify-record.json the size and write time of every workspace
artifact it built, and the content of everything they were built from: each file the compiler's
dependency info lists, including embedded assets, read before compiling; the manifests, lock file
and Cargo configuration; and the toolchain's version. It writes no record if any of them changed
while it compiled. The next run reuses those artifacts only when the record is this checkout's,
every artifact is still in the shared target as recorded, the record holds the content of every
file their dependency info lists, and no manifest, configuration or toolchain changed. It then
sets only the listed files whose content changed to the current time, so Cargo rebuilds just what
depends on them, and an unchanged checkout compiles and links no workspace crate. Otherwise,
including when the record is missing or unreadable, it sets every crate source to the current
time so Cargo rebuilds every workspace crate. Doctests are compiled on every run. The record is
deleted before compiling and written only after every build succeeds. A workspace build script
would have inputs the compiler's dependency info does not list, so a workspace with one gets no
record and rebuilds every crate on every run; this workspace has none.

The script limits how much it competes with the applications someone is using. It compiles at
BelowNormal priority, so compiling and linking yield the processor to them: Windows gives each
new process the BelowNormal class of the process that creates it, so Cargo, the compiler and the
linker all inherit it. The script takes back its caller's priority before running the tests, so
the test executables and the client they launch keep normal scheduling. Their wall-clock network
checks failed under a busy foreground at BelowNormal, and each uses at most about one core.

Linking one Bevy executable takes 5 to 7.5 GB of memory, and two at once exhausted a 32 GB
machine's free memory and paged its applications out to disk, so the build and test builds,
which link, run one Cargo job at a time. Clippy, which links no workspace executable, keeps the
configured two. Holding the lock through the tests keeps another checkout's link from running
beside them.

Copies stay under out/verify in this checkout: build/ holds the product binaries for smoke and
capture runs, and test/ holds the test executables and the client they launch.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$self = [Diagnostics.Process]::GetCurrentProcess()
$callerPriority = $self.PriorityClass
$self.PriorityClass = 'BelowNormal'
try {
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
    $recordPath = Join-Path $checkout 'out\verify-record.json'
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

    function Get-Hash([string]$File) {
        if (Test-Path -LiteralPath $File -PathType Leaf) { (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash } else { '' }
    }

    # Content of every file Git sees in this checkout, read before compiling. A file edited later
    # in the run fails Save-Record's check, so it is never recorded as the content its artifacts
    # were built from.
    function Get-CheckoutContent {
        $content = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
        $files = (git -C $checkout ls-files -z --cached --others --exclude-standard) -join "`n" -split "`0"
        if ($LASTEXITCODE -ne 0) { throw "Cannot list the files of $checkout." }
        foreach ($file in $files) {
            if (-not $file) { continue }
            $path = [IO.Path]::GetFullPath($file, $checkout)
            if (Test-Path -LiteralPath $path -PathType Leaf) { $content[$path] = Get-Hash $path }
        }
        $content
    }

    # Everything besides source content that decides what Cargo builds: the toolchain, every
    # manifest and the lock file, and each Cargo configuration file that applies here, including
    # the absence of the ones that do not exist.
    function Get-Settings($Content) {
        $settings = [Collections.Generic.SortedDictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
        $settings['rustc'] = (rustc -vV) -join ' '
        $settings['cargo'] = cargo -V
        foreach ($file in $Content.Keys) {
            if ((Split-Path $file -Leaf) -in 'Cargo.toml', 'Cargo.lock', 'rust-toolchain', 'rust-toolchain.toml') {
                $settings[$file] = Get-Hash $file
            }
        }
        $directory = $checkout
        $configs = while ($directory) {
            Join-Path $directory '.cargo'
            $directory = Split-Path $directory -Parent
        }
        $configs = @($configs) + $(if ($env:CARGO_HOME) { $env:CARGO_HOME } else { Join-Path $HOME '.cargo' })
        foreach ($config in $configs) {
            foreach ($name in 'config.toml', 'config') {
                $file = Join-Path $config $name
                $settings[$file] = Get-Hash $file
            }
        }
        ($settings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "`n"
    }

    # Every checkout file the compiler's dependency info lists for these artifacts, or nothing
    # when they have no dependency info. Cargo runs the compiler from the workspace root, so
    # relative paths are relative to this checkout.
    function Get-Sources([string[]]$Files) {
        $depInfo = foreach ($file in $Files) {
            $directory = Split-Path $file -Parent
            $stem = [IO.Path]::GetFileNameWithoutExtension($file)
            Join-Path $directory "$stem.d"
            if ($stem.StartsWith('lib')) { Join-Path $directory "$($stem.Substring(3)).d" }
        }
        $depInfo = @($depInfo | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -Unique)
        $found = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($line in $depInfo | ForEach-Object { Get-Content -LiteralPath $_ }) {
            $separator = $line.IndexOf(': ')
            if ($line.StartsWith('#') -or $separator -lt 0) { continue }
            foreach ($dependency in $line.Substring($separator + 2) -split '(?<!\\) ') {
                if (-not $dependency) { continue }
                $path = [IO.Path]::GetFullPath(($dependency -replace '\\ ', ' '), $checkout)
                if ($path.StartsWith("$checkout\", [StringComparison]::OrdinalIgnoreCase)) { $null = $found.Add($path) }
            }
        }
        $found
    }

    # Whether the previous run's record still accounts for this checkout's artifacts. Adds the
    # listed source files whose content changed to $Changed, and lets this run reuse every
    # recorded artifact.
    function Test-Record([string]$Settings, [Collections.Generic.List[string]]$Changed) {
        if (-not (Test-Path -LiteralPath $recordPath)) { Write-Host 'No verification record for this checkout.'; return $false }
        try {
            $record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json -AsHashtable
        } catch {
            Write-Host "The verification record is unreadable: $_"; return $false
        } finally {
            Remove-Item -LiteralPath $recordPath
        }
        try {
            if ($record.checkout -ne $checkout -or $record.target -ne $target) {
                Write-Host 'The verification record is from another checkout or target.'; return $false
            }
            if ($record.settings -ne $Settings) {
                Write-Host 'The toolchain, a manifest, the lock file or the Cargo configuration changed.'; return $false
            }
            foreach ($artifact in $record.artifacts.GetEnumerator()) {
                if (-not (Test-Path -LiteralPath $artifact.Key -PathType Leaf) -or (Get-Signature $artifact.Key) -ne $artifact.Value) {
                    Write-Host "$($artifact.Key) is no longer the artifact this checkout built."; return $false
                }
            }
            # The recorded artifacts' own dependency info names the inputs to check, so a record
            # that lost an entry cannot hide a changed input.
            $hashes = [Collections.Generic.Dictionary[string, string]]::new([StringComparer]::OrdinalIgnoreCase)
            foreach ($source in $record.sources.GetEnumerator()) { $hashes[$source.Key] = [string]$source.Value }
            $listed = @(Get-Sources @($record.artifacts.Keys))
            if (-not $listed) { Write-Host 'The recorded artifacts have no dependency info.'; return $false }
            foreach ($source in $listed) {
                if (-not $hashes.ContainsKey($source)) { Write-Host "The verification record lacks $source."; return $false }
                $hash = Get-Hash $source
                if (-not $hash) { Write-Host "$source no longer exists."; return $false }
                if ($hash -ne $hashes[$source]) { $Changed.Add($source) }
            }
        } catch {
            Write-Host "The verification record is invalid: $_"; return $false
        }
        foreach ($signature in $record.artifacts.Values) { $null = $built.Add($signature) }
        $true
    }

    # Writes the record unless an artifact is not this run's, an input has no content read before
    # compiling, or an input or setting changed after $Started, when compiling began. Cargo writes
    # a binary's dependency info after reporting the binary, so this reads it after Cargo exits.
    function Save-Record([string]$Settings, $Content, [DateTime]$Started) {
        $record = [ordered]@{ checkout = $checkout; target = $target; settings = $Settings; artifacts = @{}; sources = @{} }
        $sources = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($unit in $units) {
            $found = @(Get-Sources $unit)
            if (-not $found) { $script:unattributed = "$($unit[0]) has no dependency info" }
            foreach ($file in $found) { $null = $sources.Add($file) }
            foreach ($file in $unit) {
                $signature = Get-Signature $file
                if (-not $built.Contains($signature)) { $script:unattributed = "$file is not this run's artifact" }
                $record.artifacts[$file] = $signature
            }
        }
        foreach ($file in $sources) {
            if (-not $Content.ContainsKey($file)) {
                $script:unattributed = "$file was not in this checkout before compiling"
            } elseif ((Get-Hash $file) -ne $Content[$file] -or (Get-Item -LiteralPath $file).LastWriteTimeUtc -ge $Started) {
                $script:unattributed = "$file changed while compiling"
            } else {
                $record.sources[$file] = $Content[$file]
            }
        }
        if ((Get-Settings $Content) -ne $Settings) {
            $script:unattributed = 'the toolchain, a manifest, the lock file or the Cargo configuration changed while compiling'
        }
        if ($unattributed) {
            Write-Host "Not recording this run's artifacts for reuse, because $unattributed; the next run rebuilds every workspace crate."
        } else {
            $record | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $recordPath
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
        $compiled = 0
        $reused = 0
        & cargo $Command --message-format=json-render-diagnostics @Arguments | ForEach-Object {
            if (-not $_.StartsWith('{')) { Write-Host $_; return }
            $message = $_ | ConvertFrom-Json
            if ($message.reason -eq 'compiler-artifact' -and (Test-WorkspaceArtifact $message)) {
                Assert-BuiltHere $message
                if ($message.fresh) { $reused++ } else { $compiled++ }
                $units.Add([string[]]$message.filenames)
                if ($OnArtifact) { & $OnArtifact $message }
            }
        }
        if ($LASTEXITCODE -ne 0) { throw "cargo $Command failed with exit code $LASTEXITCODE." }
        Write-Host "cargo ${Command}: compiled $compiled and reused $reused workspace units."
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
            Write-Host "Waiting for another checkout's verification to finish..."
            Start-Sleep -Seconds 10
        }
    }
    try {
        $content = Get-CheckoutContent
        $settings = Get-Settings $content
        $changed = [Collections.Generic.List[string]]::new()
        $now = [DateTime]::UtcNow
        if (Test-Record $settings $changed) {
            Write-Host "Reusing this checkout's recorded workspace artifacts; $($changed.Count) source files changed."
            foreach ($file in $changed) { (Get-Item -LiteralPath $file).LastWriteTimeUtc = $now }
        } else {
            Write-Host 'Rebuilding every workspace crate.'
            Get-ChildItem -LiteralPath $crates -Recurse -File | ForEach-Object { $_.LastWriteTimeUtc = $now }
        }
        # The files Cargo reported for each workspace unit.
        $units = [Collections.Generic.List[string[]]]::new()
        $unattributed = $null
        $started = [DateTime]::UtcNow

        Invoke-Cargo clippy @('--workspace', '--all-targets', '--locked', '--', '-D', 'warnings')
        Invoke-Cargo build @('--workspace', '--locked', '--jobs', '1') {
            param($message)
            if ($message.executable) { $null = Save-Executable $message (Join-Path $snapshot 'build') }
        }
        Invoke-Cargo test @('--workspace', '--locked', '--jobs', '1', '--no-run') {
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
        Save-Record $settings $content $started
        Invoke-Cargo test @('--workspace', '--locked', '--doc')

        if (-not $client) { throw 'cargo test --no-run did not report the quarrel-client executable.' }
        $self.PriorityClass = $callerPriority
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
    } finally {
        $lock.Dispose()
    }
    Write-Host "Verified $checkout. Product binaries: $(Join-Path $snapshot 'build')"
} finally {
    $self.PriorityClass = $callerPriority
}
