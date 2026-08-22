# macOS native build

## Requirements

- .NET 10 SDK
- A built `MajdataViewX.app`
- `SFX` and `Skin` from the matching MajdataX release

## Package

```sh
macOS/package.sh /path/to/MajdataViewX.app /path/to/runtime-assets
```

The script publishes the Apple Silicon editor, nests the Apple Silicon viewer at `Contents/Helpers/MajdataViewX.app`, copies the runtime assets, applies an ad-hoc signature, and writes `artifacts/MajdataEdit-Neo.app`.

On macOS, settings, the editor database, and global auto-saves use the per-user application-data directory rather than the read-only app bundle. Video export is hidden because the recording plugin is Windows-only.
