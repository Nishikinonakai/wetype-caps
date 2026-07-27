using System.Runtime.InteropServices;
using System.Text.Json;

namespace WeTypeCaps;

internal static class Program
{
    private const string MutexName = @"Local\WeTypeCaps-607FDF85-FCC8-4DBD-A365-41296F980C9C";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        string configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        AppConfig config;
        try
        {
            config = AppConfig.LoadOrCreate(configPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法读取配置：{ex.Message}",
                "WeType Caps",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 2;
        }

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            AttachToParentConsole();
            return SelfTests.Run();
        }

        if (args.Contains("--diagnose", StringComparer.OrdinalIgnoreCase))
        {
            AttachToParentConsole();
            return RunDiagnostics(config);
        }

        if (args.Contains("--sendinput-test", StringComparer.OrdinalIgnoreCase))
        {
            AttachToParentConsole();
            bool ok = KeyboardRemapper.SendCtrlSpaceForDiagnostics();
            Console.WriteLine(ok ? "SendInput Ctrl+Space: 4/4" : "SendInput Ctrl+Space: FAILED");
            return ok ? 0 : 1;
        }

        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("WeType Caps 已在运行。", "WeType Caps");
            return 0;
        }

        try
        {
            Application.Run(new TrayApplicationContext(configPath, config));
            return 0;
        }
        catch (Exception ex)
        {
            AppLog.Write(ex.ToString());
            MessageBox.Show(
                $"程序异常退出：{ex.Message}\n\n日志：{AppLog.Path}",
                "WeType Caps",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int RunDiagnostics(AppConfig config)
    {
        try
        {
            var identity = TsfProfileDetector.ResolveWeTypeIdentity(config);
            using var tsf = new TsfProfileDetector(identity);
            ActiveProfile active = tsf.GetActiveProfile();
            ForegroundState foreground = FullscreenDetector.Inspect(config);
            var report = new
            {
                Time = DateTimeOffset.Now,
                Config = config,
                WeTypeIdentity = identity,
                ActiveProfile = active,
                Foreground = new
                {
                    WindowHandle = $"0x{foreground.WindowHandle:X}",
                    foreground.ProcessId,
                    foreground.ProcessName,
                    foreground.WindowClass,
                    foreground.IsFullscreen,
                    foreground.IsSelectedProcess,
                    foreground.IsAlwaysBypassed
                },
                WouldCaptureCapsLock =
                    active.IsMatch(identity) &&
                    !(config.BypassFullscreen && foreground.IsFullscreenAndSelected)
            };
            string json = JsonSerializer.Serialize(report, AppConfig.JsonOptions);
            Console.WriteLine(json);
            AppLog.Write("Diagnostics:" + Environment.NewLine + json);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            AppLog.Write(ex.ToString());
            return 1;
        }
    }

    private static void AttachToParentConsole()
    {
        if (!NativeMethods.AttachConsole(NativeMethods.AttachParentProcess))
        {
            return;
        }

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
    }
}
