$ErrorActionPreference='Stop'
$orbitFramework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$orbitArguments=@('/nologo','/target:exe','/platform:x64','/codepage:65001',('/out:'+(Join-Path $PSScriptRoot 'OrbitalsExample.exe')),('/reference:'+(Join-Path $orbitFramework 'System.Web.Extensions.dll')),(Join-Path $PSScriptRoot 'Program.cs'))
& (Join-Path $orbitFramework 'csc.exe') @orbitArguments
if($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output (Join-Path $PSScriptRoot 'OrbitalsExample.exe')
