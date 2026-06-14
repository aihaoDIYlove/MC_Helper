using System.Drawing;
using System.Runtime.InteropServices;

namespace MC_Helper.Helpers;

public class TrayIcon : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly uint _uid = 1;
    private Icon? _icon;
    private bool _visible;

    private const uint WM_TRAYICON = 0x8001;

    private const int MENU_OPEN = 1001;
    private const int MENU_EXIT = 1002;

    public event Action? OpenRequested;
    public event Action? ExitRequested;
    public event Action? DoubleClickRequested;

    private DateTime _lastClickTime = DateTime.MinValue;
    private const int DoubleClickWindowMs = 500;

    public TrayIcon(IntPtr hwnd)
    {
        _hwnd = hwnd;
    }

    public void Show(string tooltip = "MC_Helper")
    {
        if (_visible) return;

        _icon?.Dispose();
        _icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                ?? SystemIcons.Application;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = _uid,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = _icon.Handle,
            szTip = tooltip
        };

        Shell_NotifyIcon(NIM_ADD, ref nid);
        _visible = true;
    }

    public void Hide()
    {
        if (!_visible) return;

        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = _uid
        };

        Shell_NotifyIcon(NIM_DELETE, ref nid);
        _visible = false;
        _icon?.Dispose();
        _icon = null;
    }

    public void Dispose()
    {
        Hide();
    }

    public void HandleMessage(uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_TRAYICON) return;
        if ((uint)(int)wParam != _uid) return;

        var mouseMsg = (uint)(int)lParam;

        switch (mouseMsg)
        {
            case 0x0202: // WM_LBUTTONUP
            {
                var now = DateTime.Now;
                if ((now - _lastClickTime).TotalMilliseconds < DoubleClickWindowMs)
                {
                    _lastClickTime = DateTime.MinValue;
                    DoubleClickRequested?.Invoke();
                }
                else
                {
                    _lastClickTime = now;
                    OpenRequested?.Invoke();
                }
                break;
            }

            case 0x0203: // WM_LBUTTONDBLCLK
                _lastClickTime = DateTime.MinValue;
                DoubleClickRequested?.Invoke();
                break;

            case 0x0205: // WM_RBUTTONUP
                ShowContextMenu();
                break;
        }
    }

    public void HandleCommand(uint wParam)
    {
        switch ((int)wParam)
        {
            case MENU_OPEN:
                OpenRequested?.Invoke();
                break;
            case MENU_EXIT:
                ExitRequested?.Invoke();
                break;
        }
    }

    private void ShowContextMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;

        AppendMenuW(menu, MF_STRING, MENU_OPEN, "打开");
        AppendMenuW(menu, MF_STRING, MENU_EXIT, "退出");

        GetCursorPos(out var pt);

        SetForegroundWindow(_hwnd);

        var cmd = TrackPopupMenu(menu,
            0x0002 | 0x0020 | 0x0001,
            pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);

        if (cmd != 0)
            PostMessage(_hwnd, 0x0111, cmd, IntPtr.Zero);

        DestroyMenu(menu);
    }


    private const int NIM_ADD = 0;
    private const int NIM_DELETE = 2;
    private const int NIF_MESSAGE = 1;
    private const int NIF_ICON = 2;
    private const int NIF_TIP = 4;
    private const uint MF_STRING = 0x00000000;

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y,
        int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NOTIFYICONDATA
{
    public int cbSize;
    public IntPtr hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public IntPtr hIcon;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string szTip;
}
