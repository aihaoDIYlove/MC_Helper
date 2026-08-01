using System.Windows.Threading;
using MC_Helper.Helpers;
using MC_Helper.Models;

namespace MC_Helper.Services;

public class DetectionLoop : IDisposable
{
    private readonly FishingSettings _settings;
    private readonly OcrService _ocr;
    private readonly InputService _input;
    private readonly FishingStateMachine _fsm;
    private readonly RodSwitchService _rodSwitch;
    private readonly DispatcherTimer _timer;
    private int _tickCount;

    public event Action<bool>? StateChanged;
    public event Action<string>? TextRecognized;
    public event Action<FishingState, FishingState>? FishStateChanged;
    public event Action<string>? DebugInfo;
    /// <summary>调试面板显隐切换请求（由 App 层同步到 FishingOverlay）</summary>
    public event Action<bool>? DebugOverlayChanged;

    public RodSwitchService RodSwitch => _rodSwitch;

    private bool _running;
    public bool IsRunning
    {
        get => _running;
        private set
        {
            if (_running != value)
            {
                _running = value;
                StateChanged?.Invoke(value);
            }
        }
    }

    public DetectionLoop(FishingSettings settings, OcrService ocr, InputService input, RodSwitchService rodSwitch)
    {
        _settings = settings;
        _ocr = ocr;
        _input = input;
        _rodSwitch = rodSwitch;

        _fsm = new FishingStateMachine(settings);
        _fsm.RightClickRequested += OnRightClickRequested;
        _fsm.StateChanged += (old, @new) => FishStateChanged?.Invoke(old, @new);
        _fsm.DebugInfo += msg => DebugInfo?.Invoke(msg);
        _fsm.RodBrokenDetected += OnRodBrokenDetected;

        _rodSwitch.DebugInfo += msg => DebugInfo?.Invoke(msg);

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(settings.PollingIntervalMs),
            DispatcherPriority.Background,
            OnTick,
            Dispatcher.CurrentDispatcher);
        _timer.Stop(); // 四参数构造自动启动，显式停止
    }

    public void Start()
    {
        if (IsRunning) return;
        SyncDebugFlags();
        if (!_ocr.IsAvailable)
        {
            var msg = "OCR 引擎不可用。\n请安装中文语言包的光学字符识别组件：\n设置 → 时间和语言 → 语言 → 添加语言 → 中文(简体) → 可选功能 → 光学字符识别";
            DebugInfo?.Invoke(msg);
            System.Windows.MessageBox.Show(msg, "MC_Helper", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            return;
        }
        _tickCount = 0;
        _fsm.Reset();
        _rodSwitch.ResetSlot();
        _timer.Interval = TimeSpan.FromMilliseconds(_settings.PollingIntervalMs);
        _timer.Start();
        IsRunning = true;
        var (lx, ly, lw, lh) = CaptureRegionHelper.ToPixels(_settings);
        Logger.Info($"检测已启动: 区域=({lx},{ly}) {lw}x{lh} 间隔={_settings.PollingIntervalMs}ms");
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _timer.Stop();
        IsRunning = false;
        Logger.Info("检测已停止");
        TextRecognized?.Invoke("");
        DebugInfo?.Invoke("已停止");
    }

    public void ResetStateMachine()
    {
        _fsm.Reset();
        _rodSwitch.ResetSlot();
        SyncDebugFlags();
        DebugInfo?.Invoke("状态机已重置");
    }

    private void SyncDebugFlags()
    {
        _input.DebugLogInput = _settings.DebugLogInput;
        DebugOverlayChanged?.Invoke(_settings.DebugOverlayEnabled);
    }

    private void OnRightClickRequested(string reason)
    {
        Logger.Info($"右键触发: {reason}");
        DebugInfo?.Invoke($"右键: {reason}");
        _input.SendRightClick();
    }

    private void OnRodBrokenDetected(int _)
    {
        var triggered = _rodSwitch.Trigger(_rodSwitch.CurrentSlot);
        if (!triggered)
        {
            DebugInfo?.Invoke("鱼竿已全部损坏 — 请更换");
            Stop();
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tickCount++;

        // 切杆进行中则推进时序，跳过本轮 OCR/FSM
        if (_rodSwitch.IsActive)
        {
            try { _rodSwitch.Tick(); }
            catch (Exception ex)
            {
                Logger.Error("切杆时序异常", ex);
                DebugInfo?.Invoke($"切杆异常: {ex.Message}");
                _rodSwitch.ResetSlot();
            }
            return;
        }

        try
        {
            var (cx, cy, cw, ch) = CaptureRegionHelper.ToPixels(_settings);
            using var frame = ScreenCapture.CaptureRegion(cx, cy, cw, ch);

            IReadOnlyList<string> lines;
            try
            {
                lines = _ocr.RecognizeLines(frame);
            }
            catch (Exception ex)
            {
                Logger.Error("OCR 异常", ex);
                DebugInfo?.Invoke($"OCR 异常: {ex.Message}");
                return;
            }

            var joined = string.Join(" | ", lines);
            TextRecognized?.Invoke(joined);

            if (_settings.DebugLogOcr && lines.Count > 0)
                Logger.Info($"OCR: [{joined}]");

            if (_tickCount % 10 == 0 && lines.Count > 0)
                DebugInfo?.Invoke($"OCR({lines.Count}行): {joined}");

            // 始终调用 Process，即使 OCR 返回空行。
            // 时间驱动的状态（ReeledIn 倒计时重抛、水花消失判定）不依赖 OCR 结果。
            _fsm.Process(lines);
        }
        catch (Exception ex)
        {
            Logger.Error("检测循环异常", ex);
            DebugInfo?.Invoke($"循环异常: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
