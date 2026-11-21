param([string]$OutputName = 'Action Jackson.exe')
$ErrorActionPreference = 'Stop'
$sourceDir = $PSScriptRoot
$appDir = Split-Path -Parent $sourceDir
$frameworkDir = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
Add-Type -AssemblyName System.Drawing
$original = [Drawing.Image]::FromFile((Join-Path $appDir 'soldier.png'))
$frames = [Collections.Generic.List[byte[]]]::new()
$sizes = @(16,24,32,48,64,128,256)
try {
 foreach ($size in $sizes) {
  $bitmap = [Drawing.Bitmap]::new($size,$size)
  $graphics = [Drawing.Graphics]::FromImage($bitmap)
  $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $graphics.DrawImage($original,0,0,$size,$size)
  $stream = [IO.MemoryStream]::new()
  $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
  $frames.Add($stream.ToArray())
  $graphics.Dispose(); $bitmap.Dispose(); $stream.Dispose()
 }
} finally { $original.Dispose() }
$iconPath = Join-Path $appDir 'soldier.ico'
$file = [IO.File]::Create($iconPath)
$writer = [IO.BinaryWriter]::new($file)
try {
 $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
 $offset = 6 + 16 * $sizes.Count
 for ($i=0; $i -lt $sizes.Count; $i++) {
  $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
  $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
  $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
  $offset += $frames[$i].Length
 }
 foreach ($frame in $frames) { $writer.Write($frame) }
} finally { $writer.Dispose(); $file.Dispose() }
$refs = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Runtime.WindowsRuntime.dll') | ForEach-Object { '/r:' + (Join-Path $frameworkDir $_) }
foreach ($assembly in @('System.Runtime','System.Speech','UIAutomationClient','UIAutomationTypes','WindowsBase')) {
 $assemblyPath = Get-ChildItem (Join-Path $env:WINDIR "Microsoft.NET\assembly\GAC_MSIL\$assembly") -Recurse -Filter "$assembly.dll" | Select-Object -First 1 -ExpandProperty FullName
 $refs += '/r:' + $assemblyPath
}
$refs += Get-ChildItem (Join-Path $env:WINDIR 'System32\WinMetadata') -Filter *.winmd | ForEach-Object { '/r:' + $_.FullName }
$files = Get-ChildItem -LiteralPath $sourceDir -Filter *.cs | Select-Object -ExpandProperty FullName
$exePath = Join-Path $appDir $OutputName
& (Join-Path $frameworkDir 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ ('/out:'+$exePath) ('/win32icon:'+$iconPath) ('/win32manifest:'+(Join-Path $sourceDir 'app.manifest')) ('/resource:'+(Join-Path $appDir 'soldier.png')+',soldier.png') ('/resource:'+$iconPath+',soldier.ico') @refs @files
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output "Built: $exePath"
