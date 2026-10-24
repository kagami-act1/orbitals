$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dependencies=Join-Path $PSScriptRoot 'dependencies'
$destination=Join-Path $PSScriptRoot 'current\conversion'
New-Item -ItemType Directory -Path $destination,(Join-Path $destination 'licenses') -Force | Out-Null
$manifest=Get-Content -LiteralPath (Join-Path $dependencies 'manifest.json') -Raw | ConvertFrom-Json
foreach($entry in $manifest) { $path=Join-Path $dependencies $entry.File; if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Sha256) { throw "Dependency checksum mismatch: $($entry.File)" } }
foreach($file in $manifest | Where-Object File -like '*.nupkg' | ForEach-Object { Get-Item -LiteralPath (Join-Path $dependencies $_.File) }) {
 $zip=[IO.Compression.ZipFile]::OpenRead($file.FullName)
 try {
  $framework=if($file.Name -like 'system.numerics.vectors*'){'net46'}elseif($file.Name -match '^(system.buffers|system.memory|system.runtime.compilerservices.unsafe|system.threading.tasks.extensions)\.'){'net461'}else{'net462'}
  foreach($entry in $zip.Entries) {
   if($entry.FullName -match "^lib/$framework/[^/]+\.dll$" -and $entry.Name -notmatch '(BarCodes|Quality|Charting|Snippets)') { [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $destination $entry.Name),$true) }
   if($entry.FullName -match '^LICENSE(\.TXT|\.txt)?$'){ [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $destination ('licenses\'+$file.BaseName+'-LICENSE.txt')),$true) }
  }
 } finally {$zip.Dispose()}
}
$zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $dependencies 'libwebp-1.6.0-windows-x64.zip'))
try { foreach($name in @('cwebp','dwebp')) { [IO.Compression.ZipFileExtensions]::ExtractToFile($zip.GetEntry("libwebp-1.6.0-windows-x64/bin/$name.exe"),(Join-Path $destination "$name.exe"),$true) } } finally {$zip.Dispose()}
Copy-Item -LiteralPath (Join-Path $dependencies 'PDFsharp-LICENSE.txt'),(Join-Path $dependencies 'libwebp-COPYING.txt'),(Join-Path $dependencies 'libwebp-PATENTS.txt') -Destination (Join-Path $destination 'licenses')
Get-ChildItem -LiteralPath $destination -File | Select-Object Name,Length
