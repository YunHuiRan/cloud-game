param(
    [Parameter(Mandatory = $true)]
    [string]$Version
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$releaseDirectory = Join-Path $repositoryRoot 'release'
$archiveName = "CloudGame-$Version-win64.zip"
$archivePath = Join-Path $releaseDirectory $archiveName
$checksumPath = Join-Path $releaseDirectory 'SHA256SUMS.txt'
$temporaryDirectory = Join-Path $env:TEMP "cloud-game-release-$Version"

if (-not (Test-Path -LiteralPath $archivePath)) {
    throw "Release archive not found: $archivePath"
}

$checksums = Get-ChildItem -LiteralPath $releaseDirectory -Filter 'CloudGame-*-win64.zip' |
    Sort-Object Name |
    ForEach-Object {
        "SHA256  $((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash)  $($_.Name)"
    }
[System.IO.File]::WriteAllText(
    $checksumPath,
    (($checksums -join "`n") + "`n"),
    [System.Text.UTF8Encoding]::new($false))

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    $entryNames = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
}
finally {
    $archive.Dispose()
}

$requiredEntries = @(
    'CloudGame.exe',
    'CloudGame.dll',
    'CloudGame.deps.json',
    'CloudGame.runtimeconfig.json',
    'README.md',
    'README.en.md',
    'docs/screenshots/library-dark.png'
)

$missingEntries = @($requiredEntries | Where-Object { $_ -notin $entryNames })
if ($missingEntries.Count -gt 0) {
    throw "Release archive is missing: $($missingEntries -join ', ')"
}

Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $temporaryDirectory -Force
    $exeVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $temporaryDirectory 'CloudGame.exe')).FileVersion
    $dllVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $temporaryDirectory 'CloudGame.dll')).Version.ToString()

    if ($exeVersion -ne "$Version.0" -or $dllVersion -ne "$Version.0") {
        throw "Archive version mismatch. EXE=$exeVersion; DLL=$dllVersion; expected=$Version.0"
    }
}
finally {
    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Output "PASS: $archiveName"
Write-Output "EXE/DLL version: $Version.0"
Get-Content -LiteralPath $checksumPath