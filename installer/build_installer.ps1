# ==============================================================================
# Script tự động Publish Release và Đóng gói Inno Setup Installer cho GoalOverlay
# ==============================================================================
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   🎯 GoalOverlay - Tự động Build Release & Đóng gói Installer   " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$scriptRoot = $PSScriptRoot
$projectDir = Resolve-Path "$scriptRoot\..\src\GoalOverlay"
$publishDir = "$projectDir\bin\Release\net8.0-windows\win-x64\publish"
$issPath = "$scriptRoot\GoalOverlay.iss"
$outputDir = "$scriptRoot\output"

# 1. BIÊN DỊCH VÀ XUẤT BẢN RELEASE SINGLE-FILE SELF-CONTAINED
Write-Host "`n[1/3] Đang biên dịch Release x64 Single-file (Self-contained)..." -ForegroundColor Yellow

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
    Write-Error "Quá trình dotnet publish thất bại! Vui lòng kiểm tra lại log lỗi phía trên."
    exit 1
}

$exePath = Join-Path $publishDir "GoalOverlay.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Không tìm thấy file GoalOverlay.exe trong thư mục: $publishDir"
    exit 1
}

$exeSizeMB = [math]::Round(((Get-Item $exePath).Length / 1MB), 2)
Write-Host "-> Đã tạo thành công file thực thi: $exePath ($exeSizeMB MB)" -ForegroundColor Green

# 2. TÌM KIẾM TRÌNH BIÊN DỊCH INNO SETUP (ISCC.exe)
Write-Host "`n[2/3] Đang tìm kiếm Inno Setup Compiler (ISCC.exe)..." -ForegroundColor Yellow

$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)

$isccPath = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $isccPath) {
    Write-Host "`n[LƯU Ý] Chưa phát hiện Inno Setup Compiler (ISCC.exe) trên máy của bạn." -ForegroundColor Magenta
    Write-Host "Bạn có thể cài đặt nhanh Inno Setup bằng lệnh:" -ForegroundColor White
    Write-Host "    winget install JR.InnoSetup" -ForegroundColor Cyan
    Write-Host "`nSau khi cài xong, bạn có thể chạy lại script này hoặc mở file:" -ForegroundColor White
    Write-Host "    $issPath" -ForegroundColor Gray
    Write-Host "trực tiếp trong Inno Setup Compiler và nhấn Compile (Ctrl + F9)." -ForegroundColor White
    Write-Host "`n-> File thực thi độc lập GoalOverlay.exe đã sẵn sàng tại: $exePath" -ForegroundColor Green
    exit 0
}

Write-Host "-> Tìm thấy Inno Setup tại: $isccPath" -ForegroundColor Green

# 3. TIẾN HÀNH ĐÓNG GÓI SETUP INSTALLER
Write-Host "`n[3/3] Đang đóng gói installer qua Inno Setup..." -ForegroundColor Yellow

if (-not (Test-Path $outputDir)) {
    [System.IO.Directory]::CreateDirectory($outputDir) | Out-Null
}

& "$isccPath" "$issPath"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Inno Setup biên dịch thất bại!"
    exit 1
}

$setupExePath = Join-Path $outputDir "GoalOverlay-Setup.exe"
if (Test-Path $setupExePath) {
    $setupSizeMB = [math]::Round(((Get-Item $setupExePath).Length / 1MB), 2)
    Write-Host "`n=================================================================" -ForegroundColor Green
    Write-Host "   🎉 ĐÓNG GÓI THÀNH CÔNG BỘ CÀI ĐẶT WINDOWS!                   " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Green
    Write-Host "File cài đặt : $setupExePath" -ForegroundColor Cyan
    Write-Host "Kích thước   : $setupSizeMB MB" -ForegroundColor Cyan
    Write-Host "Vị trí cài   : %LOCALAPPDATA%\Programs\GoalOverlay (Không cần admin)" -ForegroundColor White
} else {
    Write-Error "Không tìm thấy file GoalOverlay-Setup.exe trong thư mục: $outputDir"
}
