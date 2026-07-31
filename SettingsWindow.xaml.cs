using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MC_Helper.Helpers;
using MC_Helper.Models;
using MC_Helper.Services;

namespace MC_Helper;

public partial class SettingsWindow : Window
{
    private readonly RootSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly Action? _onSaved;

    private enum BindTarget { None, ToggleMode, QuickToggle, PrevPreset, NextPreset }
    private BindTarget _capturing;
    private Models.KeyBinding? _capturedBinding;

    public SettingsWindow(RootSettings settings, SettingsService settingsService, Action? onSaved = null)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _onSaved = onSaved;

        // DWM backdrop 在 SourceInitialized 事件中初始化（此时 HWND 已存在）
        SourceInitialized += (_, _) =>
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    Win32.ApplyWindowBackdrop(hwnd);
                    // Win10 沉浸式暗模式标题栏
                    int darkMode = 1;
                    Win32.DwmSetWindowAttribute(hwnd, Win32.DWMWA_USE_IMMERSIVE_DARK_MODE,
                        ref darkMode, Marshal.SizeOf<int>());
                }
            }
            catch
            {
                // DWM 特效失败不影响窗口功能，静默回退
            }
        };

        Loaded += (_, _) =>
        {
            // 初始化侧边栏选中第一项
            SidebarList.SelectedIndex = 0;
            LoadAllSettings();
        };

        KeyDown += OnGlobalKeyDown;
        MouseDown += OnGlobalMouseDown;
    }

    // ── 侧边栏导航 ───────────────────────────────

    private void SidebarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // 防护：InitializeComponent 期间这些字段可能尚未连接
        if (PageGeneral == null || PageClick == null || PageFishing == null) return;
        if (SidebarList.SelectedIndex < 0) return;
        var idx = SidebarList.SelectedIndex;
        PageGeneral.Visibility  = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageClick.Visibility    = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
        PageFishing.Visibility  = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── 设置加载入口 ──────────────────────────────

    private void LoadAllSettings()
    {
        LoadKeyBindings();
        LoadClickPresets();
        LoadFishingSettings();
        LoadGeneralSettings();
    }

    private void LoadGeneralSettings()
    {
        ChkDoubleClickTrayExit.IsChecked = _settings.DoubleClickTrayToExit;
        ChkDebugLogInput.IsChecked = _settings.Fishing.DebugLogInput;
    }

    // ── 按键绑定 ────────────────────────────────

    private void LoadKeyBindings()
    {
        var ms = _settings.ModeSwitching;
        TxtToggleModeKey.Text = ms.ToggleModeKey.DisplayText;
        TxtQuickToggleKey.Text = ms.QuickToggleKey.DisplayText;
        TxtPrevPresetKey.Text = ms.PrevPresetKey.DisplayText;
        TxtNextPresetKey.Text = ms.NextPresetKey.DisplayText;
    }

    private void StartCapture(BindTarget target)
    {
        _capturing = target;
        _capturedBinding = null;
        RefreshBindButtons();
    }

    private void StopCapture()
    {
        _capturing = BindTarget.None;
        RefreshBindButtons();
    }

    private void RefreshBindButtons()
    {
        SetBindBtn(BtnBindToggleMode, _capturing == BindTarget.ToggleMode);
        SetBindBtn(BtnBindQuickToggle, _capturing == BindTarget.QuickToggle);
        SetBindBtn(BtnBindPrevPreset, _capturing == BindTarget.PrevPreset);
        SetBindBtn(BtnBindNextPreset, _capturing == BindTarget.NextPreset);
    }

    private static void SetBindBtn(Button btn, bool active)
    {
        if (active)
        {
            btn.Content = "等待按键...";
            btn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0x9E, 0x0B)); // Amber
            btn.Foreground = new SolidColorBrush(Colors.White);
            btn.FontWeight = FontWeights.SemiBold;
        }
        else
        {
            btn.Content = "点击绑定";
            btn.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x3B, 0x82, 0xF6)); // Blue
            btn.Foreground = new SolidColorBrush(Colors.White);
            btn.FontWeight = FontWeights.SemiBold;
        }
    }

    private void OnGlobalKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturing == BindTarget.None) return;
        if (e.Key == Key.Escape) { StopCapture(); e.Handled = true; return; }
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || e.Key == Key.LeftShift ||
            e.Key == Key.RightShift || e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LWin || e.Key == Key.RWin) return;

        int vk = KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);
        int mods = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) mods |= 0x2;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mods |= 0x4;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) mods |= 0x1;

        _capturedBinding = new Models.KeyBinding(vk, mods);
        ApplyCapturedBinding();
        e.Handled = true;
    }

    private void OnGlobalMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_capturing == BindTarget.None) return;
        int button = 0;
        if (e.ChangedButton == MouseButton.XButton1) button = 1;
        else if (e.ChangedButton == MouseButton.XButton2) button = 2;
        else return;

        _capturedBinding = Models.KeyBinding.Mouse(button);
        ApplyCapturedBinding();
        e.Handled = true;
    }

    private void ApplyCapturedBinding()
    {
        if (_capturedBinding == null) return;
        var display = _capturedBinding.DisplayText;
        var ms = _settings.ModeSwitching;

        switch (_capturing)
        {
            case BindTarget.ToggleMode: ms.ToggleModeKey = _capturedBinding; TxtToggleModeKey.Text = display; break;
            case BindTarget.QuickToggle: ms.QuickToggleKey = _capturedBinding; TxtQuickToggleKey.Text = display; break;
            case BindTarget.PrevPreset: ms.PrevPresetKey = _capturedBinding; TxtPrevPresetKey.Text = display; break;
            case BindTarget.NextPreset: ms.NextPresetKey = _capturedBinding; TxtNextPresetKey.Text = display; break;
        }
        StopCapture();
    }

    private void BtnBindToggleMode_Click(object s, RoutedEventArgs e) => StartCapture(BindTarget.ToggleMode);
    private void BtnBindQuickToggle_Click(object s, RoutedEventArgs e) => StartCapture(BindTarget.QuickToggle);
    private void BtnBindPrevPreset_Click(object s, RoutedEventArgs e) => StartCapture(BindTarget.PrevPreset);
    private void BtnBindNextPreset_Click(object s, RoutedEventArgs e) => StartCapture(BindTarget.NextPreset);

    // ── 点击方案 ────────────────────────────────

    private void LoadClickPresets()
    {
        var toolSettings = _settings.Click;
        var panel = ClickPresetPanel;
        panel.Children.Clear();

        for (int i = 0; i < toolSettings.Presets.Count; i++)
        {
            var preset = toolSettings.Presets[i];
            var idx = i;

            // use settings-card style
            var card = new Border
            {
                Style = (Style)FindResource("SettingsCardStyle"),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 16)
            };

            var sp = new StackPanel();

            // Preset name + summary
            var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition());
            headerRow.Children.Add(new TextBlock
            {
                Text = preset.Name,
                Foreground = (Brush)FindResource("AppleTextPrimaryBrush"),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            headerRow.Children.Add(new TextBlock
            {
                Text = preset.Summary,
                Foreground = (Brush)FindResource("AppleTextSecondaryBrush"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            Grid.SetColumn(headerRow.Children[1], 1);
            sp.Children.Add(headerRow);

            // Edit / Delete buttons
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var btnEdit = new Button
            {
                Content = "编辑",
                Style = (Style)FindResource("AppleSecondaryButtonStyle"),
                Width = 60,
                Height = 28,
                Margin = new Thickness(0, 0, 6, 0)
            };
            btnEdit.Click += (_, _) => EditPreset(toolSettings, idx);
            btnRow.Children.Add(btnEdit);

            if (toolSettings.Presets.Count > 1)
            {
                var btnDel = new Button
                {
                    Content = "删除",
                    Style = (Style)FindResource("AppleDangerButtonStyle"),
                    Width = 60,
                    Height = 28
                };
                btnDel.Click += (_, _) =>
                {
                    toolSettings.Presets.RemoveAt(idx);
                    if (toolSettings.ActivePresetIndex >= toolSettings.Presets.Count)
                        toolSettings.ActivePresetIndex = Math.Max(0, toolSettings.Presets.Count - 1);
                    LoadClickPresets();
                };
                btnRow.Children.Add(btnDel);
            }
            sp.Children.Add(btnRow);
            card.Child = sp;
            panel.Children.Add(card);
        }

        // Add new preset button
        var addCard = new Border
        {
            Style = (Style)FindResource("SettingsCardStyle"),
            Padding = new Thickness(12),
            Margin = new Thickness(0, 0, 0, 16)
        };
        var addBtn = new Button
        {
            Content = "+ 新建方案",
            Style = (Style)FindResource("ApplePrimaryButtonStyle"),
            Width = 110,
            Height = 32
        };
        addBtn.Click += (_, _) => EditPreset(toolSettings, -1);
        addCard.Child = addBtn;
        panel.Children.Add(addCard);
    }

    private void EditPreset(ClickToolSettings toolSettings, int index)
    {
        var isNew = index < 0;
        var preset = isNew ? new ClickPreset() : toolSettings.Presets[index].Clone();
        var dlg = new PresetEditDialog(preset, $"点击方案 - {(isNew ? "新建" : "编辑")}")
        {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        if (dlg.ShowDialog() == true)
        {
            if (isNew) { toolSettings.Presets.Add(preset); toolSettings.ActivePresetIndex = toolSettings.Presets.Count - 1; }
            else toolSettings.Presets[index] = preset;
            LoadClickPresets();
        }
    }

    // ── 钓鱼设置 ────────────────────────────────

    private void LoadFishingSettings()
    {
        var f = _settings.Fishing;
        TxtCaptureX.Text = f.CaptureXPercent.ToString("F1");
        TxtCaptureY.Text = f.CaptureYPercent.ToString("F1");
        TxtCaptureW.Text = f.CaptureWidthPercent.ToString("F1");
        TxtCaptureH.Text = f.CaptureHeightPercent.ToString("F1");
        SetPollingRadio(f.PollingIntervalMs);
        ChkAutoFish.IsChecked = f.AutoFishEnabled;
        ChkDebugLog.IsChecked = f.DebugLogOcr;
        ChkDebugOverlay.IsChecked = f.DebugOverlayEnabled;
        TxtFuzzyThreshold.Text = f.FuzzyMatchThreshold.ToString("F2");
        TxtCastPhrases.Text = string.Join(", ", f.CastPhrases);
        TxtBitePhrases.Text = string.Join(", ", f.BitePhrases);
        TxtReelPhrases.Text = string.Join(", ", f.ReelPhrases);
        TxtCastCooldown.Text = f.CastCooldownMs.ToString();
        TxtRecastDelay.Text = f.RecastDelayMs.ToString();
        TxtFishingTimeout.Text = f.FishingTimeoutMs.ToString();
        TxtReelingInTimeout.Text = f.ReelingInTimeoutMs.ToString();
        ChkAutoRecastIdle.IsChecked = f.AutoRecastFromIdleEnabled;
        TxtAutoRecastIdleDelay.Text = f.AutoRecastFromIdleDelayMs.ToString();
        ChkAutoSwitchRod.IsChecked = f.AutoSwitchRodEnabled;
        TxtSwitchRodDelay.Text = f.SwitchRodDelayMs.ToString();
        TxtSwitchRodRecast.Text = f.SwitchRodRecastMs.ToString();
        TxtBrokenPhrases.Text = string.Join(", ", f.BrokenPhrases);

        var rods = new[] { ChkRod1, ChkRod2, ChkRod3, ChkRod4, ChkRod5, ChkRod6, ChkRod7, ChkRod8, ChkRod9 };
        for (int i = 0; i < 9; i++)
            rods[i].IsChecked = i < f.RodSlots.Count ? f.RodSlots[i] : true;
    }

    private void SaveFishingSettings()
    {
        var f = _settings.Fishing;
        f.CaptureXPercent = double.Parse(TxtCaptureX.Text);
        f.CaptureYPercent = double.Parse(TxtCaptureY.Text);
        f.CaptureWidthPercent = double.Parse(TxtCaptureW.Text);
        f.CaptureHeightPercent = double.Parse(TxtCaptureH.Text);
        f.PollingIntervalMs = GetPollingValue();
        f.AutoFishEnabled = ChkAutoFish.IsChecked == true;
        f.DebugLogOcr = ChkDebugLog.IsChecked == true;
        f.DebugOverlayEnabled = ChkDebugOverlay.IsChecked == true;
        if (double.TryParse(TxtFuzzyThreshold.Text, out var th))
            f.FuzzyMatchThreshold = Math.Clamp(th, 0.10, 1.0);
        f.CastPhrases = ParsePhrases(TxtCastPhrases.Text);
        f.BitePhrases = ParsePhrases(TxtBitePhrases.Text);
        f.ReelPhrases = ParsePhrases(TxtReelPhrases.Text);
        f.CastCooldownMs = int.Parse(TxtCastCooldown.Text);
        f.RecastDelayMs = int.Parse(TxtRecastDelay.Text);
        f.FishingTimeoutMs = int.Parse(TxtFishingTimeout.Text);
        f.ReelingInTimeoutMs = int.Parse(TxtReelingInTimeout.Text);
        f.AutoRecastFromIdleEnabled = ChkAutoRecastIdle.IsChecked == true;
        if (int.TryParse(TxtAutoRecastIdleDelay.Text, out var idleDelay))
            f.AutoRecastFromIdleDelayMs = Math.Max(1000, idleDelay);
        f.AutoSwitchRodEnabled = ChkAutoSwitchRod.IsChecked == true;
        f.SwitchRodDelayMs = int.Parse(TxtSwitchRodDelay.Text);
        f.SwitchRodRecastMs = int.Parse(TxtSwitchRodRecast.Text);
        f.BrokenPhrases = ParsePhrases(TxtBrokenPhrases.Text);

        var rods = new[] { ChkRod1, ChkRod2, ChkRod3, ChkRod4, ChkRod5, ChkRod6, ChkRod7, ChkRod8, ChkRod9 };
        f.RodSlots.Clear();
        for (int i = 0; i < 9; i++)
            f.RodSlots.Add(rods[i].IsChecked == true);
    }

    // ── 保存 / 取消 ──────────────────────────────

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveFishingSettings();
            _settings.DoubleClickTrayToExit = ChkDoubleClickTrayExit.IsChecked == true;
            _settings.Fishing.DebugLogInput = ChkDebugLogInput.IsChecked == true;
            _settingsService.Save();
            _onSaved?.Invoke();
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    // ── 轮询 RadioButton 辅助 ────────────────────

    private void SetPollingRadio(int ms)
    {
        (ms switch { 250 => Rb250, 125 => Rb125, 100 => Rb100, _ => Rb200 }).IsChecked = true;
    }

    private int GetPollingValue()
    {
        if (Rb100.IsChecked == true) return 100;
        if (Rb125.IsChecked == true) return 125;
        if (Rb250.IsChecked == true) return 250;
        return 200;
    }

    private static List<string> ParsePhrases(string text) =>
        text.Split(new[] { ',', '，', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
}
