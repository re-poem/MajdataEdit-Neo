# macOS native build

## Requirements

- .NET 10 SDK
- A built `MajdataViewX.app`
- `SFX` and `Skin` from the matching MajdataX release
- Homebrew `ffmpeg-full` (`brew install ffmpeg-full`)

## Package

```sh
macOS/package.sh /path/to/MajdataViewX.app /path/to/runtime-assets
```

The script publishes the Apple Silicon editor, nests the Apple Silicon viewer at `Contents/Helpers/MajdataViewX.app`, copies the runtime assets, applies an ad-hoc signature, and writes `artifacts/MajdataEdit-Neo.app`.

On macOS, settings, the editor database, and global auto-saves use the per-user application-data directory rather than the read-only app bundle. Missing files are copied from the legacy executable directory on first run without replacing newer user data. Video export is hidden because the recording plugin is Windows-only.

Media tools use `/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg` directly because the Homebrew formula is keg-only. If it is missing, the editor shows the Homebrew installation command.
