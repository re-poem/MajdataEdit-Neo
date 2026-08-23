using MajdataEdit_Neo.Base;
using MajdataEdit_Neo.Utils;
using Microsoft.Data.Sqlite;
using System.Reflection;
using System.Text.Json;

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
    Directory.CreateDirectory(Path.Combine(migratedDir, ".autosave"));
    File.WriteAllText(Path.Combine(legacyDir, "Settings.json"), "legacy");
    File.WriteAllText(Path.Combine(migratedDir, "Settings.json"), "current");
    var legacyDatabase = Path.Combine(legacyDir, "editor.db");
    var migratedDatabase = Path.Combine(migratedDir, "editor.db");
    using (var connection = new SqliteConnection($"Data Source={legacyDatabase}"))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE MigrationTest (Value TEXT); INSERT INTO MigrationTest VALUES ('legacy');";
        command.ExecuteNonQuery();
    }

    var legacyAutoSave = Path.Combine(legacyDir, ".autosave", "chart", "maidata.txt");
    var currentAutoSave = Path.Combine(migratedDir, ".autosave", "autosave.current.txt");
    File.WriteAllText(legacyAutoSave, "autosave");
    File.WriteAllText(currentAutoSave, "current autosave");
    File.WriteAllText(
        Path.Combine(legacyDir, ".autosave", ".index.json"),
        JsonSerializer.Serialize(new
        {
            Count = 1,
            FilesInfo = new[] { new { FileName = legacyAutoSave, RawPath = "legacy", SavedTime = 1 } }
        }));
    File.WriteAllText(
        Path.Combine(migratedDir, ".autosave", ".index.json"),
        JsonSerializer.Serialize(new
        {
            Count = 1,
            FilesInfo = new[] { new { FileName = currentAutoSave, RawPath = "current", SavedTime = 2 } }
        }));

    var migrate = typeof(MajEnv).GetMethod(
        "MigrateLegacyUserData",
        BindingFlags.NonPublic | BindingFlags.Static);
    Check(migrate is not null, "MajEnv must migrate legacy non-Windows user data.");
    migrate!.Invoke(null, [legacyDir, migratedDir]);

    Check(
        File.ReadAllText(Path.Combine(migratedDir, "Settings.json")) == "current",
        "Migration must not overwrite current user data.");
    Check(
        ReadMigrationValue(migratedDatabase) == "legacy",
        "Migration must preserve the legacy edit database.");
    Check(
        File.ReadAllText(Path.Combine(migratedDir, ".autosave", "chart", "maidata.txt")) == "autosave",
        "Migration must preserve legacy global auto-saves.");

    using (var index = JsonDocument.Parse(
               File.ReadAllText(Path.Combine(migratedDir, ".autosave", ".index.json"))))
    {
        var files = index.RootElement.GetProperty("FilesInfo");
        Check(files.GetArrayLength() == 2, "Migration must merge legacy and current auto-save indexes.");
        Check(
            files.EnumerateArray().Any(file =>
                file.GetProperty("FileName").GetString() ==
                Path.Combine(migratedDir, ".autosave", "chart", "maidata.txt")),
            "Migration must rewrite legacy auto-save paths.");
    }

    using (var connection = new SqliteConnection($"Data Source={migratedDatabase}"))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE MigrationTest SET Value = 'current'";
        command.ExecuteNonQuery();
    }
    File.WriteAllText(legacyDatabase + "-wal", "stale");
    migrate.Invoke(null, [legacyDir, migratedDir]);
    Check(
        ReadMigrationValue(migratedDatabase) == "current" && !File.Exists(migratedDatabase + "-wal"),
        "Migration must never replay legacy SQLite sidecars after the database is migrated.");
}
finally
{
    if (Directory.Exists(migrationRoot))
        Directory.Delete(migrationRoot, recursive: true);
}

static string ReadMigrationValue(string path)
{
    using var connection = new SqliteConnection($"Data Source={path}");
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT Value FROM MigrationTest";
    return (string)command.ExecuteScalar()!;
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
