using System.Runtime.InteropServices;

namespace MC_Helper.Helpers;

public static class Win32
{
    // ── 窗口样式 ────────────────────────────────
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;

    // ── SendInput 常量 ───────────────────────────
    public const int INPUT_MOUSE = 0;
    public const int INPUT_KEYBOARD = 1;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    public const uint KEYEVENTF_KEYDOWN = 0x0000;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // ── RegisterHotKey 常量 ──────────────────────
    public const int MOD_ALT = 0x0001;
    public const int MOD_CONTROL = 0x0002;
    public const int MOD_SHIFT = 0x0004;
    public const int MOD_WIN = 0x0008;
    public const int MOD_NOREPEAT = 0x4000;
    public const int WM_HOTKEY = 0x0312;

    // ── 低层钩子常量 ─────────────────────────────
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYUP = 0x0105;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;
    public const int WM_MBUTTONDOWN = 0x0207;
    public const int WM_MBUTTONUP = 0x0208;
    public const int WM_XBUTTONDOWN = 0x020B;
    public const int WM_XBUTTONUP = 0x020C;

    // 鼠标侧键 data 字段
    public const int XBUTTON1 = 0x0001;
    public const int XBUTTON2 = 0x0002;

    // ── 注入事件标志 ─────────────────────────────
    /// <summary>MSLLHOOKSTRUCT.flags: 事件由 SendInput/mouse_event 注入</summary>
    public const int LLMHF_INJECTED = 0x01;

    // ── 结构体 ───────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public int type;
        public INPUTUNION u;
    }

    /// <summary>KBDLLHOOKSTRUCT — 低层键盘钩子数据</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>MSLLHOOKSTRUCT — 低层鼠标钩子数据</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public int ptX;
        public int ptY;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // ── 窗口 API ─────────────────────────────────

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    public static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    // ── SendInput ────────────────────────────────

    [DllImport("user32.dll")]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // ── RegisterHotKey ───────────────────────────

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ── RegisterHotKey 预定义 ID ──────────────────
    public const int HK_TOGGLE_MODE = 1;
    public const int HK_QUICK_TOGGLE = 3;
    public const int HK_PREV_PRESET = 4;
    public const int HK_NEXT_PRESET = 5;
    public const int HK_TOGGLE_VISIBILITY = 6;  // 始终活跃，不受隐藏影响

    // ── 低层钩子 API ─────────────────────────────

    public delegate IntPtr LowLevelHookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelHookProc lpfn,
        IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    // ── 修饰键状态查询 ───────────────────────────

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public static bool IsCtrlPressed => (GetAsyncKeyState(0x11) & 0x8000) != 0;
    public static bool IsShiftPressed => (GetAsyncKeyState(0x10) & 0x8000) != 0;
    public static bool IsAltPressed => (GetAsyncKeyState(0x12) & 0x8000) != 0;
    public static bool IsWinPressed =>
        (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0;

    // ── 消息循环 API ─────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int nExitCode);

    // ── 屏幕尺寸（物理像素） ──────────────────────

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    /// <summary>物理像素屏幕宽高（不受 DPI 缩放影响，与 CopyFromScreen 一致）</summary>
    public static int PhysicalScreenWidth => GetSystemMetrics(SM_CXSCREEN);
    public static int PhysicalScreenHeight => GetSystemMetrics(SM_CYSCREEN);

    // ══════════════════════════════════════════════
    //  DWM 窗口特效 API（亚克力/Mica 背景模糊 + 圆角）
    // ══════════════════════════════════════════════

    // ── DWM 窗口属性枚举 ─────────────────────────

    /// <summary>DWMWINDOWATTRIBUTE — 用于 DwmSetWindowAttribute</summary>
    public const int DWMWA_NCRENDERING_ENABLED = 1;
    public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;          // Win10 20H1+
    public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;              // Win11 22621+
    public const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;         // Win11

    /// <summary>DWM_SYSTEMBACKDROP_TYPE — Mica/Acrylic 背景类型</summary>
    public const int DWMSBT_AUTO = 0;           // 系统默认
    public const int DWMSBT_NONE = 1;           // 无效果
    public const int DWMSBT_MAINWINDOW = 2;     // Mica（云母）
    public const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic（亚克力）
    public const int DWMSBT_TABBEDWINDOW = 4;   // Mica Alt

    /// <summary>DWM_WINDOW_CORNER_PREFERENCE</summary>
    public const int DWMWCP_DEFAULT = 0;
    public const int DWMWCP_DONOTROUND = 1;
    public const int DWMWCP_ROUND = 2;
    public const int DWMWCP_ROUNDSMALL = 3;

    // ── DWM 模糊结构体 ───────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DWM_BLURBEHIND
    {
        public uint dwFlags;
        [MarshalAs(UnmanagedType.Bool)] public bool fEnable;
        public IntPtr hRgnBlur;
        [MarshalAs(UnmanagedType.Bool)] public bool fTransitionOnMaximized;
    }

    public const uint DWM_BB_ENABLE = 0x00000001;
    public const uint DWM_BB_BLURREGION = 0x00000002;
    public const uint DWM_BB_TRANSITIONONMAXIMIZED = 0x00000004;

    // ── Win10 SetWindowCompositionAttribute（私有 API，亚克力回退） ──

    public const int ACCENT_ENABLE_BLURBEHIND = 3;
    public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

    [StructLayout(LayoutKind.Sequential)]
    public struct ACCENTPOLICY
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINCOMPATTRDATA
    {
        public int Attribute;       // WCA_ACCENT_POLICY = 19
        public IntPtr Data;
        public int DataSize;
    }

    // ── DWM API 声明 ─────────────────────────────

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute,
        ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    public static extern void DwmEnableBlurBehindWindow(IntPtr hWnd, ref DWM_BLURBEHIND pBlurBehind);

    [DllImport("dwmapi.dll")]
    public static extern int DwmIsCompositionEnabledNative(
        [MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);

    /// <summary>检查 DWM 合成是否启用（修正 P/Invoke 签名）</summary>
    public static bool DwmIsCompositionEnabled()
    {
        int hr = DwmIsCompositionEnabledNative(out bool enabled);
        return hr == 0 && enabled;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowCompositionAttribute(IntPtr hwnd,
        ref WINCOMPATTRDATA data);

    /// <summary>
    /// 尝试为窗口启用最佳背景特效：
    /// Win11  → Mica/Acrylic (DWMWA_SYSTEMBACKDROP_TYPE)
    /// Win10  → AcrylicBlurBehind (SetWindowCompositionAttribute)
    ///         回退 → BlurBehind (DwmEnableBlurBehindWindow)
    /// 同时启用 Win11 系统圆角。
    /// </summary>
    public static void ApplyWindowBackdrop(IntPtr hwnd)
    {
        if (!DwmIsCompositionEnabled()) return;

        // ① Win11 圆角
        int corner = DWMWCP_ROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE,
            ref corner, sizeof(int));

        // ② Win11 Mica/Acrylic
        int backdrop = DWMSBT_MAINWINDOW; // Mica — 半透明云母效果
        int hr = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE,
            ref backdrop, sizeof(int));
        if (hr == 0) return; // 成功 → 完成

        // ③ Win10 回退：Acrylic
        var accent = new ACCENTPOLICY
        {
            AccentState = ACCENT_ENABLE_ACRYLICBLURBEHIND,
            AccentFlags = 2,     // 绘制所有边框
            GradientColor = unchecked((int)0x99282929)  // ABGR: #282929 @ 60% alpha
        };
        var dataSize = Marshal.SizeOf(accent);
        var accentPtr = Marshal.AllocHGlobal(dataSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WINCOMPATTRDATA
            {
                Attribute = 19, // WCA_ACCENT_POLICY
                Data = accentPtr,
                DataSize = dataSize
            };
            if (SetWindowCompositionAttribute(hwnd, ref data)) return;
        }
        finally { Marshal.FreeHGlobal(accentPtr); }

        // ④ Win10 回退：经典 BlurBehind（Gaussian 模糊）
        var bb = new DWM_BLURBEHIND
        {
            dwFlags = DWM_BB_ENABLE,
            fEnable = true,
            hRgnBlur = IntPtr.Zero,
            fTransitionOnMaximized = false
        };
        DwmEnableBlurBehindWindow(hwnd, ref bb);

        // ⑤ 扩展玻璃边框到整个客户区
        var margins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1,
            cyTopHeight = -1, cyBottomHeight = -1 };
        DwmExtendFrameIntoClientArea(hwnd, ref margins);
    }

    /// <summary>检查当前系统是否支持 Mica 背景（Win11 22621+）</summary>
    public static bool IsMicaSupported
    {
        get
        {
            var os = Environment.OSVersion;
            return os.Platform == PlatformID.Win32NT && os.Version.Build >= 22621;
        }
    }
}
