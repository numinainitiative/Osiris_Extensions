param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
# User-supplied Lucide joystick, rendered directly from the preserved SVG.
$svgPath = Join-Path $PSScriptRoot 'assets\joystick.svg'
$svg = [xml](Get-Content -LiteralPath $svgPath -Raw)
if ($svg.DocumentElement.GetAttribute('viewBox') -ne '0 0 24 24') { throw 'Unexpected joystick viewBox.' }
$visual = [Windows.Media.DrawingVisual]::new()
$drawing = $visual.RenderOpen()
$gradient = [Windows.Media.LinearGradientBrush]::new([Windows.Media.ColorConverter]::ConvertFromString('#A1872C'), [Windows.Media.ColorConverter]::ConvertFromString('#5A4B1C'), 45)
$drawing.DrawRoundedRectangle($gradient, $null, [Windows.Rect]::new(0,0,128,128), 24,24)
$pen = [Windows.Media.Pen]::new([Windows.Media.Brushes]::White, 2)
$pen.StartLineCap = 'Round'; $pen.EndLineCap = 'Round'; $pen.LineJoin = 'Round'
$drawing.PushTransform([Windows.Media.TranslateTransform]::new(20,20))
$drawing.PushTransform([Windows.Media.ScaleTransform]::new((88.0/24),(88.0/24)))
foreach ($element in $svg.DocumentElement.ChildNodes) {
    if ($element.LocalName -eq 'path') {
        $drawing.DrawGeometry($null,$pen,[Windows.Media.Geometry]::Parse($element.GetAttribute('d')))
    } elseif ($element.LocalName -eq 'circle') {
        $radius = [double]::Parse($element.GetAttribute('r'),[Globalization.CultureInfo]::InvariantCulture)
        $centre = [Windows.Point]::new([double]::Parse($element.GetAttribute('cx'),[Globalization.CultureInfo]::InvariantCulture),[double]::Parse($element.GetAttribute('cy'),[Globalization.CultureInfo]::InvariantCulture))
        $drawing.DrawEllipse($null,$pen,$centre,$radius,$radius)
    } else { throw 'Unsupported joystick SVG element.' }
}
$drawing.Pop()
$drawing.Pop()
$drawing.Close()
$bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new(128,128,96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$stream = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
