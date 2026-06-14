using System.Windows;
using System.Windows.Input;
using MC_Helper.Models;

namespace MC_Helper;

public partial class PresetEditDialog : Window
{
    private readonly ClickPreset _preset;

    public PresetEditDialog(ClickPreset preset, string title)
    {
        InitializeComponent();
        _preset = preset;
        TitleLabel.Text = title;

        TxtName.Text = preset.Name;

        if (preset.Button == ClickButton.Right)
            RbRight.IsChecked = true;
        else
            RbLeft.IsChecked = true;

        if (preset.Behavior == ClickBehavior.Hold)
            RbHold.IsChecked = true;
        else
            RbRapid.IsChecked = true;

        if (preset.TriggerMode == TriggerMode.HoldActive)
            RbTrigHold.IsChecked = true;
        else
            RbTrigToggle.IsChecked = true;

        TxtIntervalMs.Text = preset.IntervalMs.ToString();
        TxtHoldMs.Text = preset.HoldMs.ToString();

        OnBehaviorChanged(null!, null!);
    }

    private void OnBehaviorChanged(object sender, RoutedEventArgs e)
    {
        RapidSettings.IsEnabled = RbRapid.IsChecked == true;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _preset.Name = TxtName.Text.Trim();
            if (string.IsNullOrEmpty(_preset.Name))
                _preset.Name = "未命名";

            _preset.Button = RbRight.IsChecked == true ? ClickButton.Right : ClickButton.Left;
            _preset.Behavior = RbRapid.IsChecked == true ? ClickBehavior.Rapid : ClickBehavior.Hold;
            _preset.TriggerMode = RbTrigHold.IsChecked == true ? TriggerMode.HoldActive : TriggerMode.Toggle;
            _preset.IntervalMs = int.TryParse(TxtIntervalMs.Text, out var iv) ? Math.Max(10, iv) : 500;
            _preset.HoldMs = int.TryParse(TxtHoldMs.Text, out var hm) ? Math.Max(5, hm) : 50;

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"输入错误: {ex.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
