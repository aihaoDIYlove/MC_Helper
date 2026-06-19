using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using MC_Helper.Helpers;
using MC_Helper.Models;
using MC_Helper.Panels;
using MC_Helper.Services;

namespace MC_Helper;

public partial class App : Application
{
    private SettingsService? _settingsService;
    private InputService? _inputService;
    private LowLevelHook? _hook;
    private ModeManager? _modeManager;

    private ClickTool? _clickTool;
    private DetectionLoop? _detection;
    private OcrService? _ocr;
    private RodSwitchService? _rodSwitch;

    private MainWindow? _mainWindow;
    private FishingOverlay? _fishingOverlay;
    private FishingPanel? _fishingPanel;
    private ClickToolPanel? _clickPanel;
    private TrayIcon? _trayIcon;
    private HwndSource? _hwndSource;

    /// <summary>全局快捷键是否启用 — 托盘隐藏窗口时关闭，防止误触</summary>
    private volatile bool _hotkeysEnabled = true;

    /// <summary>RegisterHotKey 注册窗口句柄</summary>
    private IntPtr _hotkeyHwnd;
    /// <summary>已注册的热键 ID 列表（用于注销）</summary>
    private readonly HashSet<int> _registeredHotKeyIds = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Logger.Clear();
        Logger.Info("=== MC_Helper 启动 ===");

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("未处理异常", args.Exception);
            args.Handled = true;
        };

        try
        {
            Logger.Info("加载设置...");
            _settingsService = new SettingsService();
            _settingsService.Load();
            var settings = _settingsService.Settings;

            _inputService = new InputService(50);
            _ocr = new OcrService();
            _rodSwitch = new RodSwitchService(settings.Fishing, _inputService);

            _clickTool = new ClickTool(settings.Click, _inputService);
            _detection = new DetectionLoop(settings.Fishing, _ocr, _inputService, _rodSwitch);

            _modeManager = new ModeManager(settings);

            Logger.Info("创建 UI 对象...");
            CreateUIObjects(settings);

            Logger.Info("连线 UI...");
            WireUpUI(settings);

            _inputService!.DebugLogInput = settings.Fishing.DebugLogInput;
            _clickTool!.DebugLog = settings.Fishing.DebugLogInput;

            CreateTray();

            _mainWindow!.Top = 20;
            _mainWindow.Loaded += (_, _) =>
            {
                _mainWindow.Left = SystemParameters.PrimaryScreenWidth - _mainWindow.ActualWidth;
            };
            _mainWindow.Show();

            // 获取 MainWindow 句柄用于 RegisterHotKey + 鼠标钩子
            var mainHwnd = new WindowInteropHelper(_mainWindow).Handle;
            _hotkeyHwnd = mainHwnd;
            var mainHwndSource = HwndSource.FromHwnd(mainHwnd);
            mainHwndSource!.AddHook(MainWindowWndProc);

            Logger.Info("注册键盘热键...");
            RegisterKeyboardHotKeys();

            Logger.Info("安装鼠标钩子...");
            InstallMouseHook();

            if (_modeManager!.CurrentMode == ToolMode.Fishing)
                _fishingOverlay!.ShowOverlay();

            Logger.Info("启动完成");
        }
        catch (Exception ex)
        {
            Logger.Error("启动失败", ex);
            MessageBox.Show($"启动失败:\n\n{ex.Message}",
                "MC_Helper 启动错误", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  RegisterHotKey — 键盘热键（无钩子，无超时风险）
    // ═══════════════════════════════════════════════════════════════

    private void RegisterKeyboardHotKeys()
    {
        UnregisterKeyboardHotKeys();
        var ms = _settingsService!.Settings.ModeSwitching;

        TryRegisterOne(Win32.HK_TOGGLE_MODE, ms.ToggleModeKey);
        TryRegisterOne(Win32.HK_QUICK_TOGGLE, ms.QuickToggleKey);
        TryRegisterOne(Win32.HK_PREV_PRESET, ms.PrevPresetKey);
        TryRegisterOne(Win32.HK_NEXT_PRESET, ms.NextPresetKey);
    }

    private void TryRegisterOne(int id, KeyBinding kb)
    {
        if (kb.IsMouseButton || kb.IsEmpty) return;
        if (Win32.RegisterHotKey(_hotkeyHwnd, id, kb.Modifiers, kb.VkCode))
        {
            _registeredHotKeyIds.Add(id);
            Logger.Info($"热键已注册: {kb.DisplayText} (id={id})");
        }
        else
        {
            Logger.Error($"热键注册失败: {kb.DisplayText} (id={id}) err={Marshal.GetLastWin32Error()}");
        }
    }

    private void UnregisterKeyboardHotKeys()
    {
        foreach (var id in _registeredHotKeyIds)
            Win32.UnregisterHotKey(_hotkeyHwnd, id);
        _registeredHotKeyIds.Clear();
    }

    private IntPtr MainWindowWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY)
        {
            if (!_hotkeysEnabled) return IntPtr.Zero;
            HandleHotKey((int)wParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void HandleHotKey(int id)
    {
        if (_modeManager == null) return;

        // 防重复派发（键盘连发时）
        var now = DateTime.UtcNow;
        if (id == _lastHotKeyId && (now - _lastHotKeyTime).TotalMilliseconds < 300)
            return;
        _lastHotKeyId = id;
        _lastHotKeyTime = now;

        switch (id)
        {
            case Win32.HK_TOGGLE_MODE:
                _modeManager.NextMode();
                break;
            case Win32.HK_QUICK_TOGGLE:
                if (_modeManager.CurrentMode == ToolMode.Click
                    && _settingsService!.Settings.Click.ActivePreset.TriggerMode == TriggerMode.HoldActive)
                {
                    _modeManager.ActivateCurrent();
                    // 键盘热键没有 KeyUp 事件（RegisterHotKey 只发 WM_HOTKEY），用轮询补检松键
                    var qk = _settingsService.Settings.ModeSwitching.QuickToggleKey;
                    _mainWindow!.StartKeyboardHoldActivePoll(qk.VkCode, qk.Modifiers);
                }
                else
                    _modeManager.ToggleCurrent();
                break;
            case Win32.HK_PREV_PRESET:
                if (_modeManager.CurrentMode == ToolMode.Click)
                    _modeManager.PrevPreset();
                break;
            case Win32.HK_NEXT_PRESET:
                if (_modeManager.CurrentMode == ToolMode.Click)
                    _modeManager.NextPreset();
                break;
        }
    }

    private int _lastHotKeyId = -1;
    private DateTime _lastHotKeyTime = DateTime.MinValue;

    // ═══════════════════════════════════════════════════════════════
    //  WH_MOUSE_LL — 鼠标侧键热键（仅 X1/X2，注入事件跳过）
    // ═══════════════════════════════════════════════════════════════

    private void InstallMouseHook()
    {
        _hook = new LowLevelHook();

        int _lastMouseBtn = -1;
        DateTime _lastMouseTime = DateTime.MinValue;

        _hook.ShouldSuppressMouseButton = (button, mods) =>
        {
            if (!_hotkeysEnabled) return false;
            try
            {
                var ms = _settingsService!.Settings.ModeSwitching;
                bool matchToggle = KeyMatchesThreadSafe(ms.ToggleModeKey, 0, mods, true, button);
                bool matchQuick = KeyMatchesThreadSafe(ms.QuickToggleKey, 0, mods, true, button);

                if (matchToggle || matchQuick)
                {
                    var now = DateTime.UtcNow;
                    if (button == _lastMouseBtn && (now - _lastMouseTime).TotalMilliseconds < 80)
                        return true;
                    _lastMouseBtn = button;
                    _lastMouseTime = now;

                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                        () => { try { _mainWindow!.TryHandleGlobalKey(0, mods, true, button); } catch { } });
                    return true;
                }
            }
            catch (Exception ex) { Logger.Error("ShouldSuppressMouseButton 异常", ex); }
            return false;
        };

        _hook.ShouldSuppressMouseButtonUp = (button, mods) =>
        {
            if (!_hotkeysEnabled) return false;
            try
            {
                var ms = _settingsService!.Settings.ModeSwitching;
                // 与 ShouldSuppressMouseButton 对称：ToggleMode 和 QuickToggle 的 UP 都要吃掉
                bool matchToggleUp = KeyMatchesThreadSafe(ms.ToggleModeKey, 0, mods, true, button);
                bool matchQuickUp = KeyMatchesThreadSafe(ms.QuickToggleKey, 0, mods, true, button);

                if (matchQuickUp)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                        () => { try { _mainWindow!.TryHandleGlobalKeyUp(0, mods, true, button); } catch { } });
                    return true;
                }
                if (matchToggleUp)
                {
                    return true; // 吃掉 UP，与 DOWN 保持平衡
                }
            }
            catch (Exception ex) { Logger.Error("ShouldSuppressMouseButtonUp 异常", ex); }
            return false;
        };

        _hook.Install();
    }

    private static bool KeyMatchesThreadSafe(Models.KeyBinding kb, int vk, int mods, bool isMouse, int mouseBtn)
    {
        if (kb.IsMouseButton != isMouse) return false;
        if (isMouse) return kb.MouseButton == mouseBtn;
        return kb.VkCode == vk && kb.Modifiers == mods;
    }

    // ═══════════════════════════════════════════════════════════════

    private void CreateUIObjects(RootSettings settings)
    {
        _mainWindow = new MainWindow();

        _clickPanel = new ClickToolPanel();
        _clickPanel.Bind(settings.Click, _clickTool!);

        _fishingOverlay = new FishingOverlay();
        _fishingOverlay.Init(settings.Fishing);
        _fishingOverlay.BindDetection(_detection!);

        _fishingPanel = new FishingPanel();
        _fishingPanel.Bind(_detection!, _fishingOverlay);

        _mainWindow.RegisterFishingPanel(_fishingPanel);
    }

    private void WireUpUI(RootSettings settings)
    {
        _mainWindow!.WireUp(_modeManager!, _settingsService!,
            _clickTool!, _detection!, _clickPanel!);

        _mainWindow.BtnSettings.Click += (_, _) => OpenSettings();

        _detection!.DebugOverlayChanged += enabled => _fishingOverlay!.SetDebugOverlayEnabled(enabled);

        _fishingOverlay!.SettingsSaved += () =>
        {
            _settingsService!.Save();
            _detection!.ResetStateMachine();
        };

        _fishingPanel!.SelectRegionRequested += () =>
        {
            if (_detection!.IsRunning) _detection.Stop();
            _fishingOverlay!.ToggleSelectMode();
            _fishingPanel.SetSelectingMode(_fishingOverlay.IsSelecting);
        };

        _modeManager!.ModeChanged += mode =>
        {
            if (mode == ToolMode.Fishing) _fishingOverlay!.ShowOverlay();
            else { if (_detection!.IsRunning) _detection.Stop(); _fishingOverlay!.HideOverlay(); }
        };

        _modeManager.PresetSwitchRequested += delta =>
        {
            if (_clickTool!.IsRunning)
            {
                _clickTool.Stop();
                _modeManager.SetRunning(false);
            }

            var cs = _settingsService!.Settings.Click;
            int count = cs.Presets.Count;
            int idx = (cs.ActivePresetIndex + delta + count) % count;
            cs.ActivePresetIndex = idx;
            _clickPanel!.SyncPresetIndex();
        };

        _modeManager.StartRequested += () =>
        {
            if (_modeManager.CurrentMode == ToolMode.Click) _clickTool!.Start();
            else _detection!.Start();
        };
        _modeManager.StopRequested += () =>
        {
            if (_modeManager.CurrentMode == ToolMode.Click) _clickTool!.Stop();
            else _detection!.Stop();
        };
    }

    private void OpenSettings()
    {
        _mainWindow!.SuppressGlobalKeys = true;
        var win = new SettingsWindow(_settingsService!.Settings, _settingsService, () =>
        {
            var s = _settingsService.Settings;
            _inputService = new InputService(50);
            _inputService.DebugLogInput = s.Fishing.DebugLogInput;
            _clickTool!.DebugLog = s.Fishing.DebugLogInput;
            _detection!.ResetStateMachine();
            _fishingOverlay!.UpdateFromSettings();
            _clickPanel!.UpdateFromSettings();

            // 热键可能已变更，重新注册键盘热键
            RegisterKeyboardHotKeys();
        }) { Owner = _mainWindow };
        win.ShowDialog();
        _mainWindow!.SuppressGlobalKeys = false;
    }

    private void CreateTray()
    {
        var parameters = new HwndSourceParameters("MC_Helper_Tray")
        {
            Width = 0, Height = 0, WindowStyle = 0,
            ExtendedWindowStyle = Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW
        };
        _hwndSource = new HwndSource(parameters);

        _trayIcon = new TrayIcon(_hwndSource.Handle);
        _trayIcon.OpenRequested += () => Dispatcher.Invoke(() =>
        {
            if (_mainWindow!.IsVisible)
            {
                // 隐藏窗口 → 停止工具 + 注销键盘热键 + 禁用鼠标钩子，避免误触和吞键
                _modeManager!.StopCurrent();
                _hotkeysEnabled = false;
                UnregisterKeyboardHotKeys();
                _mainWindow.Hide();
                if (_fishingOverlay!.IsVisible) _fishingOverlay.HideOverlay();
                Logger.Info("窗口已隐藏，热键已注销");
            }
            else
            {
                _mainWindow.Show();
                RegisterKeyboardHotKeys();
                _hotkeysEnabled = true;
                if (_modeManager!.CurrentMode == ToolMode.Fishing) _fishingOverlay!.ShowOverlay();
                Logger.Info("窗口已显示，热键已注册");
            }
        });
        _trayIcon.ExitRequested += () => Dispatcher.Invoke(() => { _mainWindow?.Close(); _fishingOverlay?.Close(); Shutdown(); });

        _trayIcon.DoubleClickRequested += () => Dispatcher.Invoke(() =>
        {
            if (_settingsService!.Settings.DoubleClickTrayToExit)
            {
                Logger.Info("托盘双击 → 强制退出");
                _mainWindow?.Close();
                _fishingOverlay?.Close();
                Shutdown();
            }
        });

        _hwndSource.AddHook((IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (msg == 0x0111) { _trayIcon.HandleCommand((uint)(int)wParam); handled = true; }
            _trayIcon.HandleMessage((uint)msg, wParam, lParam);
            return IntPtr.Zero;
        });

        _trayIcon.Show("MC_Helper — Minecraft 辅助工具");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("=== MC_Helper 退出 ===");
        _clickTool?.Dispose();
        _detection?.Dispose();
        UnregisterKeyboardHotKeys();
        _hook?.Dispose();
        _trayIcon?.Dispose();
        _hwndSource?.Dispose();
        _mainWindow?.Close();
        _fishingOverlay?.Close();
        base.OnExit(e);
    }
}
