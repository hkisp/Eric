using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace IDCXray;

public partial class App : System.Windows.Application
{
    private static readonly object LogLock = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IDCXray", "app.log");

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            LogException("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogException("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            LogException("Startup", ex);
            System.Windows.MessageBox.Show(
                $"Eric agent 启动失败。诊断信息已写入：\n{LogPath}\n\n{ex.GetType().Name}: {MaskSecrets(ex.Message)}",
                "Eric agent", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException("DispatcherUnhandledException", e.Exception);
        e.Handled = true;
        System.Windows.MessageBox.Show(
            $"Eric agent 遇到错误，详细信息已写入本地日志：\n{LogPath}\n\n{e.Exception.GetType().Name}: {MaskSecrets(e.Exception.Message)}",
            "Eric agent", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static void LogException(string source, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var details = exception?.ToString() ?? "Exception object unavailable.";
            var entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{source}]\r\n{MaskSecrets(details)}\r\n\r\n";
            lock (LogLock) File.AppendAllText(LogPath, entry);
        }
        catch
        {
            // Logging failures must not recursively crash application startup.
        }
    }

    private static string MaskSecrets(string value)
    {
        value = Regex.Replace(value, @"(?i)\b(vless|vmess|trojan|ss)://[^\s""'<>]+", "$1://[REDACTED]");
        value = Regex.Replace(value, @"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b", "[UUID]");
        value = Regex.Replace(value, @"(?i)(""(?:pbk|publicKey|password|passwd|uuid|id|sid|shortId|token|secret)""\s*:\s*)""[^""]*""", "$1\"[REDACTED]\"");
        value = Regex.Replace(value, @"(?i)\b(pbk|publicKey|password|passwd|uuid|id|sid|shortId|token|secret)\s*[:=]\s*[^\s&,;]+", "$1=[REDACTED]");
        return value;
    }
}
