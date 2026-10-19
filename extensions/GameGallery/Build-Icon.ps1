param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'source'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
$context = New-Object System.Windows.Markup.ParserContext
$context.BaseUri = [Uri]::new(($PSScriptRoot + '\'))
$visual = [System.Windows.Markup.XamlReader]::Parse([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Icon.xaml')), $context)
$size = New-Object System.Windows.Size(256,256)
$visual.Measure($size)
$visual.Arrange((New-Object System.Windows.Rect($size)))
$visual.UpdateLayout()
$bitmap = New-Object System.Windows.Media.Imaging.RenderTargetBitmap(256,256,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create((Join-Path $OutputDirectory 'icon.png'))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
