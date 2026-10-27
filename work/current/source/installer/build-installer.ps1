param(
    [Parameter(Mandatory=$true)][string]$Package,
    [Parameter(Mandatory=$true)][string]$Compiler,
    [Parameter(Mandatory=$true)][string]$Output,
    [switch]$TestBuild
)
$ErrorActionPreference='Stop'
$orbitPackage=(Resolve-Path -LiteralPath $Package).Path
$orbitCompiler=(Resolve-Path -LiteralPath $Compiler).Path
$orbitOutput=[IO.Path]::GetFullPath($Output)
$orbitStage=Join-Path ([IO.Path]::GetDirectoryName($orbitOutput)) ('installer-stage-'+[Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($orbitStage) | Out-Null
try {
Expand-Archive -LiteralPath $orbitPackage -DestinationPath $orbitStage
$orbitPayload=Join-Path $orbitStage 'orbitals'
$orbitVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $orbitPayload 'orbitals.exe')).FileVersion
$orbitUninstall=Join-Path $orbitStage 'uninstall-files.nsh'
$orbitLines=[Collections.Generic.List[string]]::new()
foreach($orbitFile in Get-ChildItem -LiteralPath $orbitPayload -Recurse -File) {
    $orbitRelative=$orbitFile.FullName.Substring($orbitPayload.Length+1)
    if($orbitRelative.Contains('$') -or $orbitRelative.Contains('"')) { throw 'Unsupported package filename.' }
    $orbitLines.Add('Delete "$INSTDIR\'+$orbitRelative+'"')
}
foreach($orbitDirectory in (Get-ChildItem -LiteralPath $orbitPayload -Recurse -Directory | Sort-Object { $_.FullName.Length } -Descending)) { $orbitRelative=$orbitDirectory.FullName.Substring($orbitPayload.Length+1); $orbitLines.Add('RMDir "$INSTDIR\'+$orbitRelative+'"') }
[IO.File]::WriteAllLines($orbitUninstall,$orbitLines,[Text.UTF8Encoding]::new($true))
Add-Type -AssemblyName System.Drawing
$orbitSidebar=Join-Path $orbitStage 'sidebar.bmp'
$orbitBitmap=[Drawing.Bitmap]::new(328,628)
$orbitGraphics=[Drawing.Graphics]::FromImage($orbitBitmap)
$orbitBrand=[Drawing.Icon]::new((Join-Path $PSScriptRoot '../assets/app/running.ico'),256,256)
$orbitImage=$orbitBrand.ToBitmap()
$orbitFont=[Drawing.Font]::new('Segoe UI',22,[Drawing.FontStyle]::Regular)
$orbitBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(147,233,193))
try {
    $orbitGraphics.Clear([Drawing.Color]::FromArgb(15,20,27))
    $orbitGraphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $orbitGraphics.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $orbitGraphics.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $orbitGraphics.DrawImage($orbitImage,72,160,184,184)
    $orbitGraphics.DrawString('ORBITALS',$orbitFont,$orbitBrush,90,380)
    $orbitBitmap.Save($orbitSidebar,[Drawing.Imaging.ImageFormat]::Bmp)
} finally { $orbitGraphics.Dispose(); $orbitBitmap.Dispose(); $orbitImage.Dispose(); $orbitBrand.Dispose(); $orbitFont.Dispose(); $orbitBrush.Dispose() }
$orbitIcon=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../assets/app/running.ico')).Path
$orbitSize=[Math]::Ceiling(((Get-ChildItem -LiteralPath $orbitPayload -Recurse -File | Measure-Object Length -Sum).Sum)/1024)
$orbitArguments=@('/V2','/INPUTCHARSET','UTF8',('/DPAYLOAD='+$orbitPayload),('/DAPP_VERSION='+$orbitVersion),('/DAPP_ICON='+$orbitIcon),('/DAPP_SIZE='+$orbitSize),('/DSIDEBAR='+$orbitSidebar),('/DUNINSTALL_FILES='+$orbitUninstall),('/DSETUP_OUTPUT='+$orbitOutput))
if($TestBuild) { $orbitArguments+='/DINSTALL_TEST=1' }
$orbitArguments+=Join-Path $PSScriptRoot 'orbitals.nsi'
& $orbitCompiler @orbitArguments
if($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
Write-Output $orbitOutput
} finally {
    if(Test-Path -LiteralPath $orbitStage -PathType Container) {
        $orbitResolved=(Get-Item -LiteralPath $orbitStage).FullName
        if(-not [String]::Equals([IO.Path]::GetDirectoryName($orbitResolved),[IO.Path]::GetDirectoryName($orbitOutput),[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($orbitResolved) -notmatch '^installer-stage-[0-9a-f]{32}$') { throw 'Unexpected installer staging path; cleanup stopped.' }
        Remove-Item -LiteralPath $orbitResolved -Recurse -Force
    }
}
