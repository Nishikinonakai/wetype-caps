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
    private ImeShortcut? _activeShortcut;
    private ActiveProfile _activeProfile;
    private ForegroundState _foreground;

    internal TrayApplicationContext(string configPath, AppConfig config, bool firstRun)
    {
        _configPath = configPath;
        _config = config;
        _identity = TsfProfileDetector.ResolveWeTypeIdentity(config);
        _tsf = new TsfProfileDetector();

        _statusItem = new ToolStripMenuItem("正在检测…") { Enabled = false };
        _pauseItem = new ToolStripMenuItem("暂停");
        _pauseItem.Click += (_, _) => TogglePause();
        _startupItem = new ToolStripMenuItem("开机启动")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = false
        };
        _startupItem.Click += (_, _) => ToggleStartup();

        var openConfig = new ToolStripMenuItem("设置…");
        openConfig.Click += (_, _) => ShowSettings();
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
            () => _captureEnabled ? _activeShortcut : null,
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
        if (firstRun) ShowSettings();
    }

    private void RefreshState()
    {
        _tsf.TryGetActiveProfile(out _activeProfile);
        _activeShortcut = _config.ImeRules.FirstOrDefault(r => r.Matches(_activeProfile))?.Shortcut;
        _foreground = FullscreenDetector.Inspect(_config);
        bool bypass =
            _foreground.IsAlwaysBypassed ||
            (_config.BypassFullscreen && _foreground.IsFullscreenAndSelected);
        _captureEnabled = _manualEnabled && _activeShortcut is not null && !bypass;

        string status = !_manualEnabled
            ? "已暂停"
            : _activeShortcut is null
                ? "等待已启用的输入法"
                : bypass
                    ? "当前应用已绕过"
                    : "生效中";
        _statusItem.Text = status;
        _pauseItem.Text = _manualEnabled ? "暂停" : "恢复";
        _tray.Text = $"WeType Caps：{status}";
    }

    private void ShowSettings()
    {
        using var settings = new SettingsForm(_config, IsStartupEnabled(), _activeProfile);
        if (settings.ShowDialog() != DialogResult.OK) return;
        try
        {
            AppConfig next = settings.GetConfig();
            string temp = _configPath + ".tmp";
            File.WriteAllText(temp, System.Text.Json.JsonSerializer.Serialize(next, AppConfig.JsonOptions));
            File.Move(temp, _configPath, overwrite: true);
            _config = next;
            SetStartup(settings.Startup);
            _remapper.HoldThresholdMilliseconds = next.HoldThresholdMilliseconds;
            _stateTimer.Interval = next.StateRefreshMilliseconds;
            RefreshState();
        }
        catch (Exception ex) { MessageBox.Show($"保存设置失败：{ex.Message}", "Caps 输入法切换"); }
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
            SetStartup(!IsStartupEnabled());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"修改开机启动失败：{ex.Message}", "WeType Caps");
        }
    }

    private void SetStartup(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(StartupKeyPath);
        if (enabled) key.SetValue(StartupValueName, $"\"{Application.ExecutablePath}\"");
        else key.DeleteValue(StartupValueName, throwOnMissingValue: false);
        _startupItem.Checked = enabled;
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
            $"短按 Caps Lock → {_activeShortcut?.ToString() ?? "输入法未匹配"}\n" +
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
