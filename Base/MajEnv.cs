using Semver;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MajdataEdit_Neo.Base;

public static partial class MajEnv
{
    public static string MajBase => AppDomain.CurrentDomain.BaseDirectory;
    public static string GetPath(string relativePath) => Path.Combine(MajBase, relativePath);

    public static string MmfAudioTimePath =>
        GetPath("majdata_time.dat");

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
                return GetPath("..\\..\\..\\runtimes\\osx\\native\\libbass.dylib");
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
                return GetPath("MajdataViewX_Data/Plugins/x86_64/libbass.dylib");
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

    public static string SettingsFile => GetPath("Settings.json");
    public static string CrashFile => GetPath("crash.log");
    public static string DatabaseFile => GetPath("editor.db");
    public static string CompletionFile => GetPath("completions.json");

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

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static readonly string MAJDATA_VERSION_STRING = $"v{Assembly.GetExecutingAssembly().GetName().Version!.ToString(3)}";
    public static readonly SemVersion MAJDATA_VERSION = SemVersion.Parse(MAJDATA_VERSION_STRING, SemVersionStyles.Any);
}
