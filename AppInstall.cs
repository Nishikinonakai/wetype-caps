using Microsoft.Win32;
using System.Diagnostics;

namespace WeTypeCaps;

internal static class AppInstall
{
    internal static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WeTypeCaps");
    internal static string ExecutablePath => Path.Combine(DirectoryPath, "WeTypeCaps.exe");
    internal static string ConfigPath => Path.Combine(DirectoryPath, "config.json");

    internal static bool OfferInstall()
    {
        string source = Application.ExecutablePath;
        if (!source.EndsWith("WeTypeCaps.exe", StringComparison.OrdinalIgnoreCase) ||
            Path.GetFullPath(source).Equals(Path.GetFullPath(ExecutablePath), StringComparison.OrdinalIgnoreCase))
            return false;

        var choice = MessageBox.Show(
            "将程序安装到当前用户目录、设置登录后启动，并立即运行。\n\n是否安装？",
            "安装 Caps 输入法切换", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (choice != DialogResult.Yes) return false;

        try
        {
            string sourceDirectory = Path.GetDirectoryName(source)!;
            string legacyPath = Path.Combine(
                Path.GetDirectoryName(sourceDirectory) ?? sourceDirectory,
                "publish", "WeTypeCaps.exe");
            foreach (var process in Process.GetProcessesByName("WeTypeCaps"))
            {
                using (process)
                {
                    if (process.Id == Environment.ProcessId) continue;
                    string? runningPath;
                    try { runningPath = process.MainModule?.FileName; }
                    catch { continue; }
                    if (runningPath is null ||
                        !(runningPath.Equals(ExecutablePath, StringComparison.OrdinalIgnoreCase) ||
                          runningPath.Equals(legacyPath, StringComparison.OrdinalIgnoreCase))) continue;
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            Directory.CreateDirectory(DirectoryPath);
            File.Copy(source, ExecutablePath, overwrite: true);
            string oldConfig = Path.Combine(AppContext.BaseDirectory, "config.json");
            if (!File.Exists(ConfigPath) && File.Exists(oldConfig)) File.Copy(oldConfig, ConfigPath);
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            key.SetValue("WeTypeCaps", $"\"{ExecutablePath}\"");
            Process.Start(new ProcessStartInfo(ExecutablePath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"安装失败：{ex.Message}", "Caps 输入法切换", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
