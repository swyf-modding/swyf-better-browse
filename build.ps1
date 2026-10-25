<#
    Builds ScamWYF.BetterBrowse and installs it into the game's BepInEx/plugins.

    The shared library's build script does the work; this file only says what this mod is and
    which extra references it needs.

        ./build.ps1
        ./build.ps1 -NoCopy -Test
        ./build.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Scam With Your Friends"

    -Test runs the library's tests as well: the behaviour tests and the API surface check.

    Powershell 7 (pwsh) on Linux and macOS; either that or Windows PowerShell 5.1 on Windows.
#>
[CmdletBinding()]
param(
    [string]$GameDir,
    [string]$CscDll,
    # Stamp this version instead of reading the nearest tag.
    [string]$Version,
    [switch]$NoCopy,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'

# Some .NET SDK installs live in the user profile and are not on PATH. The shared build script finds
# that SDK's csc.dll by path but runs it with `dotnet`, so make sure the matching host is reachable.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    $roots = @((Join-Path $env:USERPROFILE '.dotnet'))
    if ($env:ProgramFiles) { $roots += (Join-Path $env:ProgramFiles 'dotnet') }

    foreach ($root in $roots) {
        foreach ($name in @('dotnet.exe', 'dotnet')) {
            if (-not (Test-Path (Join-Path $root $name))) { continue }
            $env:PATH = $root + [IO.Path]::PathSeparator + $env:PATH
            break
        }
        if (Get-Command dotnet -ErrorAction SilentlyContinue) { break }
    }
}

$lib = Join-Path $PSScriptRoot 'vendor/ScamWYF.Modding.Core/build.ps1'
if (-not (Test-Path $lib)) {
    throw "The shared library is missing. Run: git submodule update --init --recursive"
}

# The library finds the game install itself; find it here too so the extra references below point
# at the same place.
. (Join-Path $PSScriptRoot 'vendor/ScamWYF.Modding.Core/GameDir.ps1')
$GameDir = Resolve-GameDir -Explicit $GameDir -Require Managed
$managed = Join-Path $GameDir 'Scam With Your Friends_Data/Managed'

# The version comes from this repository's tags, not the library's.
. (Join-Path $PSScriptRoot 'vendor/ScamWYF.Modding.Core/Version.ps1')

$buildArgs = @{
    Project = 'ScamWYF.BetterBrowse'
    Sources = @((Join-Path $PSScriptRoot 'src'))
    OutDir  = (Join-Path $PSScriptRoot 'bin')
    BuildVersion = (Resolve-BuildVersion -Path $PSScriptRoot -Explicit $Version)
    Refs    = @(
        (Join-Path $managed 'Assembly-CSharp.dll')
    )
}

if ($GameDir)   { $buildArgs.GameDir = $GameDir }
if ($CscDll)    { $buildArgs.CscDll = $CscDll }
if ($NoCopy)    { $buildArgs.NoCopy = $true }
if ($Test)      { $buildArgs.Test = $true }

& $lib @buildArgs

if (-not $NoCopy) {
    Write-Host ""
    Write-Host "Launch the game and open the browser: searches now go to the engine in" -ForegroundColor Green
    Write-Host "  BepInEx/config/com.community.scamwyf.betterbrowse.cfg" -ForegroundColor Green
    Write-Host "or use the Search Engine tab in the shared F1 menu." -ForegroundColor Green
}
