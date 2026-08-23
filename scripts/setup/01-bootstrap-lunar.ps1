param(
    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $false)]
    [string] $SolutionName = "Lunar.AssetStudio",

    [Parameter(Mandatory = $false)]
    [string] $TargetFramework = "net10.0",

    [switch] $SkipFrontendInstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"


# ============================================================================
# Helpers
# ============================================================================

function Write-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Message
    )

    Write-Host
    Write-Host ("=" * 80)
    Write-Host $Message
    Write-Host ("=" * 80)
}


function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path $Path)) {
        New-Item `
            -ItemType Directory `
            -Path $Path `
            -Force `
            | Out-Null

        Write-Host "Created directory: $Path"
    }
}


function Ensure-GitKeep {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Directory
    )

    Ensure-Directory $Directory

    $gitKeepPath = Join-Path $Directory ".gitkeep"

    if (-not (Test-Path $gitKeepPath)) {
        New-Item `
            -ItemType File `
            -Path $gitKeepPath `
            | Out-Null
    }
}


function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $false)]
        [string[]] $Arguments = @(),

        [Parameter(Mandatory = $false)]
        [string] $WorkingDirectory = $RepositoryRoot
    )

    Push-Location $WorkingDirectory

    try {
        Write-Host
        Write-Host "> $FilePath $($Arguments -join ' ')"

        $output = & $FilePath @Arguments 2>&1
        $exitCode = $LASTEXITCODE

        foreach ($line in $output) {
            Write-Host $line
        }

        if ($exitCode -ne 0) {
            throw (
                "Command failed with exit code ${exitCode}: " +
                "$FilePath $($Arguments -join ' ')"
            )
        }
    }
    finally {
        Pop-Location
    }
}


function Ensure-GitIgnoreEntry {
    param(
        [Parameter(Mandatory = $true)]
        [string] $GitIgnorePath,

        [Parameter(Mandatory = $true)]
        [string] $Entry
    )

    if (-not (Test-Path $GitIgnorePath)) {
        New-Item `
            -ItemType File `
            -Path $GitIgnorePath `
            | Out-Null
    }

    $existingLines = @(
        Get-Content `
            -Path $GitIgnorePath `
            -ErrorAction SilentlyContinue
    )

    if ($existingLines -notcontains $Entry) {
        Add-Content `
            -Path $GitIgnorePath `
            -Value $Entry
    }
}


function Ensure-DotNetProject {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Template,

        [Parameter(Mandatory = $true)]
        [string] $ProjectName,

        [Parameter(Mandatory = $true)]
        [string] $ProjectPath
    )

    $projectFile = Join-Path `
        $ProjectPath `
        "$ProjectName.csproj"

    if (Test-Path $projectFile) {
        Write-Host "Project already exists: $projectFile"
        return $false
    }

    Ensure-Directory (
        Split-Path `
            -Parent `
            $ProjectPath
    )

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @(
            "new",
            $Template,
            "--name",
            $ProjectName,
            "--output",
            $ProjectPath,
            "--framework",
            $TargetFramework
        )

    return $true
}


function Test-DirectoryEmpty {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path $Path)) {
        return $true
    }

    $items = @(
        Get-ChildItem `
            -Path $Path `
            -Force
    )

    return $items.Count -eq 0
}


# ============================================================================
# Resolve repository
# ============================================================================

$RepositoryRoot = [System.IO.Path]::GetFullPath(
    $RepositoryRoot
)

Ensure-Directory $RepositoryRoot


Write-Step "LUNAR ASSET STUDIO - REPOSITORY BOOTSTRAP"

Write-Host "Repository:       $RepositoryRoot"
Write-Host "Solution:         $SolutionName"
Write-Host "Target framework: $TargetFramework"


# ============================================================================
# Repository topology
# ============================================================================

$backendPath = Join-Path `
    $RepositoryRoot `
    "backend"

$backendSourcePath = Join-Path `
    $backendPath `
    "src"

$backendTestsPath = Join-Path `
    $backendPath `
    "tests"

$frontendPath = Join-Path `
    $RepositoryRoot `
    "frontend"

$workersPath = Join-Path `
    $RepositoryRoot `
    "workers"

$configPath = Join-Path `
    $RepositoryRoot `
    "config"

$artifactsPath = Join-Path `
    $RepositoryRoot `
    "artifacts"

$docsPath = Join-Path `
    $RepositoryRoot `
    "docs"

$scriptsPath = Join-Path `
    $RepositoryRoot `
    "scripts"


# ============================================================================
# Project topology
# ============================================================================

$apiPath = Join-Path `
    $backendSourcePath `
    "Lunar.Api"

$corePath = Join-Path `
    $backendSourcePath `
    "Lunar.Core"

$infrastructurePath = Join-Path `
    $backendSourcePath `
    "Lunar.Infrastructure"

$testsProjectPath = Join-Path `
    $backendTestsPath `
    "Lunar.Tests"


# ============================================================================
# 1 - Prerequisites
# ============================================================================

Write-Step "1/10 - Checking prerequisites"


$dotnetCommand = Get-Command `
    "dotnet" `
    -ErrorAction SilentlyContinue

if ($null -eq $dotnetCommand) {
    throw ".NET SDK was not found in PATH."
}


$nodeCommand = Get-Command `
    "node" `
    -ErrorAction SilentlyContinue

if ($null -eq $nodeCommand) {
    throw "Node.js was not found in PATH."
}


$npmCommand = Get-Command `
    "npm" `
    -ErrorAction SilentlyContinue

if ($null -eq $npmCommand) {
    throw "npm was not found in PATH."
}


$dotnetVersion = (
    & dotnet --version
).Trim()

$dotnetMajorVersion = (
    $dotnetVersion.Split(".")[0]
)


if ($dotnetMajorVersion -ne "10") {
    throw (
        "Lunar requires .NET 10 for the current baseline. " +
        "Detected SDK: $dotnetVersion"
    )
}


$nodeVersion = (
    & node --version
).Trim()

$npmVersion = (
    & npm --version
).Trim()


Write-Host ".NET SDK: $dotnetVersion"
Write-Host "Node:     $nodeVersion"
Write-Host "npm:      $npmVersion"


# ============================================================================
# 2 - Base directories
# ============================================================================

Write-Step "2/10 - Ensuring repository directories"


Ensure-Directory $backendPath
Ensure-Directory $backendSourcePath
Ensure-Directory $backendTestsPath
Ensure-Directory $frontendPath
Ensure-Directory $workersPath
Ensure-Directory $configPath
Ensure-Directory $artifactsPath
Ensure-Directory $docsPath
Ensure-Directory $scriptsPath


# ============================================================================
# 3 - Pin SDK
# ============================================================================

Write-Step "3/10 - Pinning .NET SDK"


$globalJsonPath = Join-Path `
    $RepositoryRoot `
    "global.json"


if (-not (Test-Path $globalJsonPath)) {

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @(
            "new",
            "globaljson",
            "--sdk-version",
            $dotnetVersion
        )
}
else {
    Write-Host "global.json already exists. Leaving it untouched."
}


# ============================================================================
# 4 - Solution
# ============================================================================

Write-Step "4/10 - Creating solution"


$solutionSlnxPath = Join-Path `
    $RepositoryRoot `
    "$SolutionName.slnx"

$solutionSlnPath = Join-Path `
    $RepositoryRoot `
    "$SolutionName.sln"


if (
    -not (Test-Path $solutionSlnxPath) -and
    -not (Test-Path $solutionSlnPath)
) {

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @(
            "new",
            "sln",
            "--name",
            $SolutionName
        )
}


if (Test-Path $solutionSlnxPath) {
    $solutionPath = $solutionSlnxPath
}
elseif (Test-Path $solutionSlnPath) {
    $solutionPath = $solutionSlnPath
}
else {
    throw "Solution file could not be found after creation."
}


Write-Host "Solution: $solutionPath"


# ============================================================================
# 5 - .NET projects
# ============================================================================

Write-Step "5/10 - Creating .NET projects"


$apiCreated = Ensure-DotNetProject `
    -Template "web" `
    -ProjectName "Lunar.Api" `
    -ProjectPath $apiPath


$coreCreated = Ensure-DotNetProject `
    -Template "classlib" `
    -ProjectName "Lunar.Core" `
    -ProjectPath $corePath


$infrastructureCreated = Ensure-DotNetProject `
    -Template "classlib" `
    -ProjectName "Lunar.Infrastructure" `
    -ProjectPath $infrastructurePath


$testsCreated = Ensure-DotNetProject `
    -Template "xunit" `
    -ProjectName "Lunar.Tests" `
    -ProjectPath $testsProjectPath


# Remove template placeholders ONLY when this execution created them.

if ($coreCreated) {

    $placeholderPath = Join-Path `
        $corePath `
        "Class1.cs"

    if (Test-Path $placeholderPath) {
        Remove-Item $placeholderPath
    }
}


if ($infrastructureCreated) {

    $placeholderPath = Join-Path `
        $infrastructurePath `
        "Class1.cs"

    if (Test-Path $placeholderPath) {
        Remove-Item $placeholderPath
    }
}


if ($testsCreated) {

    $placeholderPath = Join-Path `
        $testsProjectPath `
        "UnitTest1.cs"

    if (Test-Path $placeholderPath) {
        Remove-Item $placeholderPath
    }
}


# ============================================================================
# Project paths
# ============================================================================

$apiProject = Join-Path `
    $apiPath `
    "Lunar.Api.csproj"

$coreProject = Join-Path `
    $corePath `
    "Lunar.Core.csproj"

$infrastructureProject = Join-Path `
    $infrastructurePath `
    "Lunar.Infrastructure.csproj"

$testsProject = Join-Path `
    $testsProjectPath `
    "Lunar.Tests.csproj"


# ============================================================================
# 6 - Solution membership
# ============================================================================

Write-Step "6/10 - Adding projects to solution"


$projects = @(
    $apiProject,
    $coreProject,
    $infrastructureProject,
    $testsProject
)


foreach ($project in $projects) {

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @(
            "sln",
            $solutionPath,
            "add",
            $project
        )
}


# ============================================================================
# 7 - Architectural references
# ============================================================================

Write-Step "7/10 - Creating architectural references"


# ---------------------------------------------------------------------------
# Dependency direction:
#
# Lunar.Api
#    ├── Lunar.Core
#    └── Lunar.Infrastructure
#
# Lunar.Infrastructure
#    └── Lunar.Core
#
# Lunar.Core
#    └── NOTHING
#
# Core never references Infrastructure.
# ---------------------------------------------------------------------------


Invoke-Checked `
    -FilePath "dotnet" `
    -Arguments @(
        "add",
        $apiProject,
        "reference",
        $coreProject
    )


Invoke-Checked `
    -FilePath "dotnet" `
    -Arguments @(
        "add",
        $apiProject,
        "reference",
        $infrastructureProject
    )


Invoke-Checked `
    -FilePath "dotnet" `
    -Arguments @(
        "add",
        $infrastructureProject,
        "reference",
        $coreProject
    )


Invoke-Checked `
    -FilePath "dotnet" `
    -Arguments @(
        "add",
        $testsProject,
        "reference",
        $coreProject
    )


Invoke-Checked `
    -FilePath "dotnet" `
    -Arguments @(
        "add",
        $testsProject,
        "reference",
        $infrastructureProject
    )


# ============================================================================
# 8 - Source structure
# ============================================================================

Write-Step "8/10 - Creating source structure"


# ---------------------------------------------------------------------------
# Lunar.Core
# ---------------------------------------------------------------------------

$coreDirectories = @(
    "Assets",
    "Artifacts",
    "Capabilities",
    "Workflows",
    "Workers"
)


foreach ($directory in $coreDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $corePath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Lunar.Api
# ---------------------------------------------------------------------------

$apiDirectories = @(
    "Features",
    "Realtime"
)


foreach ($directory in $apiDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $apiPath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Lunar.Infrastructure
# ---------------------------------------------------------------------------

$infrastructureDirectories = @(
    "Persistence",
    "Workers",
    "Providers",
    "FileSystem",
    "Hardware"
)


foreach ($directory in $infrastructureDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $infrastructurePath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

$testDirectories = @(
    "Unit",
    "Integration"
)


foreach ($directory in $testDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $testsProjectPath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Worker runtime
# ---------------------------------------------------------------------------

$workerDirectories = @(
    "providers",
    "blender",
    "contracts"
)


foreach ($directory in $workerDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $workersPath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Runtime configuration
# ---------------------------------------------------------------------------

$configDirectories = @(
    "providers",
    "workflows",
    "prompts",
    "profiles"
)


foreach ($directory in $configDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $configPath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Generated artifacts
# ---------------------------------------------------------------------------

Ensure-GitKeep $artifactsPath


# ---------------------------------------------------------------------------
# Documentation
# ---------------------------------------------------------------------------

$documentationDirectories = @(
    "architecture",
    "decisions"
)


foreach ($directory in $documentationDirectories) {

    Ensure-GitKeep (
        Join-Path `
            $docsPath `
            $directory
    )
}


# ---------------------------------------------------------------------------
# Scripts
# ---------------------------------------------------------------------------

Ensure-Directory (
    Join-Path `
        $scriptsPath `
        "setup"
)


# ============================================================================
# 9 - React frontend
# ============================================================================

Write-Step "9/10 - Creating React frontend"


$frontendPackageJsonPath = Join-Path `
    $frontendPath `
    "package.json"


if (-not (Test-Path $frontendPackageJsonPath)) {

    $frontendIsEmpty = Test-DirectoryEmpty `
        -Path $frontendPath


    if (-not $frontendIsEmpty) {

        throw (
            "The frontend directory contains files but no package.json. " +
            "Bootstrap will not overwrite or modify an unknown frontend. " +
            "Directory: $frontendPath"
        )
    }


    Invoke-Checked `
        -FilePath "npm" `
        -Arguments @(
            "create",
            "vite@latest",
            ".",
            "--",
            "--template",
            "react-ts"
        ) `
        -WorkingDirectory $frontendPath

}
else {

    Write-Host (
        "React project already exists: " +
        $frontendPath
    )
}


if (-not $SkipFrontendInstall) {

    Invoke-Checked `
        -FilePath "npm" `
        -Arguments @(
            "install"
        ) `
        -WorkingDirectory $frontendPath

}
else {

    Write-Host "Skipping frontend dependency installation."
}


# ============================================================================
# Git ignore
# ============================================================================

$gitIgnorePath = Join-Path `
    $RepositoryRoot `
    ".gitignore"


if (-not (Test-Path $gitIgnorePath)) {

    Invoke-Checked `
        -FilePath "dotnet" `
        -Arguments @(
            "new",
            "gitignore"
        )
}


$gitIgnoreEntries = @(
    "# Lunar generated artifacts",
    "/artifacts/*",
    "!/artifacts/.gitkeep",

    "# Python worker environments",
    "**/.venv/",
    "**/__pycache__/",
    "*.pyc",

    "# Frontend",
    "**/node_modules/",
    "**/dist/"
)


foreach ($entry in $gitIgnoreEntries) {

    Ensure-GitIgnoreEntry `
        -GitIgnorePath $gitIgnorePath `
        -Entry $entry
}


# ============================================================================
# 10 - Validation
# ============================================================================

Write-Step "10/10 - Validating repository"


Invoke-Checked `
    -FilePath "dotnet" `
    -Arguments @(
        "build",
        $solutionPath
    )


if (-not $SkipFrontendInstall) {

    Invoke-Checked `
        -FilePath "npm" `
        -Arguments @(
            "run",
            "build"
        ) `
        -WorkingDirectory $frontendPath
}


# ============================================================================
# Result
# ============================================================================

Write-Host
Write-Host ("=" * 80)
Write-Host "LUNAR BOOTSTRAP COMPLETE"
Write-Host ("=" * 80)


Write-Host
Write-Host "Repository"
Write-Host "  $RepositoryRoot"


Write-Host
Write-Host "Solution"
Write-Host "  $solutionPath"


Write-Host
Write-Host "Backend"
Write-Host "  $backendPath"


Write-Host
Write-Host "Projects"
Write-Host "  $apiPath"
Write-Host "  $corePath"
Write-Host "  $infrastructurePath"


Write-Host
Write-Host "Tests"
Write-Host "  $testsProjectPath"


Write-Host
Write-Host "Frontend"
Write-Host "  $frontendPath"


Write-Host
Write-Host "Workers"
Write-Host "  $workersPath"


Write-Host
Write-Host "Configuration"
Write-Host "  $configPath"


Write-Host
Write-Host "Artifacts"
Write-Host "  $artifactsPath"


Write-Host
Write-Host "Documentation"
Write-Host "  $docsPath"


Write-Host
Write-Host ("=" * 80)
Write-Host "BOOTSTRAP VALIDATION: PASS"
Write-Host ("=" * 80)