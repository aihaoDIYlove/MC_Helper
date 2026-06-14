using MC_Helper.Models;

namespace MC_Helper.Services;

public enum ToolMode
{
    Click = 0,
    Fishing = 1
}

/// <summary>
/// 模式管理器 — 注册/切换/启停，通过事件通知 UI
/// </summary>
public class ModeManager
{
    private readonly RootSettings _settings;

    private ToolMode _currentMode;
    private bool _isRunning;

    public ToolMode CurrentMode => _currentMode;
    public bool IsRunning => _isRunning;

    /// <summary>模式切换，UI 更新面板</summary>
    public event Action<ToolMode>? ModeChanged;

    /// <summary>运行状态变化</summary>
    public event Action<bool>? RunningChanged;

    /// <summary>当前模式的工具请求启动</summary>
    public event Action? StartRequested;

    /// <summary>当前模式的工具请求停止</summary>
    public event Action? StopRequested;

    /// <summary>点击模式内切换方案 (delta: -1 上一个, +1 下一个)</summary>
    public event Action<int>? PresetSwitchRequested;

    public ModeManager(RootSettings settings)
    {
        _settings = settings;
        _currentMode = (ToolMode)settings.ModeSwitching.CurrentModeIndex;
    }

    public void SwitchTo(ToolMode mode)
    {
        if (_currentMode == mode) return;

        if (_isRunning)
        {
            StopRequested?.Invoke();
            _isRunning = false;
            RunningChanged?.Invoke(false);
        }

        _currentMode = mode;
        _settings.ModeSwitching.CurrentModeIndex = (int)mode;
        ModeChanged?.Invoke(mode);
    }

    public void PrevMode()
    {
        var modes = Enum.GetValues<ToolMode>();
        int idx = Array.IndexOf(modes, _currentMode);
        int newIdx = (idx - 1 + modes.Length) % modes.Length;
        SwitchTo(modes[newIdx]);
    }

    public void NextMode()
    {
        var modes = Enum.GetValues<ToolMode>();
        int idx = Array.IndexOf(modes, _currentMode);
        int newIdx = (idx + 1) % modes.Length;
        SwitchTo(modes[newIdx]);
    }

    /// <summary>切换方案 — 仅点击模式有效</summary>
    public void PrevPreset() => PresetSwitchRequested?.Invoke(-1);
    public void NextPreset() => PresetSwitchRequested?.Invoke(+1);

    /// <summary>快速启停当前模式的工具</summary>
    public void ToggleCurrent()
    {
        if (_isRunning)
        {
            StopRequested?.Invoke();
            _isRunning = false;
        }
        else
        {
            StartRequested?.Invoke();
            _isRunning = true;
        }
        RunningChanged?.Invoke(_isRunning);
    }

    public void StopCurrent()
    {
        if (!_isRunning) return;
        StopRequested?.Invoke();
        _isRunning = false;
        RunningChanged?.Invoke(false);
    }

    /// <summary>仅启动 — HoldActive 模式按键按下时调用</summary>
    public void ActivateCurrent()
    {
        if (_isRunning) return;
        StartRequested?.Invoke();
        _isRunning = true;
        RunningChanged?.Invoke(true);
    }

    /// <summary>仅停止 — HoldActive 模式按键松开时调用</summary>
    public void DeactivateCurrent()
    {
        if (!_isRunning) return;
        StopRequested?.Invoke();
        _isRunning = false;
        RunningChanged?.Invoke(false);
    }

    public void SetRunning(bool running)
    {
        if (_isRunning == running) return;
        _isRunning = running;
        RunningChanged?.Invoke(running);
    }

    public static string GetModeName(ToolMode mode) => mode switch
    {
        ToolMode.Click => "🖱 点击模式",
        ToolMode.Fishing => "🎣 钓鱼",
        _ => "未知"
    };
}
