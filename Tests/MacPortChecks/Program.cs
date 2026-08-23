using MajdataEdit_Neo.Base;
using MajdataEdit_Neo.Utils;
using System.Reflection;

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

var migrationRoot = Path.Combine(Path.GetTempPath(), $"majdata-migration-{Guid.NewGuid():N}");
var legacyDir = Path.Combine(migrationRoot, "legacy");
var migratedDir = Path.Combine(migrationRoot, "migrated");
try
{
    Directory.CreateDirectory(Path.Combine(legacyDir, ".autosave", "chart"));
    Directory.CreateDirectory(migratedDir);
    File.WriteAllText(Path.Combine(legacyDir, "Settings.json"), "legacy");
    File.WriteAllText(Path.Combine(migratedDir, "Settings.json"), "current");
    File.WriteAllText(Path.Combine(legacyDir, "editor.db"), "database");
    File.WriteAllText(Path.Combine(legacyDir, ".autosave", "chart", "maidata.txt"), "autosave");

    var migrate = typeof(MajEnv).GetMethod(
        "MigrateLegacyUserData",
        BindingFlags.NonPublic | BindingFlags.Static);
    Check(migrate is not null, "MajEnv must migrate legacy non-Windows user data.");
    migrate!.Invoke(null, [legacyDir, migratedDir]);

    Check(
        File.ReadAllText(Path.Combine(migratedDir, "Settings.json")) == "current",
        "Migration must not overwrite current user data.");
    Check(
        File.ReadAllText(Path.Combine(migratedDir, "editor.db")) == "database",
        "Migration must preserve the legacy edit database.");
    Check(
        File.ReadAllText(Path.Combine(migratedDir, ".autosave", "chart", "maidata.txt")) == "autosave",
        "Migration must preserve legacy global auto-saves.");
}
finally
{
    if (Directory.Exists(migrationRoot))
        Directory.Delete(migrationRoot, recursive: true);
}

if (OperatingSystem.IsMacOS())
{
    Check(
        FFmpegChecker.ExecutablePath == "/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg",
        "macOS must use Homebrew ffmpeg-full directly because it is keg-only.");
    Check(
        FFmpegChecker.MissingMessage.Contains("brew install ffmpeg-full", StringComparison.Ordinal),
        "The missing FFmpeg message must include the Homebrew install command.");

    var viewApp = Path.GetFullPath(Path.Combine(MajEnv.MajBase, "..", "Helpers", "MajdataViewX.app"));
    Check(
        Path.GetFullPath(MajEnv.MajdataViewExecutableFile) ==
        Path.Combine(viewApp, "Contents", "MacOS", "MajdataViewX"),
        "The editor must launch the bundled MajdataViewX helper app.");
    Check(
#if DEBUG
        Path.GetFullPath(MajEnv.MajdataViewBassDllFile) ==
        Path.GetFullPath(Path.Combine(MajEnv.MajBase, "../../../runtimes/osx/native/libbass.dylib")),
#else
        Path.GetFullPath(MajEnv.MajdataViewBassDllFile) ==
        Path.Combine(viewApp, "Contents", "PlugIns", "libbass.dylib"),
#endif
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
