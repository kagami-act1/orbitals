$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Split-Path -Parent $PSScriptRoot
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','Microsoft.CSharp.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/out:' + (Join-Path $output 'orbitals.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += '/reference:WinRT=' + (Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
$arguments += '/reference:Pdf=' + (Join-Path $output 'conversion\PdfSharp-wpf.dll')
$arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'assets\OrbitRing.ico')
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'assets\OrbitRing.ico') + ',OrbitRing.AppIcon'
$arguments += (Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs').FullName
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'EditorStyles.xaml') + ',OrbitRing.EditorStyles'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'icons\catalog.tsv.gz') + ',OrbitRing.IconCatalog'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'icons\categories.tsv') + ',OrbitRing.IconCategories'
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output (Join-Path $output 'orbitals.exe')
