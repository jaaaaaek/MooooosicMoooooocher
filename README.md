# MooooosicMoooooocher

For all your music downloading needs. Supports .wav and .mp3 files currently

## Features

- Batch download from a queue of URLs. 
- Skips previously downloaded tracks within the same directory. 
- Relies on yt-dlp and FFmpeg. Downloads them on first run. 
- Cross-platform (Windows & macOS)

## Getting Started

1. Download the latest release for your platform
2. Run the app — it will download yt-dlp and FFmpeg automatically on first launch
3. Set your output folder
4. Paste URLs, add them to the queue, and start downloading

WAV downloads require an auth token, which can be set in Settings.

## Building from Source

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build MooooosicMoooooocher/MooooosicMoooooocher.csproj
```

To publish a self-contained release:

```bash
dotnet publish MooooosicMoooooocher/MooooosicMoooooocher.csproj -r win-x64 -c Release
```
