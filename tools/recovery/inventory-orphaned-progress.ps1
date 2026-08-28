[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$artifactRelativePaths = @(
    '.ivy/worktrees/009-projectile-cards',
    '.ivy/worktrees/010-volley-projectiles',
    '.ivy/worktrees/011-radial-saw-maps',
    '.ivy/worktrees/013-dynamic-arena',
    '.ivy/worktrees/014-content-roadmap',
    '.ivy/worktrees/015-projectile-damage-scale',
    '.ivy/worktrees/022-controller-support',
    '.ivy/worktrees/029-passive-auras'
)

$allowlistedDirectoryPaths = @(
    '.github',
    'docs',
    'game',
    'reels',
    'replays',
    'research/notes',
    'spec',
    'src',
    'tools'
)

$allowlistedTopLevelFiles = @(
    '.gitattributes',
    '.gitignore',
    'AGENTS.md',
    'Directory.Build.props',
    'global.json',
    'GOAL.md',
    'README.md',
    'Rounds.sln'
)

$excludedSegmentNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase
)
foreach ($name in @('.git', '.git-index', '.tools', '.tmp', '.godot', 'bin', 'obj')) {
    [void] $excludedSegmentNames.Add($name)
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Test-IsReparsePoint {
    param([Parameter(Mandatory = $true)] [System.IO.FileSystemInfo] $Item)

    return ($Item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
}

function Get-AllowlistedFiles {
    param([Parameter(Mandatory = $true)] [string] $ArtifactRoot)

    $files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()

    function Add-DirectoryFiles {
        param([Parameter(Mandatory = $true)] [System.IO.DirectoryInfo] $Directory)

        if (Test-IsReparsePoint -Item $Directory) {
            return
        }

        foreach ($item in Get-ChildItem -LiteralPath $Directory.FullName -Force) {
            if (Test-IsReparsePoint -Item $item) {
                continue
            }

            if ($item.PSIsContainer) {
                if (-not $excludedSegmentNames.Contains($item.Name)) {
                    Add-DirectoryFiles -Directory $item
                }
                continue
            }

            if ($item -is [System.IO.FileInfo] -and -not $excludedSegmentNames.Contains($item.Name)) {
                $files.Add($item)
            }
        }
    }

    foreach ($relativeDirectoryPath in $allowlistedDirectoryPaths) {
        $nativeRelativePath = $relativeDirectoryPath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $directoryPath = [System.IO.Path]::Combine($ArtifactRoot, $nativeRelativePath)
        if (-not [System.IO.Directory]::Exists($directoryPath)) {
            continue
        }

        $directory = [System.IO.DirectoryInfo]::new($directoryPath)
        Add-DirectoryFiles -Directory $directory
    }

    foreach ($relativeFilePath in $allowlistedTopLevelFiles) {
        $filePath = [System.IO.Path]::Combine($ArtifactRoot, $relativeFilePath)
        if (-not [System.IO.File]::Exists($filePath)) {
            continue
        }

        $file = [System.IO.FileInfo]::new($filePath)
        if (-not (Test-IsReparsePoint -Item $file)) {
            $files.Add($file)
        }
    }

    return $files
}

function Get-FileSha256 {
    param([Parameter(Mandatory = $true)] [string] $Path)

    $stream = [System.IO.File]::Open(
        $Path,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::ReadWrite -bor [System.IO.FileShare]::Delete
    )
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.Convert]::ToHexString($sha256.ComputeHash($stream)).ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

function Get-BytesSha256 {
    param([Parameter(Mandatory = $true)] [byte[]] $Bytes)

    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        return [System.Convert]::ToHexString($sha256.ComputeHash($Bytes)).ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
    }
}

$repositoryPath = [System.IO.Path]::GetFullPath($RepositoryRoot)
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
[void] [System.IO.Directory]::CreateDirectory($outputPath)

foreach ($artifactRelativePath in $artifactRelativePaths) {
    $nativeArtifactRelativePath = $artifactRelativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $artifactPath = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine($repositoryPath, $nativeArtifactRelativePath)
    )
    $artifactDirectory = [System.IO.DirectoryInfo]::new($artifactPath)
    if (-not $artifactDirectory.Exists) {
        throw "Frozen artifact path is missing: $artifactRelativePath"
    }
    if (Test-IsReparsePoint -Item $artifactDirectory) {
        throw "Frozen artifact root is a reparse point: $artifactRelativePath"
    }

    $files = @(Get-AllowlistedFiles -ArtifactRoot $artifactPath)
    [string[]] $relativePaths = @(
        foreach ($file in $files) {
            [System.IO.Path]::GetRelativePath($artifactPath, $file.FullName).Replace('\', '/')
        }
    )
    [string[]] $sortKeys = @(
        foreach ($relativePath in $relativePaths) {
            [System.Convert]::ToHexString($utf8NoBom.GetBytes($relativePath)).ToLowerInvariant()
        }
    )
    [System.Array]::Sort($sortKeys, $relativePaths, [System.StringComparer]::Ordinal)

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($relativePath in $relativePaths) {
        $nativeRelativePath = $relativePath.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $filePath = [System.IO.Path]::Combine($artifactPath, $nativeRelativePath)
        $file = [System.IO.FileInfo]::new($filePath)
        $length = $file.Length.ToString([System.Globalization.CultureInfo]::InvariantCulture)
        $hash = Get-FileSha256 -Path $filePath
        $lines.Add("$relativePath`t$length`t$hash")
    }

    $manifestText = [System.String]::Join("`n", $lines) + "`n"
    $manifestBytes = $utf8NoBom.GetBytes($manifestText)
    $artifactName = [System.IO.Path]::GetFileName($artifactPath)
    $manifestPath = [System.IO.Path]::Combine($outputPath, "$artifactName.manifest.tsv")
    [System.IO.File]::WriteAllBytes($manifestPath, $manifestBytes)

    $artifactDigest = Get-BytesSha256 -Bytes $manifestBytes
    Write-Output "$artifactRelativePath`t$($relativePaths.Count)`t$artifactDigest"
}
