[CmdletBinding()]
param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '2.0.0-alpha.1',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts\release'))
$versionRoot = [System.IO.Path]::GetFullPath((Join-Path $releaseRoot $Version))
$packageName = "EasyHTTPServer-$Version-win-x64"
$packageDirectory = Join-Path $versionRoot 'app'
$publishDirectory = Join-Path $versionRoot '_publish'
$zipPath = Join-Path $versionRoot "$packageName.zip"
$outerChecksums = Join-Path $versionRoot 'SHA256SUMS.txt'

if (-not $versionRoot.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Resolved release path escaped artifacts/release.'
}

if (Test-Path -LiteralPath $versionRoot) {
    Remove-Item -LiteralPath $versionRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null

Push-Location $repoRoot
try {
    dotnet restore EasyHTTPServer.sln --runtime win-x64
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build EasyHTTPServer.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    if (-not $SkipTests) {
        dotnet test EasyHTTPServer.sln -c Release --no-build --no-restore
        if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
    }

    dotnet publish src\EasyHttpServer.Desktop.Wpf\EasyHttpServer.Desktop.Wpf.csproj `
        -c Release `
        --no-restore `
        -p:PublishProfile=win-x64-self-contained `
        -p:Version=$Version `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $publishDirectory
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
}
finally {
    Pop-Location
}

Copy-Item -Path (Join-Path $publishDirectory '*') -Destination $packageDirectory -Recurse
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'LICENSE.md') -Destination $packageDirectory
Copy-Item -LiteralPath (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.txt') -Destination $packageDirectory
New-Item -ItemType Directory -Path (Join-Path $packageDirectory 'docs') | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\lan-security.md') -Destination (Join-Path $packageDirectory 'docs')

$forbiddenExtensions = @('.pdb', '.pfx', '.snk', '.pwd')
$forbiddenFileNames = @('settings.json', 'settings.local.json')
$forbiddenDirectoryNames = @('Save', 'Source', 'tests', 'log', 'logs')
$packagedItems = Get-ChildItem -LiteralPath $packageDirectory -Recurse -Force
$forbidden = $packagedItems | Where-Object {
    (-not $_.PSIsContainer -and ($forbiddenExtensions -contains $_.Extension -or $forbiddenFileNames -contains $_.Name)) -or
    ($_.PSIsContainer -and $forbiddenDirectoryNames -contains $_.Name)
}
if ($forbidden) {
    throw "Forbidden release content detected: $($forbidden.FullName -join ', ')"
}

$executable = Join-Path $packageDirectory 'EasyHTTPServer.exe'
if (-not (Test-Path -LiteralPath $executable)) {
    throw 'EasyHTTPServer.exe was not published.'
}

$guideIndex = Join-Path $packageDirectory 'Guide\index.html'
$guideText = Join-Path $packageDirectory 'Guide\README.txt'
if (-not (Test-Path -LiteralPath $guideIndex) -or -not (Test-Path -LiteralPath $guideText)) {
    throw 'Bundled first-run guide was not published.'
}

$versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($versionInfo.ProductName -ne 'EasyHTTPServer 2' -or
    $versionInfo.ProductVersion -ne $Version -or
    $versionInfo.FileVersion -ne '2.0.0.0' -or
    $versionInfo.CompanyName -ne 'charmpic') {
    throw "Unexpected executable metadata: $($versionInfo.ProductName) $($versionInfo.ProductVersion) $($versionInfo.CompanyName)"
}

$innerChecksums = Join-Path $packageDirectory 'SHA256SUMS.txt'
$checksumLines = Get-ChildItem -LiteralPath $packageDirectory -File -Recurse |
    Where-Object { $_.FullName -ne $innerChecksums } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = [System.IO.Path]::GetRelativePath($packageDirectory, $_.FullName).Replace('\', '/')
        "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    }
[System.IO.File]::WriteAllLines($innerChecksums, $checksumLines, [System.Text.UTF8Encoding]::new($false))

Remove-Item -LiteralPath $publishDirectory -Recurse -Force
Compress-Archive -Path (Join-Path $packageDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[System.IO.File]::WriteAllText(
    $outerChecksums,
    "$zipHash  $([System.IO.Path]::GetFileName($zipPath))`n",
    [System.Text.UTF8Encoding]::new($false))

Write-Output $zipPath
Write-Output $outerChecksums
