$ErrorActionPreference='Stop'
$runtime=Join-Path $PSScriptRoot 'release\packages\current'
Start-Process -FilePath (Join-Path $runtime 'orbitals.exe') -WorkingDirectory $runtime
