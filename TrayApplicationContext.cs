using Microsoft.Win32;
using System.Diagnostics;

namespace WeTypeCaps;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private const string StartupKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "WeTypeCaps";

    private readonly string _configPath;
    private AppConfig _config;
    private TsfProfileIdentity _identity;
    private readonly TsfProfileDetector _tsf;
    private readonly KeyboardRemapper _remapper;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly System.Windows.Forms.Timer _stateTimer;
    private bool _manualEnabled = true;
    private bool _captureEnabled;
    private ActiveProfile _activeProfile;
    private ForegroundState _foreground;

    internal TrayApplicationContext(string configPath, AppConfig config)
    {
        _configPath = configPath;
        _config = config;
        _identity = TsfProfileDetector.ResolveWeTypeIdentity(config);
        _tsf = new TsfProfileDetector(_identity);

        _statusItem = new ToolStripMenuItem("正在检测…") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("暂停");
        _pauseItem.Click += (_, _) => TogglePause();
        _startupItem = new ToolStripMenuItem("开机启动")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = false
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        var openConfig = new ToolStripMenuItem("打开配置");
        openConfig.Click += (_, _) => OpenFile(_configPath);
        var reloadConfig = new ToolStripMenuItem("重新加载配置");
        reloadConfig.Click += (_, _) => ReloadConfig();
        var writeDiagnostics = new ToolStripMenuItem("写入诊断日志");
        writeDiagnostics.Click += (_, _) => WriteDiagnostics();
        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            _statusItem,
            new ToolStripSeparator(),
            _pauseItem,
            _startupItem,
            new ToolStripSeparator(),
            openConfig,
            reloadConfig,
            writeDiagnostics,
            new ToolStripSeparator(),
            exitItem
        ]);

        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "WeType Caps",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => ShowStatus();

        _remapper = new KeyboardRemapper(
            () => _captureEnabled,
            config.HoldThresholdMilliseconds);
        _remapper.Install();

        _stateTimer = new System.Windows.Forms.Timer
        {
            Interval = config.StateRefreshMilliseconds
        };
        _stateTimer.Tick += (_, _) => RefreshState();
        _stateTimer.Start();
        RefreshState();

        if (_config.EnableDebugLog)
        {
            AppLog.Write($"Started. Identity={_identity}");
        }
    }

    private void RefreshState()
    {
        bool isWeType = _tsf.IsWeTypeActive(out _activeProfile);
        _foreground = FullscreenDetector.Inspect(_config);
        bool bypass =
            _foreground.IsAlwaysBypassed ||
            (_config.BypassFullscreen && _foreground.IsFullscreenAndSelected);
        _captureEnabled = _manualEnabled && isWeType && !bypass;

        string status = !_manualEnabled
            ? "已暂停"
            : !isWeType
                ? "等待微信输入法"
                : bypass
                    ? "当前应用已绕过"
                    : "生效中";
        _statusItem.Text = status;
        _pauseItem.Text = _manualEnabled ? "暂停" : "恢复";
        _tray.Text = $"WeType Caps：{status}";
    }

    private void TogglePause()
    {
        _manualEnabled = !_manualEnabled;
        RefreshState();
    }

    private void ToggleStartup()
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupKeyPath);
            if (IsStartupEnabled())
            {
                key.DeleteValue(StartupValueName, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(StartupValueName, $"\"{Application.ExecutablePath}\"");
            }

            _startupItem.Checked = IsStartupEnabled();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"修改开机启动失败：{ex.Message}", "WeType Caps");
        }
    }

    private static bool IsStartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(StartupKeyPath);
        return key?.GetValue(StartupValueName) is not null;
    }

    private void ReloadConfig()
    {
        try
        {
            AppConfig loaded = AppConfig.LoadOrCreate(_configPath);
            TsfProfileIdentity identity = TsfProfileDetector.ResolveWeTypeIdentity(loaded);
            if (identity != _identity)
            {
                MessageBox.Show(
                    "检测到微信输入法标识已变化。请退出后重新启动程序以应用。",
                    "WeType Caps");
            }

            _config = loaded;
            _remapper.HoldThresholdMilliseconds = loaded.HoldThresholdMilliseconds;
            _stateTimer.Interval = loaded.StateRefreshMilliseconds;
            RefreshState();
            _tray.ShowBalloonTip(1500, "WeType Caps", "配置已重新加载。", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"重新加载失败：{ex.Message}", "WeType Caps");
        }
    }

    private void WriteDiagnostics()
    {
        string report =
            $"Identity={_identity}{Environment.NewLine}" +
            $"ActiveProfile={_activeProfile}{Environment.NewLine}" +
            $"Foreground={_foreground}{Environment.NewLine}" +
            $"CaptureEnabled={_captureEnabled}{Environment.NewLine}" +
            $"Config={System.Text.Json.JsonSerializer.Serialize(_config, AppConfig.JsonOptions)}";
        AppLog.Write("Manual diagnostics:" + Environment.NewLine + report);
        MessageBox.Show($"诊断已写入：\n{AppLog.Path}", "WeType Caps");
    }

    private void ShowStatus()
    {
        string fullscreen = _foreground.IsFullscreen ? "是" : "否";
        MessageBox.Show(
            $"状态：{_statusItem.Text}\n" +
            $"前台进程：{_foreground.ProcessName}\n" +
            $"全屏：{fullscreen}\n" +
            $"长按阈值：{_config.HoldThresholdMilliseconds} ms\n\n" +
            "短按 Caps Lock → Ctrl+Space\n" +
            "长按 Caps Lock → 切换大写锁定",
            "WeType Caps");
    }

    private static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开失败：{ex.Message}", "WeType Caps");
        }
    }

    protected override void ExitThreadCore()
    {
        _captureEnabled = false;
        _stateTimer.Stop();
        _stateTimer.Dispose();
        _remapper.Dispose();
        _tsf.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}

