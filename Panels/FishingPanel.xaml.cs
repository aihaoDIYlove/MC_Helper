using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MC_Helper.Services;

namespace MC_Helper.Panels;

public partial class FishingPanel : UserControl
{
    private DetectionLoop? _detection;
    private FishingOverlay? _overlay;

    public event Action? SelectRegionRequested;

    public FishingPanel()
    {
        InitializeComponent();
    }

    public void Bind(DetectionLoop detection, FishingOverlay overlay)
    {
        _detection = detection;
        _overlay = overlay;

        _detection.StateChanged += OnDetectionStateChanged;
        _detection.FishStateChanged += OnFishStateChanged;
        _detection.RodSwitch.StateChanged += OnRodSwitchStateChanged;
        _detection.RodSwitch.AllExhausted += OnAllRodsExhausted;

        _overlay.SelectModeChanged += OnSelectModeChanged;
    }

    public void Unbind()
    {
        if (_detection != null)
        {
            _detection.StateChanged -= OnDetectionStateChanged;
            _detection.FishStateChanged -= OnFishStateChanged;
            _detection.RodSwitch.StateChanged -= OnRodSwitchStateChanged;
            _detection.RodSwitch.AllExhausted -= OnAllRodsExhausted;
        }
        if (_overlay != null)
            _overlay.SelectModeChanged -= OnSelectModeChanged;
    }

    private void BtnSelect_Click(object sender, RoutedEventArgs e)
    {
        SelectRegionRequested?.Invoke();
    }

    public void SetSelectingMode(bool selecting)
    {
        BtnSelect.Foreground = selecting
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x0A, 0x84, 0xFF))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xFF, 0x98, 0x98, 0x9D));
        BtnSelect.ToolTip = selecting ? "点击空白区域保存，Esc 恢复，Enter 确认" : "框选 OCR 截图区域";
    }

    private void OnSelectModeChanged(bool selecting)
    {
        Dispatcher.Invoke(() => SetSelectingMode(selecting));
    }

    private void OnDetectionStateChanged(bool running)
    {
        Dispatcher.Invoke(() =>
        {
            if (running)
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
            }
            else
            {
                StatusDot.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A));
                StateLabel.Text = "等待启用检测";
            }
        });
    }

    private void OnFishStateChanged(FishingState oldState, FishingState newState)
    {
        Dispatcher.Invoke(() =>
        {
            if (_detection?.RodSwitch.IsActive == true) return;

            StateLabel.Text = newState switch
            {
                FishingState.Idle => "未钓鱼 — 请手动抛竿",
                FishingState.Fishing => "钓鱼中 — 等待咬钩...",
                FishingState.ReelingIn => "收回中 — 已提竿",
                FishingState.ReeledIn => "已收回 — 准备重抛",
                FishingState.Probing => "探测中 — 确认鱼漂状态",
                _ => "状态未知"
            };
        });
    }

    private void OnRodSwitchStateChanged(RodSwitchState state)
    {
        Dispatcher.Invoke(() =>
        {
            var slot = _detection?.RodSwitch.CurrentSlot ?? 0;
            StateLabel.Text = state switch
            {
                RodSwitchState.WaitingForKeyPress => $"鱼竿损坏 — 即将切换到 #{slot + 1}...",
                RodSwitchState.WaitingForCast => $"已切换 — 等待抛竿...",
                RodSwitchState.Idle => "未钓鱼 — 请手动抛竿",
                _ => StateLabel.Text
            };
        });
    }

    private void OnAllRodsExhausted()
    {
        Dispatcher.Invoke(() =>
        {
            StateLabel.Text = "鱼竿已全部损坏 — 请更换";
        });
    }
}
