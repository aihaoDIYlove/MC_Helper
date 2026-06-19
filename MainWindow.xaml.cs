using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MC_Helper.Models;
using MC_Helper.Panels;
using MC_Helper.Services;

namespace MC_Helper;

public partial class MainWindow : Window
{
    private ModeManager? _modeManager;
    private SettingsService? _settingsService;
    private ClickTool? _clickTool;
    private DetectionLoop? _detection;

    private ClickToolPanel? _clickPanel;
    private UserControl? _fishingPanel;

    // 边缘吸附状态追踪
    private bool _snappedLeft, _snappedRight, _snappedTop, _snappedBottom;

    /// <summary>键盘 HoldActive 松键轮询 —— RegisterHotKey 只有 KeyDown，需用 GetAsyncKeyState 补检 KeyUp</summary>
    private DispatcherTimer? _holdActiveUpTimer;
    private int _holdActiveVkCode;
    private int _holdActiveMods;

    /// <summary>设置窗口打开时暂停全局快捷键分发</summary>
    public bool SuppressGlobalKeys { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => ReSnapAfterResize();
        // 初次启动时等 App 设完初始位置后再执行吸附（Background 优先级晚于 Loaded）
        Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, () => SnapToEdge());
    }

    public void WireUp(
        ModeManager modeManager,
        SettingsService settingsService,
        ClickTool clickTool,
        DetectionLoop detection,
        ClickToolPanel clickPanel)
    {
        _modeManager = modeManager;
        _settingsService = settingsService;
        _clickTool = clickTool;
        _detection = detection;
        _clickPanel = clickPanel;

        _modeManager.ModeChanged += OnModeChanged;
        _modeManager.RunningChanged += OnRunningChanged;

        OnModeChanged(_modeManager.CurrentMode);
    }

    public void RegisterFishingPanel(UserControl fishingPanel)
    {
        _fishingPanel = fishingPanel;
    }

    /// <summary>按键按下分发</summary>
    public bool TryHandleGlobalKey(int vkCode, int mods, bool isMouse, int mouseButton)
    {
        if (_modeManager == null || SuppressGlobalKeys) return false;
        var ms = _settingsService!.Settings.ModeSwitching;

        if (KeyBindingMatches(ms.ToggleModeKey, vkCode, mods, isMouse, mouseButton))
        { _modeManager.NextMode(); return true; }

        if (KeyBindingMatches(ms.QuickToggleKey, vkCode, mods, isMouse, mouseButton))
        {
            if (_modeManager.CurrentMode == ToolMode.Click
                && _settingsService.Settings.Click.ActivePreset.TriggerMode == TriggerMode.HoldActive)
            {
                if (_settingsService.Settings.Fishing.DebugLogInput)
                    Helpers.Logger.Info($"[Hook] KeyDown QuickToggle → ActivateCurrent (HoldActive)");
                _modeManager.ActivateCurrent();
            }
            else
                _modeManager.ToggleCurrent();
            return true;
        }

        if (_modeManager.CurrentMode == ToolMode.Click)
        {
            if (KeyBindingMatches(ms.PrevPresetKey, vkCode, mods, isMouse, mouseButton))
            { _modeManager.PrevPreset(); return true; }
            if (KeyBindingMatches(ms.NextPresetKey, vkCode, mods, isMouse, mouseButton))
            { _modeManager.NextPreset(); return true; }
        }

        return false;
    }

    /// <summary>按键松开分发 — HoldActive 模式停止</summary>
    public bool TryHandleGlobalKeyUp(int vkCode, int mods, bool isMouse, int mouseButton)
    {
        if (_modeManager == null || SuppressGlobalKeys) return false;
        var ms = _settingsService!.Settings.ModeSwitching;

        if (KeyBindingMatches(ms.QuickToggleKey, vkCode, mods, isMouse, mouseButton))
        {
            if (_modeManager.CurrentMode == ToolMode.Click
                && _settingsService.Settings.Click.ActivePreset.TriggerMode == TriggerMode.HoldActive)
            {
                if (_settingsService.Settings.Fishing.DebugLogInput)
                    Helpers.Logger.Info($"[Hook] KeyUp QuickToggle → DeactivateCurrent (HoldActive)");
                _modeManager.DeactivateCurrent();
                StopHoldActivePoll();
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 启动键盘 HoldActive 松键轮询。RegisterHotKey 只有 KeyDown 没有 KeyUp，
    /// 所以用 GetAsyncKeyState 定时检查快捷键是否已松开，松开后自动停止工具。
    /// </summary>
    internal void StartKeyboardHoldActivePoll(int vkCode, int mods)
    {
        StopHoldActivePoll();
        _holdActiveVkCode = vkCode;
        _holdActiveMods = mods;

        _holdActiveUpTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(30),
            DispatcherPriority.Background,
            OnHoldActivePollTick,
            Dispatcher.CurrentDispatcher);
        _holdActiveUpTimer.Start();

        if (_settingsService?.Settings.Fishing.DebugLogInput == true)
            Helpers.Logger.Info($"[HoldActivePoll] 启动 vk={vkCode} mods={mods}");
    }

    internal void StopHoldActivePoll()
    {
        if (_holdActiveUpTimer == null) return;
        _holdActiveUpTimer.Stop();
        _holdActiveUpTimer = null;
        if (_settingsService?.Settings.Fishing.DebugLogInput == true)
            Helpers.Logger.Info("[HoldActivePoll] 停止");
    }

    private void OnHoldActivePollTick(object? sender, EventArgs e)
    {
        // 检查主键是否松开
        bool keyDown = (Helpers.Win32.GetAsyncKeyState(_holdActiveVkCode) & 0x8000) != 0;

        if (!keyDown)
        {
            if (_settingsService?.Settings.Fishing.DebugLogInput == true)
                Helpers.Logger.Info("[HoldActivePoll] 按键已松开 → DeactivateCurrent");
            _modeManager?.DeactivateCurrent();
            StopHoldActivePoll();
            return;
        }

        // 如果快捷键含修饰键，修饰键松开也应停止
        if (_holdActiveMods != 0)
        {
            bool modsMatch = true;
            if ((_holdActiveMods & 0x2) != 0) modsMatch &= Helpers.Win32.IsCtrlPressed;
            if ((_holdActiveMods & 0x4) != 0) modsMatch &= Helpers.Win32.IsShiftPressed;
            if ((_holdActiveMods & 0x1) != 0) modsMatch &= Helpers.Win32.IsAltPressed;
            if ((_holdActiveMods & 0x8) != 0) modsMatch &= Helpers.Win32.IsWinPressed;

            if (!modsMatch)
            {
                if (_settingsService?.Settings.Fishing.DebugLogInput == true)
                    Helpers.Logger.Info("[HoldActivePoll] 修饰键已松开 → DeactivateCurrent");
                _modeManager?.DeactivateCurrent();
                StopHoldActivePoll();
            }
        }
    }

    private static bool KeyBindingMatches(Models.KeyBinding kb, int vkCode, int mods,
        bool isMouse, int mouseButton)
    {
        if (kb.IsMouseButton != isMouse) return false;
        if (isMouse) return kb.MouseButton == mouseButton;
        return kb.VkCode == vkCode && kb.Modifiers == mods;
    }

    private void OnModeChanged(ToolMode mode)
    {
        Dispatcher.Invoke(() =>
        {
            ModeLabel.Text = ModeManager.GetModeName(mode);
            ModeContent.Visibility = Visibility.Visible;

            switch (mode)
            {
                case ToolMode.Click:
                    ModeContent.Content = _clickPanel;
                    _clickPanel?.SetRunning(_modeManager!.IsRunning);
                    break;
                case ToolMode.Fishing:
                    ModeContent.Content = _fishingPanel;
                    break;
            }

            // 面板宽度变化后重新贴边
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ReSnapAfterResize);
        });
    }

    /// <summary>面板尺寸变化后，按之前吸附的边重新贴过去</summary>
    private void ReSnapAfterResize()
    {
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        if (_snappedRight)
            Left = ClampToScreen(screenW - ActualWidth, ActualWidth, screenW);
        else if (_snappedLeft)
            Left = 0;

        if (_snappedBottom)
            Top = ClampToScreen(screenH - ActualHeight, ActualHeight, screenH);
        else if (_snappedTop)
            Top = 0;
    }

    /// <summary>确保窗口至少保留 40px 在屏幕内</summary>
    private static double ClampToScreen(double pos, double size, double screenSize)
    {
        if (size >= screenSize) return pos; // 比屏幕还大，保持原位
        if (pos + size < 40) return 40 - size;   // 太靠左/上，拉回来
        if (pos > screenSize - 40) return screenSize - 40; // 太靠右/下，拉回来
        return pos;
    }

    private void OnRunningChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            if (_modeManager == null) return;
            if (_modeManager.CurrentMode == ToolMode.Click)
                _clickPanel?.SetRunning(running);
            // 工具被任何方式停止时，清理 HoldActive 轮询
            if (!running) StopHoldActivePoll();
        });
    }

    private void Window_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        DragMove();
        SnapToEdge();
    }

    /// <summary>边缘吸附：任意一边距屏幕边缘 ≤20px 就贴过去（支持窗口超出屏幕后拖回）</summary>
    private void SnapToEdge()
    {
        const double snapMargin = 50;
        double screenW = SystemParameters.PrimaryScreenWidth;
        double screenH = SystemParameters.PrimaryScreenHeight;

        // 左边距：窗口左边在屏幕内时看 Left；超出屏幕左边时看溢出量 |Left|
        double leftDist = Left >= 0 ? Left : -Left;
        // 右边距：窗口右边在屏幕内时看间距；超出屏幕右边时看溢出量
        double rightDist = Left + ActualWidth <= screenW
            ? screenW - Left - ActualWidth
            : Left + ActualWidth - screenW;

        _snappedLeft = leftDist <= snapMargin;
        _snappedRight = rightDist <= snapMargin;
        _snappedTop = Math.Abs(Top) <= snapMargin;
        _snappedBottom = Math.Abs(screenH - Top - ActualHeight) <= snapMargin;

        if (_snappedLeft && _snappedRight)
        {
            // 两边都在吸附范围内（窗口比屏幕宽），选择溢出更少的一侧
            if (leftDist <= rightDist) _snappedRight = false;
            else _snappedLeft = false;
        }

        if (_snappedLeft)
            Left = 0;
        else if (_snappedRight)
            Left = screenW - ActualWidth;

        if (_snappedTop)
            Top = 0;
        else if (_snappedBottom)
            Top = screenH - ActualHeight;
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e) { }
}
