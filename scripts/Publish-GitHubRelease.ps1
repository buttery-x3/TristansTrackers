[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^v\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [string]$Repository = 'buttery-x3/TristansTrackers',

    [string]$Runtime = 'win-x64',

    [switch]$Draft,

    [switch]$Prerelease
)

$ErrorActionPreference = 'Stop'

function Invoke-Checked {
    param(
        [Parameter(Mandatory)]
        [string]$Command,

        [Parameter(ValueFromRemainingArguments)]
        [string[]]$Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $Command $($Arguments -join ' ')"
    }
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$packageName = "TristansTrackers-$Version-$Runtime"
$publishDirectory = Join-Path $artifactsRoot $packageName
$zipPath = Join-Path $artifactsRoot "$packageName.zip"
$checksumPath = "$zipPath.sha256"
$releaseNotesPath = Join-Path $artifactsRoot "release-notes-$Version.md"

Push-Location $repositoryRoot
try {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
        throw 'GitHub CLI (gh) is required. Install it and run gh auth login.'
    }

    Invoke-Checked gh auth status

    $branch = (git branch --show-current).Trim()
    if ($branch -ne 'main') {
        throw "Releases must be published from main. Current branch: $branch"
    }

    git diff --quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Tracked working-tree changes must be committed before publishing a release.'
    }

    git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        throw 'Staged changes must be committed before publishing a release.'
    }

    Invoke-Checked git fetch origin main
    $localSha = (git rev-parse HEAD).Trim()
    $originSha = (git rev-parse origin/main).Trim()
    if ($localSha -ne $originSha) {
        throw "Local main ($localSha) and origin/main ($originSha) are not aligned."
    }

    $remoteTag = git ls-remote --tags origin "refs/tags/$Version"
    if ($remoteTag) {
        throw "Tag $Version already exists on origin."
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'SilentlyContinue'
        & gh release view $Version --repo $Repository 2>$null | Out-Null
        $releaseExists = $LASTEXITCODE -eq 0
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($releaseExists) {
        throw "GitHub release $Version already exists."
    }

    New-Item -ItemType Directory -Path $artifactsRoot -Force | Out-Null

    $resolvedArtifactsRoot = [IO.Path]::GetFullPath($artifactsRoot).TrimEnd('\')
    $resolvedPublishDirectory = [IO.Path]::GetFullPath($publishDirectory)
    if (-not $resolvedPublishDirectory.StartsWith("$resolvedArtifactsRoot\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean publish directory outside artifacts: $resolvedPublishDirectory"
    }

    if (Test-Path -LiteralPath $publishDirectory) {
        Remove-Item -LiteralPath $publishDirectory -Recurse -Force
    }

    foreach ($generatedFile in @($zipPath, $checksumPath, $releaseNotesPath)) {
        if (Test-Path -LiteralPath $generatedFile) {
            Remove-Item -LiteralPath $generatedFile -Force
        }
    }

    Invoke-Checked dotnet publish TristansTrackers.csproj `
        --configuration Release `
        --runtime $Runtime `
        --self-contained true `
        --output $publishDirectory `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:DebugType=None `
        -p:DebugSymbols=false

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'README.md') -Destination $publishDirectory
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE.txt') -Destination $publishDirectory

    Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath $checksumPath -Value "$hash  $([IO.Path]::GetFileName($zipPath))" -Encoding ascii

    $releaseNotes = @"
## About

TristansTrackers is a minimal, borderless, always-on-top Windows timer HUD. It provides a continuously filling one-second tracker, optional alarm timers, draggable positioning, and a lock control while remaining hidden from the taskbar and Alt+Tab.

## Alarm timers

- Choose 1 or 2 minutes, 5-minute increments through 60 minutes, 90 minutes, or 2 hours.
- The alarm bar fills from left to right and shows remaining whole minutes on hover.
- Replace or cancel an active alarm from the alarm menu.
- At expiry, Windows plays an alert and a large alarm-clock icon remains above the tracker until dismissed.

## Usage

1. Download and extract **$([IO.Path]::GetFileName($zipPath))**.
2. Run **TristansTrackers.exe**.
3. Drag the bar with the left mouse button to position it.
4. Hover over the bar to access the alarm and lock controls.
5. Right-click the tracker to open the Exit menu.

This **$Runtime** package is self-contained and does not require a separate .NET installation. Alarm state is not retained after the application exits.
"@
    Set-Content -LiteralPath $releaseNotesPath -Value $releaseNotes -Encoding utf8

    $releaseArguments = @(
        'release', 'create', $Version,
        $zipPath,
        $checksumPath,
        '--repo', $Repository,
        '--target', $localSha,
        '--title', "TristansTrackers $Version",
        '--notes-file', $releaseNotesPath
    )

    if ($Draft) {
        $releaseArguments += '--draft'
    }

    if ($Prerelease) {
        $releaseArguments += '--prerelease'
    }

    Invoke-Checked gh @releaseArguments
    Invoke-Checked gh release view $Version --repo $Repository --json url,tagName,name,isDraft,isPrerelease
}
finally {
    Pop-Location
}
