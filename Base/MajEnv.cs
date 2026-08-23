using Microsoft.Data.Sqlite;
using Semver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace MajdataEdit_Neo.Base;

public static partial class MajEnv
{
    private const string ViewCompanyName = "bbben";
    private const string ViewProductName = "MajdataViewX";

    public static string MajBase => AppDomain.CurrentDomain.BaseDirectory;
    public static string GetPath(string relativePath) => Path.Combine(MajBase, relativePath);
    public static string UserDataDir { get; } = InitializeUserDataDir();

    private static string InitializeUserDataDir()
    {
        if (OperatingSystem.IsWindows())
            return MajBase;

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MajdataEdit-Neo");
        Directory.CreateDirectory(path);
        MigrateLegacyUserData(MajBase, path);
        return path;
    }

    private static void MigrateLegacyUserData(string legacyDir, string userDataDir)
    {
        foreach (var fileName in new[] { "Settings.json", "crash.log" })
        {
            var source = Path.Combine(legacyDir, fileName);
            var destination = Path.Combine(userDataDir, fileName);
            if (File.Exists(source) && !File.Exists(destination))
                File.Copy(source, destination);
        }

        MigrateLegacyDatabase(legacyDir, userDataDir);
        MigrateLegacyAutoSaves(legacyDir, userDataDir);
    }

    private static void MigrateLegacyDatabase(string legacyDir, string userDataDir)
    {
        var source = Path.Combine(legacyDir, "editor.db");
        var destination = Path.Combine(userDataDir, "editor.db");
        if (!File.Exists(source) || File.Exists(destination))
            return;

        var temporary = destination + $".migration-{Guid.NewGuid():N}";
        try
        {
            using (var sourceConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = source,
                           Mode = SqliteOpenMode.ReadWrite
                       }.ToString()))
            using (var destinationConnection = new SqliteConnection(
                       new SqliteConnectionStringBuilder
                       {
                           DataSource = temporary,
                           Mode = SqliteOpenMode.ReadWriteCreate
                       }.ToString()))
            {
                sourceConnection.Open();
                destinationConnection.Open();
                sourceConnection.BackupDatabase(destinationConnection);
            }

            File.Move(temporary, destination);
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
                File.Delete(temporary + suffix);
        }
    }

    private static void MigrateLegacyAutoSaves(string legacyDir, string userDataDir)
    {
        var legacyAutoSaveDir = Path.Combine(legacyDir, ".autosave");
        if (!Directory.Exists(legacyAutoSaveDir))
            return;

        var destinationAutoSaveDir = Path.Combine(userDataDir, ".autosave");
        foreach (var source in Directory.EnumerateFiles(
                     legacyAutoSaveDir,
                     "*",
                     SearchOption.AllDirectories))
        {
            if (Path.GetFileName(source) == ".index.json")
                continue;

            var destination = Path.Combine(
                destinationAutoSaveDir,
                Path.GetRelativePath(legacyAutoSaveDir, source));
            if (File.Exists(destination))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination);
        }

        var legacyIndexPath = Path.Combine(legacyAutoSaveDir, ".index.json");
        if (!File.Exists(legacyIndexPath))
            return;

        var destinationIndexPath = Path.Combine(destinationAutoSaveDir, ".index.json");
        var destinationIndex = File.Exists(destinationIndexPath)
            ? JsonNode.Parse(File.ReadAllText(destinationIndexPath))?.AsObject()
            : new JsonObject();
        var legacyIndex = JsonNode.Parse(File.ReadAllText(legacyIndexPath))?.AsObject();
        if (destinationIndex is null || legacyIndex?["FilesInfo"] is not JsonArray legacyFiles)
            throw new InvalidDataException("Invalid legacy auto-save index.");

        if (destinationIndex["FilesInfo"] is not JsonArray destinationFiles)
        {
            destinationFiles = new JsonArray();
            destinationIndex["FilesInfo"] = destinationFiles;
        }

        var indexedPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in destinationFiles)
        {
            if (entry?["FileName"]?.GetValue<string>() is { } path)
                indexedPaths.Add(path);
        }

        var legacyRoot = Path.GetFullPath(legacyAutoSaveDir) + Path.DirectorySeparatorChar;
        foreach (var entry in legacyFiles)
        {
            if (entry is not JsonObject fileInfo ||
                fileInfo["FileName"]?.GetValue<string>() is not { } sourcePath)
                continue;

            sourcePath = Path.GetFullPath(sourcePath);
            if (!sourcePath.StartsWith(legacyRoot, StringComparison.Ordinal))
                continue;

            var destinationPath = Path.Combine(
                destinationAutoSaveDir,
                Path.GetRelativePath(legacyAutoSaveDir, sourcePath));
            if (!File.Exists(destinationPath) || !indexedPaths.Add(destinationPath))
                continue;

            var migratedInfo = (JsonObject)fileInfo.DeepClone();
            migratedInfo["FileName"] = destinationPath;
            destinationFiles.Add(migratedInfo);
        }

        destinationIndex["Count"] = destinationFiles.Count;
        Directory.CreateDirectory(destinationAutoSaveDir);
        File.WriteAllText(destinationIndexPath, destinationIndex.ToJsonString());
    }
    public static string GetUserDataPath(string relativePath) => Path.Combine(UserDataDir, relativePath);

    public static string MajdataViewExecutableFile => OperatingSystem.IsMacOS()
        ? GetPath("../Helpers/MajdataViewX.app/Contents/MacOS/MajdataViewX")
        : GetPath(OperatingSystem.IsWindows() ? "MajdataViewX.exe" : "MajdataViewX");

    public static string MajdataViewPersistentDataPath
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                var appData = Directory.GetParent(localAppData)?.FullName
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return Path.Combine(
                    appData,
                    "LocalLow",
                    ViewCompanyName,
                    ViewProductName);
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    ViewCompanyName,
                    ViewProductName);
            }

            var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configHome))
            {
                configHome = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config");
            }
            return Path.Combine(
                configHome,
                "unity3d",
                ViewCompanyName,
                ViewProductName);
        }
    }

    public static string MmfAudioTimePath =>
        Path.Combine(MajdataViewPersistentDataPath, "majdata_time.dat");
    public const long MmfChartDataCapacity = 64 * 1024 * 1024; //64mb
    public static string MmfChartDataPath =>
        Path.Combine(MajdataViewPersistentDataPath, "majdata_chart.dat");

    public static string MajdataViewBassDllFile
    {
        get
        {
#if DEBUG
            if (OperatingSystem.IsWindows())
            {
                return GetPath("..\\..\\..\\runtimes\\win-x64\\native\\bass.dll");
            }
            else if (OperatingSystem.IsMacOS())
            {
                return GetPath("../../../runtimes/osx/native/libbass.dylib");
            }
            else if (OperatingSystem.IsLinux())
            {
                return GetPath("..\\..\\..\\runtimes\\linux-x64\\native\\libbass.so");
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform for MajdataViewBassDllFile.");
            }
#else
            if (OperatingSystem.IsWindows())
            {
                return GetPath("MajdataViewX_Data\\Plugins\\x86_64\\bass.dll");
            }
            else if (OperatingSystem.IsMacOS())
            {
                return GetPath("../Helpers/MajdataViewX.app/Contents/PlugIns/libbass.dylib");
            }
            else if (OperatingSystem.IsLinux())
            {
                return GetPath("MajdataViewX_Data/Plugins/x86_64/libbass.so");
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported platform for MajdataViewBassDllFile.");
            }
#endif
        }
    }

    public static string SettingsFile => GetUserDataPath("Settings.json");
    public static string CrashFile => GetUserDataPath("crash.log");
    public static string DatabaseFile => GetUserDataPath("editor.db");
    public static string GlobalAutoSaveDir => GetUserDataPath(".autosave");
    public static string CompletionFile => GetPath("completions.json");
    public static bool IsRecordingSupported => OperatingSystem.IsWindows();

    public static void ActivateProcessWindow(Process? process)
    {
        if (process == null || process.HasExited) return;

        if (OperatingSystem.IsWindows())
        {
            IntPtr hWnd = process.MainWindowHandle;
            if (hWnd != IntPtr.Zero)
            {
                // 9 = SW_RESTORE（如果被最小化，先还原）
                ShowWindow(hWnd, 9);
                SetForegroundWindow(hWnd);
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // 滚
        }
        else if (OperatingSystem.IsMacOS())
        {
            string script = $"tell application \"{process.ProcessName}\" to activate";
            Process.Start("osascript", $"-e \"{script}\"");
        }
    }

    //尽量少使用预编译，不指望到了每个平台再来纠正编译错误，只有必要场合/性能热点使用
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static readonly string MAJDATA_VERSION_STRING = $"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";
    public static readonly SemVersion MAJDATA_VERSION = SemVersion.Parse(MAJDATA_VERSION_STRING, SemVersionStyles.Any);
}
