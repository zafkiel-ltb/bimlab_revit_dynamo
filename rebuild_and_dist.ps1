param(
    [string]$ReleaseDate = (Get-Date -Format "dd-MM-yyyy")
)

$ErrorActionPreference = "Stop"
$base = $PSScriptRoot

if ($ReleaseDate -notmatch '^\d{2}-\d{2}-\d{4}$') {
    throw "ReleaseDate must use dd-MM-yyyy format."
}

function Assert-AllowedEntries {
    param(
        [string]$Path,
        [string[]]$AllowedNames
    )

    $entries = @(Get-ChildItem -LiteralPath $Path -Force)
    $unexpected = @($entries | Where-Object {
        $AllowedNames -notcontains $_.Name
    })
    $missing = @($AllowedNames | Where-Object {
        -not (Test-Path -LiteralPath (Join-Path $Path $_))
    })

    if ($unexpected.Count -gt 0 -or $missing.Count -gt 0) {
        $details = @($unexpected.FullName) + @($missing | ForEach-Object { "MISSING: $_" })
        throw "Unexpected dist contents in $Path`n$($details -join [Environment]::NewLine)"
    }
}

function Assert-NoForbiddenFiles {
    param([string]$Path)

    $forbidden = @(Get-ChildItem -LiteralPath $Path -Recurse -Force | Where-Object {
        if ($_.PSIsContainer) {
            $_.Name -match '^(bin|obj)$'
        } else {
            $_.Name -match '(?i)\.(dll|json|pdb|xml)$'
        }
    })

    if ($forbidden.Count -gt 0) {
        throw "Forbidden files found in dist:`n$($forbidden.FullName -join [Environment]::NewLine)"
    }
}

function Assert-CleanArchive {
    param([string]$Path)

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $forbidden = @($archive.Entries | Where-Object {
            $parts = @($_.FullName -split '[/\\]' | Where-Object { $_ })
            $leaf = if ($parts.Count -gt 0) { $parts[-1] } else { "" }
            $leaf -match '(?i)\.(dll|json|pdb|xml)$' -or
                $parts -contains 'bin' -or $parts -contains 'obj'
        })

        if ($forbidden.Count -gt 0) {
            throw "Forbidden files found in archive $Path`n$($forbidden.FullName -join [Environment]::NewLine)"
        }
    } finally {
        $archive.Dispose()
    }
}

Write-Host "=== 1. Build DynLock.Core ===" -ForegroundColor Cyan
$coreProject = Join-Path $base "src/DynLock.Core/DynLock.Core.csproj"
dotnet build $coreProject -c Release `
    --source https://api.nuget.org/v3/index.json `
    /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "DynLock.Core build failed." }

Write-Host ""
Write-Host "=== 2. Build DynLock.Addin (net48 + net8.0-windows) ===" -ForegroundColor Cyan
$addinProject = Join-Path $base "src/DynLock.Addin/DynLock.Addin.csproj"
dotnet build $addinProject -c Release `
    --source https://api.nuget.org/v3/index.json `
    /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "DynLock.Addin build failed." }

Write-Host ""
Write-Host "=== 3. Build DynLock.EncryptorGui (BIMLab Studio) ===" -ForegroundColor Cyan
$studioProject = Join-Path $base "src/DynLock.EncryptorGui/DynLock.EncryptorGui.csproj"
dotnet build $studioProject -c Release `
    --source https://api.nuget.org/v3/index.json `
    /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "DynLock.EncryptorGui build failed." }

Write-Host ""
Write-Host "=== 4. Build DynLock.Installer (BIMLab Player - embeds Addin) ===" -ForegroundColor Cyan
$installerProject = Join-Path $base "src/DynLock.Installer/DynLock.Installer.csproj"
dotnet build $installerProject -c Release `
    --source https://api.nuget.org/v3/index.json `
    /p:UseSharedCompilation=false
if ($LASTEXITCODE -ne 0) { throw "DynLock.Installer build failed." }

Write-Host ""
Write-Host "=== 5. Update dist folders ===" -ForegroundColor Cyan

$distRoot = Join-Path $base "dist"
New-Item -ItemType Directory -Path $distRoot -Force | Out-Null

# These folders and archives are generated outputs. Recreate them so stale
# dependencies from older builds can never leak into a new user package.
Get-ChildItem -LiteralPath $distRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^BIMLab_(Player_Member|Studio_Leader)(_.+)?$' } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
Get-ChildItem -LiteralPath $distRoot -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^BIMLab_(Player_Member|Studio_Leader)(_.+)?\.zip$' } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }

$memberFolderName = "BIMLab_Player_Member_$ReleaseDate"
$leaderFolderName = "BIMLab_Studio_Leader_$ReleaseDate"
$distMember = Join-Path $distRoot $memberFolderName
$distLeader = Join-Path $distRoot $leaderFolderName
New-Item -ItemType Directory -Path $distMember -Force | Out-Null
New-Item -ItemType Directory -Path $distLeader -Force | Out-Null

$playerExe = Join-Path $base "src/DynLock.Installer/bin/Release/net48/BIMLab Player.exe"
if (-not (Test-Path -LiteralPath $playerExe)) {
    throw "NOT FOUND: $playerExe"
}
$playerExeName = "BIMLab Player - $ReleaseDate.exe"
$playerGuideName = "Hướng dẫn - BIMLab Player - $ReleaseDate.txt"
Copy-Item -LiteralPath $playerExe -Destination (Join-Path $distMember $playerExeName) -Force
Copy-Item -LiteralPath (Join-Path $base "HUONG_DAN_NHAN_VIEN.txt") `
    -Destination (Join-Path $distMember $playerGuideName) -Force
Write-Host "  Created clean Player package folder: $memberFolderName"

$studioExe = Join-Path $base "src/DynLock.EncryptorGui/bin/Release/net48/BIMLab Studio.exe"
if (-not (Test-Path -LiteralPath $studioExe)) {
    throw "NOT FOUND: $studioExe"
}
$studioExeName = "BIMLab Studio - $ReleaseDate.exe"
$studioGuideName = "Hướng dẫn - BIMLab Studio - $ReleaseDate.txt"
Copy-Item -LiteralPath $studioExe -Destination (Join-Path $distLeader $studioExeName) -Force
Copy-Item -LiteralPath (Join-Path $base "HUONG_DAN_TEAM_LEAD.txt") `
    -Destination (Join-Path $distLeader $studioGuideName) -Force
Write-Host "  Created clean Studio package folder: $leaderFolderName"

Assert-AllowedEntries -Path $distMember -AllowedNames @($playerExeName, $playerGuideName)
Assert-AllowedEntries -Path $distLeader -AllowedNames @($studioExeName, $studioGuideName)
Assert-NoForbiddenFiles -Path $distRoot

Write-Host ""
Write-Host "=== 6. Rebuild zip files ===" -ForegroundColor Cyan

$zipMember = Join-Path $distRoot "$memberFolderName.zip"
$zipLeader = Join-Path $distRoot "$leaderFolderName.zip"

if (Test-Path -LiteralPath $zipMember) { Remove-Item -LiteralPath $zipMember -Force }
if (Test-Path -LiteralPath $zipLeader) { Remove-Item -LiteralPath $zipLeader -Force }

Compress-Archive -Path $distMember -DestinationPath $zipMember -Force
Compress-Archive -Path $distLeader -DestinationPath $zipLeader -Force

Assert-CleanArchive -Path $zipMember
Assert-CleanArchive -Path $zipLeader
Assert-AllowedEntries -Path $distRoot -AllowedNames @(
    $memberFolderName,
    $leaderFolderName,
    [System.IO.Path]::GetFileName($zipMember),
    [System.IO.Path]::GetFileName($zipLeader)
)

Write-Host "  Created and verified: $memberFolderName.zip"
Write-Host "  Created and verified: $leaderFolderName.zip"

Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host "dist/ contains 2 clean folders + 2 verified zip files ready to send."
