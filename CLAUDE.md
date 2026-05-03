# MooooosicMoooooocher

Cross-platform Avalonia desktop app for batch-downloading audio from URLs (YouTube, SoundCloud, etc.) as MP3 or WAV. Auto-downloads yt-dlp and FFmpeg on first run. Successor / GUI companion to LinkFormatter.

## Stack

- **Language:** C# .NET 10.0
- **UI:** Avalonia 11.2.0 (WinExe / GUI, MVVM pattern)
- **Extra:** AnimatedImage.Avalonia, System.Configuration
- **External tools:** yt-dlp, FFmpeg (downloaded at runtime if missing)

## Build / Run

```bash
# Debug
dotnet build MooooosicMoooooocher/MooooosicMoooooocher.csproj

# Self-contained release (Windows)
dotnet publish -r win-x64 -c Release
```

## Entry points

- [MooooosicMoooooocher/Program.cs](MooooosicMoooooocher/Program.cs) — `Main()`
- [MooooosicMoooooocher/App.axaml.cs](MooooosicMoooooocher/App.axaml.cs) — app bootstrap
- `ViewModels/`, `Models/`, `Views/` — MVVM structure

## Notes / Quirks

- **WAV downloads need a SoundCloud auth token** stored in the app's settings UI.
- Per-directory dedup: the app remembers previously-downloaded tracks in a given destination and skips them on subsequent runs.
- First launch pulls yt-dlp + FFmpeg binaries automatically — be online.
- Uses .NET 10 (bleeding edge — verify the SDK is installed with `dotnet --list-sdks`).
