using System.Text;
using System.IO;
using L1FlyMapViewer;
using L1MapViewer;
using L1MapViewer.CLI;
using L1MapViewer.Helper;
using L1MapViewer.Localization;
using System.Diagnostics;
using Eto;
using Eto.Forms;
using Path = System.IO.Path;
using File = System.IO.File;

namespace L1MapViewerCore;

static class Program
{
    // 效能 Log 開關（供 MapForm 讀取）
    public static bool PerfLogEnabled { get; private set; } = false;

    // 全域啟動計時器
    public static Stopwatch StartupStopwatch { get; } = Stopwatch.StartNew();

    [STAThread]
    static int Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // 初始化閃退報告機制（最優先）
        CrashReporter.Initialize();
        CrashReporter.ClearOldLogs();

        // 初始化 Debug Log（每次啟動清除舊 log）
        DebugLog.Clear();
        DebugLog.Log($"[PROGRAM] Version: {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}");
        DebugLog.Log($"[PROGRAM] Args: {string.Join(" ", args)}");

        // 檢查是否啟用效能 Log
        var argsList = args.ToList();
        if (argsList.Contains("--perf-log"))
        {
            PerfLogEnabled = true;
            argsList.Remove("--perf-log");
            args = argsList.ToArray();
            LogPerf("[PROGRAM] PerfLog enabled");
        }

        // 檢查是否為 CLI 模式
        if (args.Length > 0 && args[0].ToLower() == "-cli")
        {
            return CliHandler.Execute(args);
        }

        // GUI 模式
        LogPerf("[PROGRAM] Starting GUI mode");

        // 初始化多語言支援
        LogPerf("[PROGRAM] Initializing localization...");
        LocalizationManager.Initialize();
        LogPerf("[PROGRAM] Localization initialized: " + LocalizationManager.CurrentLanguage);

        // 初始化 Eto.Forms 平台
        LogPerf("[PROGRAM] Initializing Eto.Forms platform...");
        Platform platform;
        try
        {
            platform = Platform.Detect;
        }
        catch (InvalidOperationException)
        {
            // Platform.Detect failed, try to load platform manually based on OS
            var basePath = AppContext.BaseDirectory;
            if (OperatingSystem.IsMacOS())
            {
                // Try native macOS platform first, fall back to GTK
                try
                {
                    var assemblyPath = Path.Combine(basePath, "Eto.macOS.dll");
                    if (File.Exists(assemblyPath))
                    {
                        var macPlatformAssembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
                        var platformType = macPlatformAssembly.GetType("Eto.Mac.Platform");
                        platform = (Platform)Activator.CreateInstance(platformType!)!;
                    }
                    else
                    {
                        throw new FileNotFoundException("Eto.macOS.dll not found");
                    }
                }
                catch (Exception ex)
                {
                    // Fall back to GTK on macOS (requires GTK installed via Homebrew: brew install gtk+3)
                    Console.WriteLine($"Note: Native macOS platform failed ({ex.GetType().Name}). Trying GTK backend...");
                    Console.WriteLine("For native look, install: sudo dotnet workload install macos");
                    Console.WriteLine("For GTK backend, install: brew install gtk+3");

                    try
                    {
                        var gtkPath = Path.Combine(basePath, "Eto.Gtk.dll");
                        var gtkPlatformAssembly = System.Reflection.Assembly.LoadFrom(gtkPath);
                        var platformType = gtkPlatformAssembly.GetType("Eto.GtkSharp.Platform");
                        platform = (Platform)Activator.CreateInstance(platformType!)!;
                    }
                    catch (Exception gtkEx)
                    {
                        Console.WriteLine();
                        Console.WriteLine("ERROR: Could not initialize any UI platform on macOS.");
                        Console.WriteLine();
                        Console.WriteLine("Please install ONE of the following:");
                        Console.WriteLine("  Option 1 (Native): sudo dotnet workload install macos");
                        Console.WriteLine("  Option 2 (GTK):    brew install gtk+3");
                        Console.WriteLine();
                        throw new InvalidOperationException($"No UI platform available. Native: {ex.Message}, GTK: {gtkEx.Message}");
                    }
                }
            }
            else if (OperatingSystem.IsWindows())
            {
                var assemblyPath = Path.Combine(basePath, "Eto.Wpf.dll");
                var wpfPlatformAssembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
                var platformType = wpfPlatformAssembly.GetType("Eto.Wpf.Platform");
                platform = (Platform)Activator.CreateInstance(platformType!)!;
            }
            else if (OperatingSystem.IsLinux())
            {
                var assemblyPath = Path.Combine(basePath, "Eto.Gtk.dll");
                var gtkPlatformAssembly = System.Reflection.Assembly.LoadFrom(assemblyPath);
                var platformType = gtkPlatformAssembly.GetType("Eto.GtkSharp.Platform");
                platform = (Platform)Activator.CreateInstance(platformType!)!;
            }
            else
            {
                throw;
            }
        }
        LogPerf($"[PROGRAM] Platform: {platform.ID}");

        using var app = new Application(platform);
        LogPerf("[PROGRAM] Eto Application created");

        // 捕捉 Eto.Forms 的未處理例外
        app.UnhandledException += (sender, e) =>
        {
            CrashReporter.ReportException(e.ExceptionObject as Exception, "Eto.UnhandledException");
            DebugLog.Log($"[PROGRAM] Eto UnhandledException: {e.ExceptionObject}");
        };

        try
        {
            LogPerf("[PROGRAM] Creating MapForm...");
            var form = new MapForm();
            LogPerf("[PROGRAM] MapForm created");

            LogPerf("[PROGRAM] Application.Run() starting...");
            app.Run(form);
            return 0;
        }
        catch (Exception ex)
        {
            CrashReporter.ReportException(ex, "Application.Run");
            DebugLog.Log($"[PROGRAM] Fatal exception in Application.Run: {ex}");
            throw;
        }
    }

    public static void LogPerf(string message)
    {
        if (!PerfLogEnabled) return;
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string elapsed = $"+{StartupStopwatch.ElapsedMilliseconds}ms";
        Console.WriteLine($"{timestamp} {elapsed,-10} {message}");
    }
}
