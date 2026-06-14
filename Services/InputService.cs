using System.Runtime.InteropServices;
using MC_Helper.Helpers;

namespace MC_Helper.Services;

public class InputService
{
    private readonly int _clickDurationMs;

    // 保留以兼容 DetectionLoop 的赋值，不再驱动日志行为（SendInput 失败时自动记 Warn/Error）
    public bool DebugLogInput { get; set; }

    public InputService(int clickDurationMs = 50)
    {
        _clickDurationMs = clickDurationMs;
    }

    // ── 右键 ────────────────────────────────────

    public void SendRightClick()
    {
        SendRightDown();
        Thread.Sleep(_clickDurationMs);
        SendRightUp();
    }

    public void SendRightDown()
    {
        SendMouseEventWithRetry(Win32.MOUSEEVENTF_RIGHTDOWN, "RightDown");
    }

    public void SendRightUp()
    {
        SendMouseEventWithRetry(Win32.MOUSEEVENTF_RIGHTUP, "RightUp");
    }

    // ── 左键 ────────────────────────────────────

    public void SendLeftClick()
    {
        SendLeftDown();
        Thread.Sleep(_clickDurationMs);
        SendLeftUp();
    }

    public void SendLeftDown()
    {
        SendMouseEventWithRetry(Win32.MOUSEEVENTF_LEFTDOWN, "LeftDown");
    }

    public void SendLeftUp()
    {
        SendMouseEventWithRetry(Win32.MOUSEEVENTF_LEFTUP, "LeftUp");
    }

    // ── 键盘 ────────────────────────────────────

    public void SendKey(int vkCode, int durationMs = 50)
    {
        SendKeyEventWithRetry(vkCode, Win32.KEYEVENTF_KEYDOWN, $"KeyDown(0x{vkCode:X})");
        Thread.Sleep(durationMs);
        SendKeyEventWithRetry(vkCode, Win32.KEYEVENTF_KEYUP, $"KeyUp(0x{vkCode:X})");
    }

    // ── 内部 SendInput 封装 ──────────────────────

    /// <summary>发送鼠标事件，失败时重试一次。debug 模式下记录每次调用。</summary>
    private void SendMouseEventWithRetry(uint flags, string label)
    {
        var ok = TrySendMouseEvent(flags);
        if (DebugLogInput)
            Logger.Info($"SendInput({label}) → {(ok ? "OK" : "FAIL")}");

        if (ok) return;

        // 第一次失败可能是 UIPI 或消息队列竞争，重试一次
        Logger.Error($"SendInput({label}) 首次失败，1ms 后重试...");
        Thread.Sleep(1);
        ok = TrySendMouseEvent(flags);
        if (DebugLogInput)
            Logger.Info($"SendInput({label}) retry → {(ok ? "OK" : "FAIL")}");
        if (!ok)
            Logger.Error($"SendInput({label}) 重试仍失败，鼠标事件可能未送达！");
    }

    private static bool TrySendMouseEvent(uint flags)
    {
        var inputs = new Win32.INPUT[]
        {
            new()
            {
                type = Win32.INPUT_MOUSE,
                u = new Win32.INPUTUNION
                {
                    mi = new Win32.MOUSEINPUT
                    {
                        dx = 0, dy = 0, mouseData = 0,
                        dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };
        return Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>()) == 1;
    }

    /// <summary>发送键盘事件，失败时重试一次</summary>
    private void SendKeyEventWithRetry(int vkCode, uint flags, string label)
    {
        var ok = TrySendKeyEvent(vkCode, flags);
        if (DebugLogInput)
            Logger.Info($"SendInput({label}) → {(ok ? "OK" : "FAIL")}");

        if (ok) return;

        Logger.Error($"SendInput({label}) 首次失败，1ms 后重试...");
        Thread.Sleep(1);
        ok = TrySendKeyEvent(vkCode, flags);
        if (DebugLogInput)
            Logger.Info($"SendInput({label}) retry → {(ok ? "OK" : "FAIL")}");
        if (!ok)
            Logger.Error($"SendInput({label}) 重试仍失败，键盘事件可能未送达！");
    }

    private static bool TrySendKeyEvent(int vkCode, uint flags)
    {
        var inputs = new Win32.INPUT[]
        {
            new()
            {
                type = Win32.INPUT_KEYBOARD,
                u = new Win32.INPUTUNION
                {
                    ki = new Win32.KEYBDINPUT
                    {
                        wVk = (ushort)vkCode,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero
                    }
                }
            }
        };
        return Win32.SendInput(1, inputs, Marshal.SizeOf<Win32.INPUT>()) == 1;
    }
}
