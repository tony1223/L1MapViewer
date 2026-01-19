using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Reflection;

namespace L1MapViewer.Helper;

/// <summary>
/// 全域閃退報告工具 - 捕捉未處理的例外並記錄
/// 閃退報告會寫入 %TEMP%\L1MapViewer_crash.log
/// </summary>
public static class CrashReporter
{
    private static readonly string _crashLogPath = Path.Combine(Path.GetTempPath(), "L1MapViewer_crash.log");
    private static readonly object _lock = new object();
    private static bool _initialized = false;

    /// <summary>
    /// 初始化閃退報告機制（在程式啟動時呼叫一次）
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // 捕捉所有未處理的例外
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        // 捕捉 Task 未觀察到的例外
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        DebugLog.Log("[CrashReporter] Initialized");
    }

    /// <summary>
    /// 閃退日誌檔案路徑
    /// </summary>
    public static string CrashLogPath => _crashLogPath;

    /// <summary>
    /// 手動報告例外（用於 try-catch 區塊）
    /// </summary>
    public static void ReportException(Exception ex, string context = "")
    {
        WriteCrashLog(ex, context, isFatal: false);
    }

    /// <summary>
    /// 安全執行動作，捕捉並記錄任何例外
    /// </summary>
    public static void SafeExecute(Action action, string context = "")
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
            throw; // 重新拋出讓呼叫者知道
        }
    }

    /// <summary>
    /// 安全執行動作，捕捉並記錄任何例外（不重新拋出）
    /// </summary>
    public static bool TryExecute(Action action, string context = "")
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
            return false;
        }
    }

    /// <summary>
    /// 安全執行有回傳值的動作
    /// </summary>
    public static T? SafeExecute<T>(Func<T> func, string context = "", T? defaultValue = default)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            ReportException(ex, context);
            return defaultValue;
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        WriteCrashLog(ex, "UnhandledException", isFatal: e.IsTerminating);

        if (e.IsTerminating)
        {
            ShowCrashDialog(ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrashLog(e.Exception, "UnobservedTaskException", isFatal: false);
        e.SetObserved(); // 防止程式崩潰
    }

    private static void WriteCrashLog(Exception? ex, string context, bool isFatal)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"[CRASH REPORT] {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Context: {context}");
            sb.AppendLine($"Fatal: {isFatal}");
            sb.AppendLine($"Version: {GetVersion()}");
            sb.AppendLine($"OS: {Environment.OSVersion}");
            sb.AppendLine($"CLR: {Environment.Version}");
            sb.AppendLine("--------------------------------------------------------------------------------");

            if (ex != null)
            {
                sb.AppendLine($"Exception Type: {ex.GetType().FullName}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine();
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(ex.StackTrace ?? "(no stack trace)");

                // 記錄內部例外
                var innerEx = ex.InnerException;
                int depth = 0;
                while (innerEx != null && depth < 5)
                {
                    sb.AppendLine();
                    sb.AppendLine($"--- Inner Exception ({depth + 1}) ---");
                    sb.AppendLine($"Type: {innerEx.GetType().FullName}");
                    sb.AppendLine($"Message: {innerEx.Message}");
                    sb.AppendLine("Stack Trace:");
                    sb.AppendLine(innerEx.StackTrace ?? "(no stack trace)");
                    innerEx = innerEx.InnerException;
                    depth++;
                }

                // AggregateException 特殊處理
                if (ex is AggregateException aggEx)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- Aggregate Exceptions ---");
                    foreach (var inner in aggEx.InnerExceptions)
                    {
                        sb.AppendLine($"  - {inner.GetType().Name}: {inner.Message}");
                    }
                }
            }
            else
            {
                sb.AppendLine("(Exception object is null)");
            }

            sb.AppendLine("================================================================================");
            sb.AppendLine();

            lock (_lock)
            {
                File.AppendAllText(_crashLogPath, sb.ToString());
            }

            // 同時寫入 DebugLog
            DebugLog.Log($"[CRASH] {context}: {ex?.GetType().Name}: {ex?.Message}");
        }
        catch
        {
            // 忽略寫入錯誤
        }
    }

    private static void ShowCrashDialog(Exception? ex)
    {
        try
        {
            var message = $"程式發生未預期的錯誤，即將關閉。\n\n" +
                         $"錯誤類型: {ex?.GetType().Name}\n" +
                         $"錯誤訊息: {ex?.Message}\n\n" +
                         $"詳細閃退報告已儲存至:\n{_crashLogPath}\n\n" +
                         $"Debug 日誌:\n{DebugLog.LogPath}";

            // 嘗試使用系統原生訊息框
            if (OperatingSystem.IsWindows())
            {
                System.Windows.MessageBox.Show(message, "L1MapViewer 閃退報告",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            else
            {
                // 其他平台輸出到 Console
                Console.WriteLine("\n" + new string('=', 60));
                Console.WriteLine("L1MapViewer 閃退報告");
                Console.WriteLine(new string('=', 60));
                Console.WriteLine(message);
                Console.WriteLine(new string('=', 60) + "\n");
            }
        }
        catch
        {
            // 如果連對話框都無法顯示，至少輸出到 Console
            Console.WriteLine($"[FATAL CRASH] {ex?.GetType().Name}: {ex?.Message}");
            Console.WriteLine($"Crash log: {_crashLogPath}");
        }
    }

    private static string GetVersion()
    {
        try
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// 清除舊的閃退日誌（可選，在程式啟動時呼叫）
    /// </summary>
    public static void ClearOldLogs(int keepDays = 7)
    {
        try
        {
            if (File.Exists(_crashLogPath))
            {
                var fileInfo = new FileInfo(_crashLogPath);
                // 如果檔案超過指定天數或超過 10MB，就清除
                if (fileInfo.LastWriteTime < DateTime.Now.AddDays(-keepDays) ||
                    fileInfo.Length > 10 * 1024 * 1024)
                {
                    File.Delete(_crashLogPath);
                    DebugLog.Log("[CrashReporter] Old crash log cleared");
                }
            }
        }
        catch
        {
            // 忽略錯誤
        }
    }
}
