param([string]$Html, [string]$Unity, [string]$Out)
# Puts the HTML screenshot (left) and the Unity capture (right) side by side at the same height.
Add-Type -AssemblyName System.Drawing
$imgHtml = [System.Drawing.Image]::FromFile($Html)
$imgUnity = [System.Drawing.Image]::FromFile($Unity)
$height = 1500
$wHtml = [int]($imgHtml.Width * $height / $imgHtml.Height); $wUnity = [int]($imgUnity.Width * $height / $imgUnity.Height)
$gap = 16
$bmp = New-Object System.Drawing.Bitmap ($wHtml + $wUnity + $gap), $height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::FromArgb(255, 40, 40, 40))
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($imgHtml, 0, 0, $wHtml, $height)
$g.DrawImage($imgUnity, $wHtml + $gap, 0, $wUnity, $height)
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose(); $imgHtml.Dispose(); $imgUnity.Dispose()
