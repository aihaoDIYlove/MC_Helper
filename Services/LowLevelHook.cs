using System.Diagnostics;
using System.Runtime.InteropServices;
using MC_Helper.Helpers;

namespace MC_Helper.Services;

/// <summary>
/// 低层全局鼠标钩子 — 仅监听鼠标侧键 (X1/X2)，在独立后台线程运行。
/// 键盘热键已迁移到 RegisterHotKey API，不再走钩子。
/// SendInput 注入事件通过 LLMHF_INJECTED 标志直接跳过，防止钩子线程超时。
/// </summary>
public class LowLevelHook : IDisposable
{
    private Thread? _hookThread;
    private volatile bool _exitRequested;
    private IntPtr _mouseHook = IntPtr.Zero;

    public Func<int, int, bool>? ShouldSuppressMouseButton { get; set; }
    public Func<int, int, bool>? ShouldSuppressMouseButtonUp { get; set; }

    public void Install()
    {
        _hookThread = new Thread(HookThreadProc)
        {
            Name = "MC_Helper_MouseHook",
            IsBackground = true
        };
        _hookThread.SetApartmentState(ApartmentState.STA);
        _hookThread.Start();
        Thread.Sleep(100);
    }

    private void HookThreadProc()
    {
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        if (curModule == null) return;
        var hMod = Win32.GetModuleHandle(curModule.ModuleName);

        _mouseHook = Win32.SetWindowsHookEx(
            Win32.WH_MOUSE_LL, MouseProc, hMod, 0);

        if (_mouseHook == IntPtr.Zero)
        {
            Logger.Error($"鼠标钩子安装失败: err={Marshal.GetLastWin32Error()}");
            return;
        }

        Logger.Info("鼠标钩子已安装 (后台线程, 仅侧键)");

        while (!_exitRequested)
        {
            if (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0))
            {
                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }
            else
            {
                break;
            }
        }

        if (_mouseHook != IntPtr.Zero) Win32.UnhookWindowsHookEx(_mouseHook);
        _mouseHook = IntPtr.Zero;
        Logger.Info("鼠标钩子已卸载");
    }

    private IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= 0)
            {
                var msg = (int)wParam;

                // 非侧键事件（LEFT/RIGHT/MOVE/MWHEEL 等）：注入事件直接跳过，不解析结构体
                // 侧键事件（XBUTTON）：永远处理，因为部分鼠标驱动也会标记注入标志
                if (msg != Win32.WM_XBUTTONDOWN && msg != Win32.WM_XBUTTONUP)
                {
                    // MSLLHOOKSTRUCT 布局: ptX(0) ptY(4) mouseData(8) flags(12) ...
                    int flags = Marshal.ReadInt32(lParam, 12);
                    if ((flags & Win32.LLMHF_INJECTED) != 0)
                        return Win32.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                }

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
        if (_hookThread?.IsAlive == true)
            _hookThread.Join(2000);

        // 紧急清理：注入 LEFTUP + RIGHTUP，防止残留的鼠标按下状态卡死系统
        try
        {
            var inputs = new Win32.INPUT[2];
            inputs[0] = new Win32.INPUT
            {
                type = Win32.INPUT_MOUSE,
                u = new Win32.INPUTUNION
                {
                    mi = new Win32.MOUSEINPUT
                    {
                        dx = 0, dy = 0, mouseData = 0,
                        dwFlags = Win32.MOUSEEVENTF_LEFTUP, time = 0, dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            inputs[1] = new Win32.INPUT
            {
                type = Win32.INPUT_MOUSE,
                u = new Win32.INPUTUNION
                {
                    mi = new Win32.MOUSEINPUT
                    {
                        dx = 0, dy = 0, mouseData = 0,
                        dwFlags = Win32.MOUSEEVENTF_RIGHTUP, time = 0, dwExtraInfo = IntPtr.Zero
                    }
                }
            };
            Win32.SendInput(2, inputs, Marshal.SizeOf<Win32.INPUT>());
            Logger.Info("紧急清理: LEFTUP + RIGHTUP 已发送");
        }
        catch (Exception ex) { Logger.Error("紧急清理失败", ex); }
    }
}
