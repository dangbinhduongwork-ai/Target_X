$icoPath = Join-Path $PSScriptRoot "Assets\app.ico"
$assetsDir = Join-Path $PSScriptRoot "Assets"

if (-not (Test-Path $assetsDir)) {
    [System.IO.Directory]::CreateDirectory($assetsDir) | Out-Null
}

if (-not (Test-Path $icoPath) -or (Get-Item $icoPath).Length -lt 100) {
    Add-Type -AssemblyName System.Drawing
    $b = New-Object System.Drawing.Bitmap 32, 32
    $g = [System.Drawing.Graphics]::FromImage($b)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(255, 15, 23, 42))

    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 56, 189, 248), 2)
    $g.DrawEllipse($pen, 3, 3, 25, 25)
    $g.DrawEllipse($pen, 8, 8, 15, 15)

    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 96, 165, 250))
    $g.FillEllipse($brush, 12, 12, 8, 8)

    $h = $b.GetHicon()
    $ico = [System.Drawing.Icon]::FromHandle($h)
    $fs = [System.IO.File]::Create($icoPath)
    $ico.Save($fs)
    $fs.Close()
    $ico.Dispose()
    $b.Dispose()
}
