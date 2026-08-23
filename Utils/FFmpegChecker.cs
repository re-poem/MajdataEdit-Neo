using System;
using System.Diagnostics;
using System.Threading.Tasks;
using MsBox.Avalonia.Enums;

namespace MajdataEdit_Neo.Utils;

public static class FFmpegChecker
{
    public static string ExecutablePath => OperatingSystem.IsMacOS()
        ? "/opt/homebrew/opt/ffmpeg-full/bin/ffmpeg"
        : "ffmpeg";

    public static string MissingMessage => OperatingSystem.IsMacOS()
        ? $"{Assets.Langs.Langs.Status_NoFfmpeg}\n\nHomebrew:\nbrew install ffmpeg-full"
        : Assets.Langs.Langs.Status_NoFfmpeg;

    public static async Task<bool> EnsureFFmpeg()
    {
        try
        {
            using var process = new Process();
            process.StartInfo.FileName = ExecutablePath;
            process.StartInfo.Arguments = "-version";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // 重定向输出，防止组件版本信息污染控制台
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            // 启动进程
            process.Start();

            // 异步等待进程结束（设置超时时间防止挂起）
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                await process.WaitForExitAsync(cts.Token);
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FFmpeg availability check failed: {ex}");
            // if (ex is Win32Exception winEx && winEx.NativeErrorCode == 2) //no ffmpeg
            await MessageBox.ShowWindowDialogAsync(
                MissingMessage,
                Assets.Langs.Langs.Gui_Error,
                ButtonEnum.Ok, Icon.Error);
            return false;
        }
    }
}
