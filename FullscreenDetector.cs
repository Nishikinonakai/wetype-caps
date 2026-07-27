using System.Diagnostics;
using System.IO.Enumeration;
using System.Runtime.InteropServices;

namespace WeTypeCaps;

public readonly record struct ForegroundState(
    nint WindowHandle,
    uint ProcessId,
    string ProcessName,
    string WindowClass,
    bool IsFullscreen,
    bool IsSelectedProcess,
    bool IsAlwaysBypassed)
{
    public bool IsFullscreenAndSelected => IsFullscreen && IsSelectedProcess;
}

internal static class FullscreenDetector
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd",
        "Shell_SecondaryTrayWnd"
    };

    internal static ForegroundState Inspect(AppConfig config)
    {
        nint window = NativeMethods.GetForegroundWindow();
        if (window == 0 || !NativeMethods.IsWindowVisible(window) || NativeMethods.IsIconic(window))
        {
            return default;
        }

        NativeMethods.GetWindowThreadProcessId(window, out uint processId);
        string processName = GetProcessName(processId);
        string windowClass = GetWindowClass(window);
        bool shellWindow = ShellClasses.Contains(windowClass);

        bool fullscreen = !shellWindow && TryGetBounds(window, out var windowRect) &&
            TryGetMonitorBounds(window, out var monitorRect) &&
            CoversMonitor(windowRect, monitorRect, tolerance: 2);

        bool selectedProcess =
            config.FullscreenProcessNames.Length == 0 ||
            MatchesAny(processName, config.FullscreenProcessNames);
        bool alwaysBypassed = MatchesAny(processName, config.AlwaysBypassProcessNames);

        return new ForegroundState(
            window,
            processId,
            processName,
            windowClass,
            fullscreen,
            selectedProcess,
            alwaysBypassed);
    }

    internal static bool CoversMonitor(
        NativeMethods.RECT window,
        NativeMethods.RECT monitor,
        int tolerance) =>
        window.Left <= monitor.Left + tolerance &&
        window.Top <= monitor.Top + tolerance &&
        window.Right >= monitor.Right - tolerance &&
        window.Bottom >= monitor.Bottom - tolerance;

    private static bool TryGetBounds(nint window, out NativeMethods.RECT rect)
    {
        int hr = NativeMethods.DwmGetWindowAttribute(
            window,
            NativeMethods.DwmwaExtendedFrameBounds,
            out rect,
            Marshal.SizeOf<NativeMethods.RECT>());
        return hr == 0 || NativeMethods.GetWindowRect(window, out rect);
    }

    private static bool TryGetMonitorBounds(nint window, out NativeMethods.RECT rect)
    {
        nint monitor = NativeMethods.MonitorFromWindow(window, NativeMethods.MonitorDefaultToNearest);
        var info = new NativeMethods.MONITORINFO
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };
        bool ok = monitor != 0 && NativeMethods.GetMonitorInfo(monitor, ref info);
        rect = info.rcMonitor;
        return ok;
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            return Process.GetProcessById((int)processId).ProcessName;
        }
        catch
        {
            return "";
        }
    }

    private static string GetWindowClass(nint window)
    {
        char[] buffer = new char[256];
        int length = NativeMethods.GetClassName(window, buffer, buffer.Length);
        return length > 0 ? new string(buffer, 0, length) : "";
    }

    private static bool MatchesAny(string processName, IEnumerable<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        string nameWithExtension = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : processName + ".exe";

        return patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Any(pattern =>
                FileSystemName.MatchesSimpleExpression(pattern, processName, ignoreCase: true) ||
                FileSystemName.MatchesSimpleExpression(pattern, nameWithExtension, ignoreCase: true));
    }
}

