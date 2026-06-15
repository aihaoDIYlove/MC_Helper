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

            _inputService = new InputService(50); // 右键持续时间，写死 50ms
            _ocr = new OcrService();
            _rodSwitch = new RodSwitchService(settings.Fishing, _inputService);

            _clickTool = new ClickTool(settings.Click, _inputService);
            _detection = new DetectionLoop(settings.Fishing, _ocr, _inputService, _rodSwitch);

            _modeManager = new ModeManager(settings);

            Logger.Info("创建 UI 对象...");
            CreateUIObjects(settings);

            Logger.Info("安装低层钩子...");
            InstallHooks();

            Logger.Info("连线 UI...");
            WireUpUI(settings);

            // 初始同步 debug 日志开关
            _inputService!.DebugLogInput = settings.Fishing.DebugLogInput;
            _clickTool!.DebugLog = settings.Fishing.DebugLogInput;

            CreateTray();

            _mainWindow!.Top = 20;
            // 等窗口渲染完成后贴右侧边缘
            _mainWindow.Loaded += (_, _) =>
            {
                _mainWindow.Left = SystemParameters.PrimaryScreenWidth - _mainWindow.ActualWidth;
            };
            _mainWindow.Show();
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

    private void InstallHooks()
    {
        _hook = new LowLevelHook();

        // 防重复派发：同一按键短时间内不重复 BeginInvoke
        int _lastDispatchedVk = -1;
        int _lastDispatchedMouseBtn = -1;
        DateTime _lastDispatchTime = DateTime.MinValue;

        _hook.ShouldSuppressKey = (vk, mods) =>
        {
            try
            {
                var ms = _settingsService!.Settings.ModeSwitching;
                bool matchPrev = KeyMatchesThreadSafe(ms.PrevModeKey, vk, mods, false, 0);
                bool matchNext = KeyMatchesThreadSafe(ms.NextModeKey, vk, mods, false, 0);
                bool matchQuick = KeyMatchesThreadSafe(ms.QuickToggleKey, vk, mods, false, 0);
                bool matchPrevP = KeyMatchesThreadSafe(ms.PrevPresetKey, vk, mods, false, 0);
                bool matchNextP = KeyMatchesThreadSafe(ms.NextPresetKey, vk, mods, false, 0);

                if (matchPrev || matchNext || matchQuick || matchPrevP || matchNextP)
                {
                    // 防重复派发（键盘连发时）
                    var now = DateTime.UtcNow;
                    if (vk == _lastDispatchedVk && (now - _lastDispatchTime).TotalMilliseconds < 80)
                        return true;
                    _lastDispatchedVk = vk;
                    _lastDispatchTime = now;

                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                        () => { try { _mainWindow!.TryHandleGlobalKey(vk, mods, false, 0); } catch { } });
                    return true;
                }
            }
            catch (Exception ex) { Logger.Error("ShouldSuppressKey 异常", ex); }
            return false;
        };

        _hook.ShouldSuppressKeyUp = (vk, mods) =>
        {
            try
            {
                var ms = _settingsService!.Settings.ModeSwitching;
                if (KeyMatchesThreadSafe(ms.QuickToggleKey, vk, mods, false, 0))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                        () => { try { _mainWindow!.TryHandleGlobalKeyUp(vk, mods, false, 0); } catch { } });
                    return true;
                }
            }
            catch (Exception ex) { Logger.Error("ShouldSuppressKeyUp 异常", ex); }
            return false;
        };

        _hook.ShouldSuppressMouseButton = (button, mods) =>
        {
            try
            {
                var ms = _settingsService!.Settings.ModeSwitching;
                bool matchPrev = KeyMatchesThreadSafe(ms.PrevModeKey, 0, mods, true, button);
                bool matchNext = KeyMatchesThreadSafe(ms.NextModeKey, 0, mods, true, button);
                bool matchQuick = KeyMatchesThreadSafe(ms.QuickToggleKey, 0, mods, true, button);

                if (matchPrev || matchNext || matchQuick)
                {
                    var now = DateTime.UtcNow;
                    if (button == _lastDispatchedMouseBtn && (now - _lastDispatchTime).TotalMilliseconds < 80)
                        return true;
                    _lastDispatchedMouseBtn = button;
                    _lastDispatchTime = now;

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
            try
            {
                var ms = _settingsService!.Settings.ModeSwitching;
                if (KeyMatchesThreadSafe(ms.QuickToggleKey, 0, mods, true, button))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Input,
                        () => { try { _mainWindow!.TryHandleGlobalKeyUp(0, mods, true, button); } catch { } });
                    return true;
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

        // 方案切换：更新面板下拉 + 若运行中则重启工具
        _modeManager.PresetSwitchRequested += delta =>
        {
            // 先停工具（用旧预设的按钮发 DoUp），再切换预设
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
            _inputService = new InputService(50); // 右键持续时间，写死 50ms
            _inputService.DebugLogInput = s.Fishing.DebugLogInput;
            _clickTool!.DebugLog = s.Fishing.DebugLogInput;
            _detection!.ResetStateMachine();
            _fishingOverlay!.UpdateFromSettings();
            _clickPanel!.UpdateFromSettings();
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
            if (_mainWindow!.IsVisible) { _mainWindow.Hide(); if (_fishingOverlay!.IsVisible) _fishingOverlay.HideOverlay(); }
            else { _mainWindow.Show(); if (_modeManager!.CurrentMode == ToolMode.Fishing) _fishingOverlay!.ShowOverlay(); }
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
        _hook?.Dispose();
        _trayIcon?.Dispose();
        _hwndSource?.Dispose();
        _mainWindow?.Close();
        _fishingOverlay?.Close();
        base.OnExit(e);
    }
}
