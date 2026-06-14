using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MC_Helper.Models;
using MC_Helper.Services;

namespace MC_Helper.Panels;

public partial class ClickToolPanel : UserControl
{
    private ClickToolSettings? _settings;
    private ClickTool? _tool;

    public ClickToolPanel()
    {
        InitializeComponent();
    }

    public void Bind(ClickToolSettings settings, ClickTool tool)
    {
        _settings = settings;
        _tool = tool;
        _tool.StateChanged += OnToolStateChanged;
        RefreshPresets();
    }

    public void Unbind()
    {
        if (_tool != null)
            _tool.StateChanged -= OnToolStateChanged;
    }

    public void RefreshPresets()
    {
        if (_settings == null) return;
        PresetCombo.Items.Clear();
        foreach (var p in _settings.Presets)
            PresetCombo.Items.Add($"{p.Name}  [{p.Summary}]");
        if (_settings.Presets.Count > 0)
            PresetCombo.SelectedIndex = Math.Min(_settings.ActivePresetIndex, _settings.Presets.Count - 1);
    }

    /// <summary>仅更新方案下拉列表，不触发事件</summary>
    public void SyncPresetIndex()
    {
        if (_settings == null) return;
        if (_settings.ActivePresetIndex >= 0 && _settings.ActivePresetIndex < PresetCombo.Items.Count)
            PresetCombo.SelectedIndex = _settings.ActivePresetIndex;
    }

    public void UpdateFromSettings()
    {
        RefreshPresets();
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || PresetCombo.SelectedIndex < 0) return;
        _settings.ActivePresetIndex = PresetCombo.SelectedIndex;
    }

    public void SetRunning(bool running)
    {
        if (running)
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
            StatusLabel.Text = $"● {_settings?.ActivePreset.Name ?? ""}";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
        }
        else
        {
            StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A));
            StatusLabel.Text = "未启动";
            StatusLabel.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x98, 0x98, 0x9D));
        }
    }

    private void OnToolStateChanged(bool running)
    {
        Dispatcher.Invoke(() => SetRunning(running));
    }
}
