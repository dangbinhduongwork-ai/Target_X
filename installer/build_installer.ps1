# ==============================================================================
# Script tu dong Chay Test, Publish Release va Dong goi Inno Setup cho GoalOverlay
# ==============================================================================
[CmdletBinding()]
param(
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   GoalOverlay - Tu dong Test, Build Release & Dong goi Setup   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
$projectDir = Resolve-Path "$scriptRoot\..\src\GoalOverlay"
$slnPath = Resolve-Path "$scriptRoot\..\GoalOverlay.sln"
$publishDir = "$projectDir\bin\Release\net8.0-windows\win-x64\publish"
$issPath = "$scriptRoot\GoalOverlay.iss"
$outputDir = "$scriptRoot\output"

# 1. CHAY TOAN BO HE THONG KIEM THU TU DONG (UNIT TESTS)
if (-not $SkipTests) {
    Write-Host "`n[1/4] Dang chay kiem thu toan bo he thong Unit Tests..." -ForegroundColor Yellow
    & dotnet test "$slnPath" --nologo
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Co bai kiem thu (unit test) that bai! Dung tien trinh de bao dam chat luong."
        exit 1
    }
    Write-Host "-> Tat ca cac bai Unit Tests deu PASS 100%!" -ForegroundColor Green
} else {
    Write-Host "`n[1/4] Bo qua buoc kiem thu theo yeu cau (-SkipTests)." -ForegroundColor DarkGray
}

# 2. BIEN DICH VA XUAT BAN RELEASE SINGLE-FILE SELF-CONTAINED
Write-Host "`n[2/4] Dang bien dich Release x64 Single-file (Self-contained)..." -ForegroundColor Yellow

$publishArgs = @(
    "publish",
    "$projectDir\GoalOverlay.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-o", "$publishDir"
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "Qua trinh dotnet publish that bai! Vui long kiem tra log loi phia tren."
    exit 1
}

$exePath = Join-Path $publishDir "GoalOverlay.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Khong tim thay file GoalOverlay.exe trong thu muc: $publishDir"
    exit 1
}

$exeSizeMB = [math]::Round(((Get-Item $exePath).Length / 1MB), 2)
Write-Host "-> Da tao thanh cong file thuc thi: $exePath ($exeSizeMB MB)" -ForegroundColor Green

# Tao ban Portable ZIP
if (-not (Test-Path $outputDir)) {
    [System.IO.Directory]::CreateDirectory($outputDir) | Out-Null
}
$zipPath = Join-Path $outputDir "GoalOverlay-v1.1.0-win-x64-portable.zip"
Write-Host "-> Dang dong goi ban Portable ZIP: $zipPath..." -ForegroundColor Gray
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force
$zipSizeMB = [math]::Round(((Get-Item $zipPath).Length / 1MB), 2)
Write-Host "-> Da dong goi xong ban Portable ZIP: $zipPath ($zipSizeMB MB)" -ForegroundColor Green

# 3. TIM KIEM TRINH BIEN DICH INNO SETUP (ISCC.exe)
Write-Host "`n[3/4] Dang tim kiem Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow

$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 5\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 5\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $isccPath) {
    Write-Host "`n[LUU Y] Chua phat hien Inno Setup Compiler (ISCC.exe) tren may cua ban." -ForegroundColor Magenta
    Write-Host "Ban co the cai dat nhanh Inno Setup 6 bang mot trong cac cach sau:" -ForegroundColor White
    Write-Host "  1. Lenh winget chinh xac:" -ForegroundColor White
    Write-Host "     winget install JRSoftware.InnoSetup" -ForegroundColor Cyan
    Write-Host "  2. Hoac tai truc tiep trinh cai dat chinh thuc tu:" -ForegroundColor White
    Write-Host "     https://jrsoftware.org/isdl.php" -ForegroundColor Cyan
    Write-Host "`nSau khi cai xong, ban co the chay lai script nay hoac mo file:" -ForegroundColor White
    Write-Host "    $issPath" -ForegroundColor Gray
    Write-Host "truc tiep trong Inno Setup Compiler va nhan Compile (Ctrl + F9)." -ForegroundColor White
    Write-Host "`n-> Ban Portable va file thuc thi doc lap da san sang tai: $outputDir" -ForegroundColor Green
    exit 0
}

Write-Host "-> Tim thay Inno Setup tai: $isccPath" -ForegroundColor Green

# 4. TIEN HANH DONG GOI SETUP INSTALLER
Write-Host "`n[4/4] Dang dong goi installer qua Inno Setup..." -ForegroundColor Yellow

if (-not (Test-Path $outputDir)) {
    [System.IO.Directory]::CreateDirectory($outputDir) | Out-Null
}

& "$isccPath" "$issPath"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup bien dich that bai!"
    exit 1
}

$setupExePath = Join-Path $outputDir "GoalOverlay-Setup.exe"
if (Test-Path $setupExePath) {
    $setupSizeMB = [math]::Round(((Get-Item $setupExePath).Length / 1MB), 2)
    Write-Host "`n=================================================================" -ForegroundColor Green
    Write-Host "   [OK] DONG GOI THANH CONG BO CAI DAT WINDOWS!                 " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "File cai dat : $setupExePath" -ForegroundColor Cyan
    Write-Host "Kich thuoc   : $setupSizeMB MB" -ForegroundColor Cyan
    Write-Host "Vi tri cai   : %LOCALAPPDATA%\Programs\GoalOverlay (Khong can admin)" -ForegroundColor White
} else {
    Write-Error "Khong tim thay file GoalOverlay-Setup.exe trong thu muc: $outputDir"
}
