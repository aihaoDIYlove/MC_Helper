using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    /// <summary>设置窗口打开时暂停全局快捷键分发</summary>
    public bool SuppressGlobalKeys { get; set; }

    public MainWindow()
    {
        InitializeComponent();
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

        if (KeyBindingMatches(ms.PrevModeKey, vkCode, mods, isMouse, mouseButton))
        { _modeManager.PrevMode(); return true; }
        if (KeyBindingMatches(ms.NextModeKey, vkCode, mods, isMouse, mouseButton))
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
                return true;
            }
        }

        return false;
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
        });
    }

    private void OnRunningChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            if (_modeManager == null) return;
            if (_modeManager.CurrentMode == ToolMode.Click)
                _clickPanel?.SetRunning(running);
        });
    }

    private void Window_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e) { }
}
