# Generuje ikonę aplikacji (kaseta w stylu retro) -> Assets/AppIcon.ico + AppIcon.png
# Uruchom w Windows PowerShell 5.1:  powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot '..\src\Kasetownik9000\Assets'
New-Item -ItemType Directory -Force $outDir | Out-Null

function New-RoundRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function C([string]$hex, [int]$a = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    return [System.Drawing.Color]::FromArgb($a, $c.R, $c.G, $c.B)
}

function Draw-Cassette([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 256.0
    $g.ScaleTransform($s, $s)
    $detail = $size -ge 48

    # --- korpus kasety ---
    $body = New-RoundRect 8 46 240 164 22
    $bodyBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 0, 46), (New-Object System.Drawing.PointF 0, 210), (C '#4A403A'), (C '#1F1A17')
    $g.FillPath($bodyBrush, $body)
    $g.DrawPath((New-Object System.Drawing.Pen (C '#120F0D'), 4), $body)

    # śrubki w rogach
    if ($detail) {
        foreach ($pt in @(@(24, 62), @(232, 62), @(24, 194), @(232, 194))) {
            $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#8C827A')), $pt[0] - 5, $pt[1] - 5, 10, 10)
            $g.DrawLine((New-Object System.Drawing.Pen (C '#3A332E'), 2), $pt[0] - 3, $pt[1], $pt[0] + 3, $pt[1])
        }
    }

    # --- etykieta ---
    $label = New-RoundRect 22 58 212 112 10
    $g.FillPath((New-Object System.Drawing.SolidBrush (C '#F3E9D2')), $label)

    # retro paski (żółty, pomarańczowy, czerwony, brąz)
    $stripes = @('#F2B035', '#E8722C', '#C8402F', '#7A3B2E')
    $sy = if ($detail) { 76 } else { 70 }
    $sh = if ($detail) { 9 } else { 14 }
    $g.SetClip($label)
    for ($i = 0; $i -lt $stripes.Count; $i++) {
        $g.FillRectangle((New-Object System.Drawing.SolidBrush (C $stripes[$i])), 22, $sy + $i * $sh, 212, $sh)
    }
    $g.ResetClip()

    # linia tytułu
    if ($detail) {
        $g.FillRectangle((New-Object System.Drawing.SolidBrush (C '#2B2420')), 40, 64, 90, 6)
        $g.FillRectangle((New-Object System.Drawing.SolidBrush (C '#C8402F')), 196, 63, 22, 8)
    }

    # --- okienko z rolkami ---
    $win = New-RoundRect 66 112 124 46 23
    $g.FillPath((New-Object System.Drawing.SolidBrush (C '#2B2420')), $win)
    if ($detail) {
        # taśma widoczna w okienku
        $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#5A3A28')), 74, 117, 50, 36)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#5A3A28')), 140, 121, 42, 28)
    }
    foreach ($cx in @(98, 158)) {
        $r = 15
        $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#F3E9D2')), $cx - $r, 135 - $r, 2 * $r, 2 * $r)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#2B2420')), $cx - 8, 127, 16, 16)
        if ($detail) {
            for ($k = 0; $k -lt 6; $k++) {
                $a = $k * [Math]::PI / 3
                $tx = $cx + [Math]::Cos($a) * 8
                $ty = 135 + [Math]::Sin($a) * 8
                $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#F3E9D2')), [float]($tx - 2.5), [float]($ty - 2.5), 5, 5)
            }
        }
    }

    # --- trapez na dole ---
    $trap = New-Object System.Drawing.Drawing2D.GraphicsPath
    $trap.AddPolygon(@(
        (New-Object System.Drawing.PointF 62, 210),
        (New-Object System.Drawing.PointF 76, 178),
        (New-Object System.Drawing.PointF 180, 178),
        (New-Object System.Drawing.PointF 194, 210)))
    $g.FillPath((New-Object System.Drawing.SolidBrush (C '#5C524B')), $trap)
    if ($detail) {
        foreach ($hx in @(92, 164)) { $g.FillEllipse((New-Object System.Drawing.SolidBrush (C '#120F0D')), $hx - 6, 188, 12, 12) }
        foreach ($hx in @(116, 134)) { $g.FillRectangle((New-Object System.Drawing.SolidBrush (C '#120F0D')), $hx, 190, 8, 8) }
    }

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 64, 48, 40, 32, 24, 20, 16)
$pngs = @()
foreach ($sz in $sizes) {
    $bmp = Draw-Cassette $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($sz -eq 256) { $bmp.Save((Join-Path $outDir 'AppIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    $pngs += , @($sz, $ms.ToArray())
}

# --- zapis ICO (wpisy PNG) ---
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ico
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
foreach ($p in $pngs) {
    $sz = $p[0]; $data = $p[1]
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($p in $pngs) { $bw.Write([byte[]]$p[1]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $outDir 'AppIcon.ico'), $ico.ToArray())
Write-Host "Zapisano ikone do $outDir"
