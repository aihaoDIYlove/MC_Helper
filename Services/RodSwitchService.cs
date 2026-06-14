using MC_Helper.Helpers;
using MC_Helper.Models;

namespace MC_Helper.Services;

public enum RodSwitchState
{
    Idle,
    WaitingForKeyPress,
    WaitingForCast
}

public class RodSwitchService
{
    private readonly FishingSettings _settings;
    private readonly InputService _input;
    private DateTime _stateStartedAt;
    private int _targetSlot;     // 即将按下的槽位索引 (0-8)
    private int _targetVkCode;   // 目标虚拟键码 (0x31-0x39)

    public RodSwitchState State { get; private set; } = RodSwitchState.Idle;
    public bool IsActive => State != RodSwitchState.Idle;

    public int CurrentSlot { get; private set; }

    public event Action<RodSwitchState>? StateChanged;
    public event Action? AllExhausted;
    public event Action<string>? DebugInfo;

    public RodSwitchService(FishingSettings settings, InputService input)
    {
        _settings = settings;
        _input = input;
    }

    /// <summary>开始切换流程，查找下一个启用槽位。返回 false 表示全部耗尽。</summary>
    public bool Trigger(int currentSlot)
    {
        if (IsActive) return true;

        var nextSlot = FindNextEnabledSlot(currentSlot);
        if (nextSlot == -1)
        {
            // 所有鱼竿已耗尽
            AllExhausted?.Invoke();
            DebugInfo?.Invoke("所有鱼竿已损坏");
            return false;
        }

        _targetSlot = nextSlot;
        _targetVkCode = 0x31 + nextSlot; // '1' = 0x31, '9' = 0x39
        _stateStartedAt = DateTime.Now;
        State = RodSwitchState.WaitingForKeyPress;
        StateChanged?.Invoke(State);

        Logger.Info($"切杆触发: 槽位 {currentSlot + 1} → {_targetSlot + 1}");
        DebugInfo?.Invoke($"鱼竿损坏 — 即将切换到 #{_targetSlot + 1}...");
        return true;
    }

    /// <summary>推进切杆时序，由 DetectionLoop.OnTick 每轮调用。</summary>
    public void Tick()
    {
        if (!IsActive) return;

        var now = DateTime.Now;
        var elapsed = (now - _stateStartedAt).TotalMilliseconds;

        switch (State)
        {
            case RodSwitchState.WaitingForKeyPress:
                if (elapsed >= _settings.SwitchRodDelayMs)
                {
                    _input.SendKey(_targetVkCode);
                    State = RodSwitchState.WaitingForCast;
                    _stateStartedAt = now;
                    StateChanged?.Invoke(State);
                    Logger.Info($"按下数字键 {_targetSlot + 1} (0x{_targetVkCode:X})");
                    DebugInfo?.Invoke($"已切换 — 等待抛竿...");
                }
                break;

            case RodSwitchState.WaitingForCast:
                if (elapsed >= _settings.SwitchRodRecastMs)
                {
                    _input.SendRightClick();
                    CurrentSlot = _targetSlot;
                    State = RodSwitchState.Idle;
                    _stateStartedAt = DateTime.MinValue;
                    StateChanged?.Invoke(State);
                    Logger.Info($"切杆完成: 当前槽位={CurrentSlot + 1}");
                    DebugInfo?.Invoke($"切杆完成 — 已抛竿");
                }
                break;
        }
    }

    /// <summary>重置为第一个启用槽位（开始检测时调用）。</summary>
    public void ResetSlot()
    {
        State = RodSwitchState.Idle;
        _stateStartedAt = DateTime.MinValue;
        var first = FindFirstEnabledSlot();
        CurrentSlot = first >= 0 ? first : 0;
        Logger.Info($"切杆槽位初始化: CurrentSlot={CurrentSlot + 1}");
    }

    private int FindFirstEnabledSlot()
    {
        var slots = _settings.RodSlots;
        int count = slots.Count;
        for (int i = 0; i < 9 && i < count; i++)
        {
            if (slots[i])
                return i;
        }
        return -1;
    }

    private int FindNextEnabledSlot(int currentSlot)
    {
        var slots = _settings.RodSlots;
        int count = slots.Count;
        for (int i = currentSlot + 1; i < 9 && i < count; i++)
        {
            if (slots[i])
                return i;
        }
        return -1;
    }
}
