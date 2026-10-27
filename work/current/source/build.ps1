$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Split-Path -Parent $PSScriptRoot
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','Microsoft.CSharp.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/out:' + (Join-Path $output 'orbitals.exe')),('/win32manifest:' + (Join-Path $PSScriptRoot 'app\app.manifest')))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$codec=Join-Path $output 'conversion'
$codecArguments=@('/nologo','/target:exe','/platform:x64','/optimize+','/codepage:65001',('/out:'+(Join-Path $codec 'image-codec.exe')),('/reference:'+(Join-Path $framework 'netstandard.dll')),('/reference:'+(Join-Path $codec 'Magick.NET.Core.dll')),('/reference:'+(Join-Path $codec 'Magick.NET-Q8-x64.dll')),(Join-Path $PSScriptRoot 'image-codec\Program.cs'))
& (Join-Path $framework 'csc.exe') @codecArguments
if ($LASTEXITCODE -ne 0) { throw 'Image codec build failed' }
$arguments += '/reference:WinRT=' + (Join-Path $framework 'System.Runtime.WindowsRuntime.dll')
$arguments += '/reference:Pdf=' + (Join-Path $output 'conversion\PdfSharp-wpf.dll')
$arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'assets\app\running.ico')
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'assets\app\running.ico') + ',OrbitRing.AppIcon'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'assets\app\stopped.ico') + ',OrbitRing.AppIcon.Stopped'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'assets\app\error.ico') + ',OrbitRing.AppIcon.Error'
foreach ($module in @('app','actions','planet','orbit','conversion','ui','tests')) { $arguments += (Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $module) -Filter '*.cs' -File -Recurse).FullName }
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'ui\EditorStyles.xaml') + ',OrbitRing.EditorStyles'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'icons\catalog.tsv.gz') + ',OrbitRing.IconCatalog'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'icons\categories.tsv') + ',OrbitRing.IconCategories'
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Output (Join-Path $output 'orbitals.exe')
