param(
    [string]$AppDataPath = [Environment]::GetFolderPath("ApplicationData")
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$musicFolder = Join-Path $repoRoot "Music"
$settingsPath = Join-Path $AppDataPath "SoundCloudDownloader\\appsettings.json"
$ffmpegPath = Join-Path $musicFolder "ffmpeg.exe"

Write-Host "Resetting first-run state..." -ForegroundColor Cyan

if (Test-Path $settingsPath) {
    Remove-Item -Path $settingsPath -Force
    Write-Host "Deleted settings: $settingsPath"
} else {
    Write-Host "Settings file not found (ok): $settingsPath"
}

if (Test-Path $ffmpegPath) {
    Remove-Item -Path $ffmpegPath -Force
    Write-Host "Deleted ffmpeg: $ffmpegPath"
} else {
    Write-Host "ffmpeg.exe not found (ok): $ffmpegPath"
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run the app (ScDownloader) and wait for the welcome screen to finish."
Write-Host "2. Verify ffmpeg.exe exists at: $ffmpegPath"
