using Avalonia;
using Avalonia.Threading;
using MajdataEdit_Neo.Utils;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MajdataEdit_Neo;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => LogCrashed((Exception)e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (sender, e) => LogCrashed(e.Exception);

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    static void LogCrashed(Exception ex)
    {
        File.WriteAllText("crash.log", $"{ex.Message}\n{ex.StackTrace}\n\n" +
            $"inner:{ex.InnerException?.Message}\n{ex.InnerException?.StackTrace}");
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
