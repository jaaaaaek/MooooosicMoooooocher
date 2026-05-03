# MooooosicMoooooocher

An Avalonia desktop app for downloading lots of music!

## Features

- Download individual songs in mp3 or wav format!
- Download a bunch of songs if you paste multiple links! 
- Download ALL your likes if you paste your profile link (like this: soundcloud.com/yourusernamehere)
- Download your playlists too! (https://soundcloud.com/yourusername/sets/yourplaylistname)
- Logs downloaded songs per directory so previously downloaded songs aren't downloaded again
- Depends on and auto-downloads yt-dlp and FFmpeg on first run (with neat download animation)
- Cross-platform! (Windows & macOS)

## Getting Started

1. Download the latest release for your platform in the releases tab of this repo
2. Run the app, it will download yt-dlp and FFmpeg automatically on first launch
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
