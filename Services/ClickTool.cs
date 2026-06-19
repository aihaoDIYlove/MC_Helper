using System.Windows.Threading;
using MC_Helper.Helpers;
using MC_Helper.Models;

namespace MC_Helper.Services;

/// <summary>
/// 自动化点击工具 — 支持连点 (Rapid) 和长按 (Hold)。
/// HoldActive 松手检测完全由低层钩子 KeyUp 驱动，ClickTool 不再参与。
/// </summary>
public class ClickTool : IDisposable
{
    private readonly ClickToolSettings _settings;
    private readonly InputService _input;
    private readonly DispatcherTimer _timer;
    private bool _holding;
    private bool _phaseDown;

    public bool IsRunning { get; private set; }

    /// <summary>由外部同步：设置 → DetectionLoop → InputService.DebugLogInput，同时也同步到这里</summary>
    public bool DebugLog { get; set; }

    public event Action<bool>? StateChanged;

    public ClickTool(ClickToolSettings settings, InputService input)
    {
        _settings = settings;
        _input = input;

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(100),
            DispatcherPriority.Background,
            OnTick,
            Dispatcher.CurrentDispatcher);
        _timer.Stop();
    }

    /// <summary>
    /// 启动点击作业。松手检测由外部低层钩子 → ModeManager.DeactivateCurrent() → Stop() 驱动。
    /// </summary>
    public void Start()
    {
        if (IsRunning)
        {
            if (DebugLog) Logger.Info("[ClickTool] Start 忽略（已在运行）");
            return;
        }

        var preset = _settings.ActivePreset;
        if (preset.Behavior == ClickBehavior.Hold)
        {
            if (DebugLog) Logger.Info($"[ClickTool] Start → DoDown({preset.Button}) [Hold]");
            DoDown(preset.Button);
            _holding = true;
        }
        else
        {
            if (DebugLog) Logger.Info($"[ClickTool] Start → timer 启动 [Rapid {preset.IntervalMs}ms]");
            _phaseDown = true;
            _timer.Interval = TimeSpan.FromMilliseconds(1);
            _timer.Start();
        }

        IsRunning = true;
        StateChanged?.Invoke(true);
        Logger.Info($"ClickTool 启动: {preset.Name} {preset.Summary}");
    }

    /// <summary>
    /// 停止点击作业。先重置状态再释放鼠标，确保即使 DoUp 抛异常也不会残留 stuck 状态。
    /// </summary>
    public void Stop()
    {
        if (!IsRunning)
        {
            if (DebugLog) Logger.Info("[ClickTool] Stop 忽略（未运行）");
            return;
        }

        _timer.Stop();

        // _phaseDown=true  → 下一个 tick 发 DoDown（鼠标 UP），无需 DoUp
        // _phaseDown=false → 刚发了 DoDown（鼠标 DOWN），必须 DoUp 释放
        var needUp = _holding || !_phaseDown;
        var button = needUp ? _settings.ActivePreset.Button : ClickButton.Left;

        if (DebugLog)
        {
            var flag = _holding ? "holding" : (_phaseDown ? "phaseDown" : "none");
            Logger.Info($"[ClickTool] Stop → needUp={needUp} flag={flag} button={button}");
        }

        // 先重置所有状态标记，防止异常或重入导致 stuck
        _holding = false;
        _phaseDown = false;
        IsRunning = false;

        if (needUp)
        {
            try
            {
                if (DebugLog) Logger.Info($"[ClickTool] Stop → DoUp({button})");
                DoUp(button);
            }
            catch (Exception ex) { Logger.Error("ClickTool.Stop DoUp 异常", ex); }
        }

        // 兜底清理：无脑发送 LEFTUP + RIGHTUP，防止任何残留的按键按下状态卡死鼠标
        // 已经 UP 的键再发 UP 是空操作，不会影响游戏
        try { _input.SendLeftUp(); } catch { }
        try { _input.SendRightUp(); } catch { }

        StateChanged?.Invoke(false);
        Logger.Info("ClickTool 停止");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (!IsRunning) { _timer.Stop(); return; }

        var preset = _settings.ActivePreset;
        if (preset.Behavior == ClickBehavior.Hold) return;

        if (_phaseDown)
        {
            DoDown(preset.Button);
            _phaseDown = false;
            _timer.Interval = TimeSpan.FromMilliseconds(preset.HoldMs);
        }
        else
        {
            DoUp(preset.Button);
            _phaseDown = true;
            _timer.Interval = TimeSpan.FromMilliseconds(preset.IntervalMs);
        }
    }

    private void DoDown(ClickButton button)
    {
        if (DebugLog) Logger.Info($"[ClickTool] DoDown({button})");
        if (button == ClickButton.Left)
            _input.SendLeftDown();
        else
            _input.SendRightDown();
    }

    private void DoUp(ClickButton button)
    {
        if (DebugLog) Logger.Info($"[ClickTool] DoUp({button})");
        if (button == ClickButton.Left)
            _input.SendLeftUp();
        else
            _input.SendRightUp();
    }

    public void Dispose()
    {
        Stop();
    }
}
