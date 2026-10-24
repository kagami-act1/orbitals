param(
    [Parameter(Mandatory=$true)][string]$Repository,
    [string]$OutputDirectory
)
$ErrorActionPreference='Stop'
if($Repository -notmatch '\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,37}[A-Za-z0-9])?/[A-Za-z0-9_][A-Za-z0-9_.-]{0,99}\z' -or $Repository.Split('/')[-1] -in @('.','..')) { throw 'Specify owner/repository.' }
$orbitApp=Split-Path -Parent $PSScriptRoot
if(-not $OutputDirectory) { $OutputDirectory=Join-Path $orbitApp 'distribution' }
$orbitOutput=[IO.Path]::GetFullPath($OutputDirectory)
[IO.Directory]::CreateDirectory($orbitOutput) | Out-Null
$orbitPackage=Join-Path $orbitOutput 'orbitals-Windows11.zip'
if(Test-Path -LiteralPath $orbitPackage) { throw 'Package already exists; choose an empty output directory.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$orbitFiles=@('orbitals.exe','orbitals.exe.config','使い方.md','アイコン分類.md','プラグインの作り方.md','THIRD-PARTY-NOTICES.txt','LICENSE-Material-Symbols.txt','LICENSE-NSIS.txt')
foreach($orbitName in $orbitFiles) { if(-not (Test-Path -LiteralPath (Join-Path $orbitApp $orbitName) -PathType Leaf)) { throw "Missing distribution file: $orbitName" } }
$orbitConversion=Join-Path $orbitApp 'conversion'
foreach($orbitName in @('PdfSharp-wpf.dll','cwebp.exe','dwebp.exe','resvg.exe','licenses\PDFsharp-LICENSE.txt','licenses\libwebp-COPYING.txt','licenses\resvg-LICENSE.txt')) { if(-not (Test-Path -LiteralPath (Join-Path $orbitConversion $orbitName) -PathType Leaf)) { throw "Missing conversion component: $orbitName" } }
$orbitZip=[IO.Compression.ZipFile]::Open($orbitPackage,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($orbitName in $orbitFiles) { [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($orbitZip,(Join-Path $orbitApp $orbitName),('orbitals/'+$orbitName),[IO.Compression.CompressionLevel]::Optimal) | Out-Null }
    foreach($orbitFile in Get-ChildItem -LiteralPath $orbitConversion -Recurse -File) { $orbitRelative=$orbitFile.FullName.Substring($orbitConversion.Length+1).Replace('\','/'); [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($orbitZip,$orbitFile.FullName,('orbitals/conversion/'+$orbitRelative),[IO.Compression.CompressionLevel]::Optimal) | Out-Null }
    $orbitExamples=Join-Path $orbitApp 'plugin-examples'
    foreach($orbitFile in Get-ChildItem -LiteralPath $orbitExamples -Recurse -File) { $orbitRelative=$orbitFile.FullName.Substring($orbitExamples.Length+1).Replace('\','/'); [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($orbitZip,$orbitFile.FullName,('orbitals/plugin-examples/'+$orbitRelative),[IO.Compression.CompressionLevel]::Optimal) | Out-Null }
    $orbitEntry=$orbitZip.CreateEntry('orbitals/update-channel.json')
    $orbitWriter=[IO.StreamWriter]::new($orbitEntry.Open(),[Text.UTF8Encoding]::new($false))
    try { $orbitWriter.Write((@{Repository=$Repository} | ConvertTo-Json -Compress)) } finally { $orbitWriter.Dispose() }
} finally { $orbitZip.Dispose() }
$orbitHash=(Get-FileHash -LiteralPath $orbitPackage -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $orbitOutput 'SHA256SUMS.txt'),($orbitHash+'  orbitals-Windows11.zip'+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Output $orbitPackage
