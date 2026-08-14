param(
    [Parameter(Mandatory = $true)]
    [string]$EventName,
    [Parameter(Mandatory = $true)]
    [string]$EventPath,
    [string]$TrustedRoot = 'b9073b6a9c110b5fbca5e242d49bd03a8cecef12',
    [string]$Repository = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($Repository)
$eventScript = Join-Path $PSScriptRoot 'check-golden-event.ps1'
$trustedRoot = $TrustedRoot
$zeroSha = '0000000000000000000000000000000000000000'

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    $result = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed:`n$($result -join [Environment]::NewLine)" }
    return @($result)
}

function Fetch-ExactCommit([string]$Revision, [string]$Name) {
    $fetch = & git fetch --no-tags origin $Revision 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Could not fetch exact $Name revision $Revision`: $($fetch -join [Environment]::NewLine)" }
    $resolved = (@(Invoke-Git rev-parse --verify "$Revision`^{commit}"))[0].Trim()
    if ($resolved.Length -ne 40) { throw "Fetched $Name revision does not peel to a commit." }
    return $resolved
}

function Merge-Base([string]$Left, [string]$Right) {
    $result = & git merge-base $Left $Right 2>&1
    if ($LASTEXITCODE -ne 0 -or @($result).Count -ne 1) { throw "No unique merge base exists between $Left and $Right." }
    return @($result)[0].Trim()
}

Push-Location $repository
try {
    $event = Get-Content -Raw -LiteralPath $EventPath | ConvertFrom-Json
    Invoke-Git fetch --force --prune origin '+refs/heads/*:refs/remotes/origin/*' '+refs/tags/*:refs/tags/*' | Out-Null
    $root = Fetch-ExactCommit $trustedRoot 'trusted-root'
    if ($root -cne $trustedRoot) { throw 'Trusted inception resolved unexpectedly.' }

    if ($EventName -ceq 'pull_request') {
        $base = Fetch-ExactCommit ([string]$event.pull_request.base.sha) 'pull-request-base'
        $head = Fetch-ExactCommit ([string]$event.pull_request.head.sha) 'pull-request-head'
        $historyBase = Merge-Base $base $head
        & $eventScript -HistoryBase $historyBase -Established $base -Candidate $head -ProspectiveMerge -TrustedRoot $root -Repository $repository
        if ($LASTEXITCODE -ne 0) { throw 'Pull-request golden event check failed.' }
        exit 0
    }

    if ($EventName -cne 'push') { throw "Unsupported CI event `$EventName`." }
    if ([bool]$event.deleted -or [string]$event.after -ceq $zeroSha) {
        Write-Output 'golden event skipped: deleted ref has no candidate commit'
        exit 0
    }

    $afterValue = [string]$event.after
    if ([string]$event.ref -like 'refs/tags/*') {
        if ([string]$event.before -cne $zeroSha) { throw 'In-place tag updates are not permitted by replay history policy.' }
        $peel = & git rev-parse --verify "$afterValue`^{commit}" 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Output 'golden event skipped: tag target does not peel to a commit'
            exit 0
        }
        $candidate = Fetch-ExactCommit $afterValue 'tag-candidate'
        $defaultRef = "refs/remotes/origin/$([string]$event.repository.default_branch)"
        $defaultHead = (@(Invoke-Git rev-parse --verify "$defaultRef`^{commit}"))[0].Trim()
        & git merge-base --is-ancestor $candidate $defaultHead
        if ($LASTEXITCODE -ne 0) { throw 'New tag candidate is not contained in default-branch history.' }
        & $eventScript -HistoryBase $root -Established $root -Candidate $candidate -TrustedRoot $root -Repository $repository
        if ($LASTEXITCODE -ne 0) { throw 'Tag golden event check failed.' }
        exit 0
    }

    if ([string]$event.ref -notlike 'refs/heads/*') { throw "Unsupported push ref `$([string]$event.ref)`." }
    $candidateHead = Fetch-ExactCommit $afterValue 'branch-candidate'
    if ([string]$event.before -eq $zeroSha) {
        $defaultRef = "refs/remotes/origin/$([string]$event.repository.default_branch)"
        $defaultHead = (@(Invoke-Git rev-parse --verify "$defaultRef`^{commit}"))[0].Trim()
        $historyBase = Merge-Base $defaultHead $candidateHead
        & $eventScript -HistoryBase $historyBase -Established $defaultHead -Candidate $candidateHead -ProspectiveMerge -TrustedRoot $root -Repository $repository
        if ($LASTEXITCODE -ne 0) { throw 'New-branch golden event check failed.' }
        exit 0
    }

    $before = Fetch-ExactCommit ([string]$event.before) 'branch-before'
    & git merge-base --is-ancestor $before $candidateHead
    if ($LASTEXITCODE -ne 0) { throw 'Non-fast-forward branch updates are not permitted by replay history policy.' }
    & $eventScript -HistoryBase $before -Established $before -Candidate $candidateHead -TrustedRoot $root -Repository $repository
    if ($LASTEXITCODE -ne 0) { throw 'Branch golden event check failed.' }
} finally {
    Pop-Location
}
