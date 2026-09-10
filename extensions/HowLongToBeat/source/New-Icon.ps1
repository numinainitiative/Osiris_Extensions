$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$bitmap = New-Object Drawing.Bitmap 512, 512
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([Drawing.Color]::Transparent)

$background = New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(255, 37, 40, 45))
$white = [Drawing.Color]::FromArgb(255, 244, 245, 247)
$outline = New-Object Drawing.Pen $white, 34
$outline.StartCap = [Drawing.Drawing2D.LineCap]::Round
$outline.EndCap = [Drawing.Drawing2D.LineCap]::Round
$hand = New-Object Drawing.Pen $white, 30
$hand.StartCap = [Drawing.Drawing2D.LineCap]::Round
$hand.EndCap = [Drawing.Drawing2D.LineCap]::Round
$dot = New-Object Drawing.SolidBrush $white

$graphics.FillRectangle($background, 0, 0, 512, 512)
$graphics.DrawEllipse($outline, 114, 132, 284, 284)
$graphics.DrawLine($outline, 202, 74, 310, 74)
$graphics.DrawLine($outline, 256, 74, 256, 128)
$graphics.DrawLine($outline, 365, 142, 403, 180)
$graphics.DrawLine($hand, 256, 274, 256, 184)
$graphics.DrawLine($hand, 256, 274, 328, 316)
$graphics.FillEllipse($dot, 239, 257, 34, 34)

$target = Join-Path $PSScriptRoot "icon.png"
$bitmap.Save($target, [Drawing.Imaging.ImageFormat]::Png)

$dot.Dispose()
$hand.Dispose()
$outline.Dispose()
$background.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Generated $target"
