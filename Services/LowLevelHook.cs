using System.Diagnostics;
using System.Runtime.InteropServices;
using MC_Helper.Helpers;

namespace MC_Helper.Services;

/// <summary>
/// 低层全局键盘+鼠标钩子 — 在独立后台线程运行，避免与 SendInput 死锁
/// </summary>
public class LowLevelHook : IDisposable
{
    private Thread? _hookThread;
    private volatile bool _exitRequested;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;

    public Func<int, int, bool>? ShouldSuppressKey { get; set; }
    public Func<int, int, bool>? ShouldSuppressKeyUp { get; set; }
    public Func<int, int, bool>? ShouldSuppressMouseButton { get; set; }
    public Func<int, int, bool>? ShouldSuppressMouseButtonUp { get; set; }

    public void Install()
    {
        _hookThread = new Thread(HookThreadProc)
        {
            Name = "MC_Helper_Hook",
            IsBackground = true
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        // 等待钩子线程的消息循环就绪
        Thread.Sleep(100);
    }

    private void HookThreadProc()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        if (curModule == null) return;
        var hMod = Win32.GetModuleHandle(curModule.ModuleName);

        _keyboardHook = Win32.SetWindowsHookEx(
            Win32.WH_KEYBOARD_LL, KeyboardProc, hMod, 0);

        _mouseHook = Win32.SetWindowsHookEx(
            Win32.WH_MOUSE_LL, MouseProc, hMod, 0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Logger.Error($"钩子安装失败: kbd={_keyboardHook} mouse={_mouseHook} err={Marshal.GetLastWin32Error()}");
            return;
        }

        Logger.Info("低层钩子已安装 (后台线程)");

        // 消息循环
        while (!_exitRequested)
        {
            if (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }
            else
            {
                break; // WM_QUIT
            }
        }

        if (_keyboardHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = IntPtr.Zero;
        _mouseHook = IntPtr.Zero;
        Logger.Info("钩子已卸载");
    }

    private IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = (int)wParam;
                if (msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN)
                {
                    var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                    int vk = (int)kb.vkCode;
                    int mods = GetModifiers();
                    if (ShouldSuppressKey?.Invoke(vk, mods) == true)
                        return (IntPtr)1;
                }
                else if (msg == Win32.WM_KEYUP || msg == Win32.WM_SYSKEYUP)
                {
                    var kb = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                    int vk = (int)kb.vkCode;
                    int mods = GetModifiers();
                    if (ShouldSuppressKeyUp?.Invoke(vk, mods) == true)
                        return (IntPtr)1;
                }
            }
        }
        catch { }
        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = (int)wParam;
                if (msg == Win32.WM_XBUTTONDOWN)
                {
                    var ms = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    int btn = (ms.mouseData >> 16) == Win32.XBUTTON1 ? 1 : 2;
                    int mods = GetModifiers();
                    if (ShouldSuppressMouseButton?.Invoke(btn, mods) == true)
                        return (IntPtr)1;
                }
                else if (msg == Win32.WM_XBUTTONUP)
                {
                    var ms = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);
                    int btn = (ms.mouseData >> 16) == Win32.XBUTTON1 ? 1 : 2;
                    int mods = GetModifiers();
                    if (ShouldSuppressMouseButtonUp?.Invoke(btn, mods) == true)
                        return (IntPtr)1;
                }
            }
        }
        catch { }
        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private static int GetModifiers()
    {
        int mods = 0;
        if (Win32.IsCtrlPressed) mods |= 0x2;
        if (Win32.IsShiftPressed) mods |= 0x4;
        if (Win32.IsAltPressed) mods |= 0x1;
        if (Win32.IsWinPressed) mods |= 0x8;
        return mods;
    }

    public void Dispose()
    {
        _exitRequested = true;
        Win32.PostQuitMessage(0);
        // 等待线程退出（最多 2 秒）
        if (_hookThread?.IsAlive == true)
            _hookThread.Join(2000);
    }
}
