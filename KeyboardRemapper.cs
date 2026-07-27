using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WeTypeCaps;

internal sealed class KeyboardRemapper : IDisposable
{
    private static readonly nuint InjectionMarker =
        nint.Size == 8 ? unchecked((nuint)0x5754595045434150UL) : (nuint)0x57435450U;

    private readonly Func<bool> _shouldCapture;
    private readonly NativeMethods.LowLevelKeyboardProc _hookProc;
    private readonly System.Windows.Forms.Timer _holdTimer;
    private nint _hook;
    private bool _capturedPhysicalDown;
    private bool _longPressTriggered;

    internal KeyboardRemapper(Func<bool> shouldCapture, int holdThresholdMilliseconds)
    {
        _shouldCapture = shouldCapture;
        _hookProc = HookCallback;
        _holdTimer = new System.Windows.Forms.Timer
        {
            Interval = holdThresholdMilliseconds
        };
        _holdTimer.Tick += HoldTimerOnTick;
    }

    internal int HoldThresholdMilliseconds
    {
        get => _holdTimer.Interval;
        set => _holdTimer.Interval = Math.Clamp(value, 200, 2000);
    }

    internal void Install()
    {
        if (_hook != 0)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl,
            _hookProc,
            NativeMethods.GetModuleHandle(null),
            0);
        if (_hook == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装键盘钩子。");
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode != NativeMethods.HcAction)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
        if (data.vkCode != NativeMethods.VkCapital ||
            (data.flags & NativeMethods.LlkhfInjected) != 0 ||
            data.dwExtraInfo == InjectionMarker)
        {
            return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        int message = unchecked((int)wParam);
        bool isDown = message is NativeMethods.WmKeyDown or NativeMethods.WmSysKeyDown;
        bool isUp = message is NativeMethods.WmKeyUp or NativeMethods.WmSysKeyUp;

        if (isDown)
        {
            if (_capturedPhysicalDown)
            {
                return 1;
            }

            if (!_shouldCapture())
            {
                return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
            }

            _capturedPhysicalDown = true;
            _longPressTriggered = false;
            _holdTimer.Stop();
            _holdTimer.Start();
            return 1;
        }

        if (isUp && _capturedPhysicalDown)
        {
            _holdTimer.Stop();
            bool sendShortPress = !_longPressTriggered;
            _capturedPhysicalDown = false;
            _longPressTriggered = false;

            if (sendShortPress)
            {
                SendCtrlSpace();
            }

            return 1;
        }

        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private void HoldTimerOnTick(object? sender, EventArgs e)
    {
        _holdTimer.Stop();
        if (!_capturedPhysicalDown || _longPressTriggered)
        {
            return;
        }

        _longPressTriggered = true;
        SendKeyStroke(NativeMethods.VkCapital);
    }

    private static void SendCtrlSpace()
    {
        bool controlAlreadyDown =
            (NativeMethods.GetAsyncKeyState(NativeMethods.VkLControl) & 0x8000) != 0 ||
            (NativeMethods.GetAsyncKeyState(NativeMethods.VkRControl) & 0x8000) != 0;

        var inputs = new List<NativeMethods.INPUT>(4);
        if (!controlAlreadyDown)
        {
            inputs.Add(KeyInput(NativeMethods.VkLControl, keyUp: false));
        }

        inputs.Add(KeyInput(NativeMethods.VkSpace, keyUp: false));
        inputs.Add(KeyInput(NativeMethods.VkSpace, keyUp: true));

        if (!controlAlreadyDown)
        {
            inputs.Add(KeyInput(NativeMethods.VkLControl, keyUp: true));
        }

        Send(inputs);
    }

    internal static bool SendCtrlSpaceForDiagnostics()
    {
        bool controlAlreadyDown =
            (NativeMethods.GetAsyncKeyState(NativeMethods.VkLControl) & 0x8000) != 0 ||
            (NativeMethods.GetAsyncKeyState(NativeMethods.VkRControl) & 0x8000) != 0;
        if (controlAlreadyDown)
        {
            return false;
        }

        NativeMethods.INPUT[] inputs =
        [
            KeyInput(NativeMethods.VkLControl, keyUp: false),
            KeyInput(NativeMethods.VkSpace, keyUp: false),
            KeyInput(NativeMethods.VkSpace, keyUp: true),
            KeyInput(NativeMethods.VkLControl, keyUp: true)
        ];
        return Send(inputs) == inputs.Length;
    }

    private static void SendKeyStroke(int virtualKey)
    {
        Send(
        [
            KeyInput(virtualKey, keyUp: false),
            KeyInput(virtualKey, keyUp: true)
        ]);
    }

    private static NativeMethods.INPUT KeyInput(int virtualKey, bool keyUp) =>
        new()
        {
            type = NativeMethods.InputKeyboard,
            U = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = (ushort)virtualKey,
                    dwFlags = keyUp ? NativeMethods.KeyeventfKeyup : 0,
                    dwExtraInfo = InjectionMarker
                }
            }
        };

    private static uint Send(IEnumerable<NativeMethods.INPUT> inputSequence)
    {
        NativeMethods.INPUT[] inputs = inputSequence.ToArray();
        uint sent = NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());
        if (sent != inputs.Length)
        {
            AppLog.Write(
                $"SendInput sent {sent}/{inputs.Length}; Win32={Marshal.GetLastWin32Error()}");
        }

        return sent;
    }

    public void Dispose()
    {
        _holdTimer.Stop();
        _holdTimer.Dispose();
        if (_hook != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        GC.SuppressFinalize(this);
    }
}
