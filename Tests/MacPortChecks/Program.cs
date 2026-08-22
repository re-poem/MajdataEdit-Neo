using MajdataEdit_Neo.Base;

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

var userDataDir = OperatingSystem.IsWindows()
    ? MajEnv.MajBase
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MajdataEdit-Neo");

Check(
    MajEnv.SettingsFile == Path.Combine(userDataDir, "Settings.json"),
    "Settings must use the per-user writable data directory.");
Check(
    MajEnv.DatabaseFile == Path.Combine(userDataDir, "editor.db"),
    "The edit database must use the per-user writable data directory.");
Check(
    MajEnv.GlobalAutoSaveDir == Path.Combine(userDataDir, ".autosave"),
    "Global auto-saves must use the per-user writable data directory.");
Check(
    MajEnv.IsRecordingSupported == OperatingSystem.IsWindows(),
    "Video recording must be hidden on unsupported platforms.");

if (OperatingSystem.IsMacOS())
{
    var viewApp = Path.GetFullPath(Path.Combine(MajEnv.MajBase, "..", "Helpers", "MajdataViewX.app"));
    Check(
        Path.GetFullPath(MajEnv.MajdataViewExecutableFile) ==
        Path.Combine(viewApp, "Contents", "MacOS", "MajdataViewX"),
        "The editor must launch the bundled MajdataViewX helper app.");
    Check(
        Path.GetFullPath(MajEnv.MajdataViewBassDllFile) ==
        Path.Combine(viewApp, "Contents", "PlugIns", "libbass.dylib"),
        "The editor must load BASS from the bundled MajdataViewX helper app.");

    var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    Check(
        File.Exists(Path.Combine(repoRoot, "Properties", "PublishProfiles", "macOS-arm64.pubxml")),
        "The repository must include the macOS arm64 publish profile.");
    Check(
        File.Exists(Path.Combine(repoRoot, "macOS", "Info.plist")),
        "The repository must include the macOS app metadata.");
    Check(
        File.Exists(Path.Combine(repoRoot, "macOS", "package.sh")),
        "The repository must include the macOS app packaging script.");
}
