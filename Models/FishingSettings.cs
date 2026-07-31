namespace MC_Helper.Models;

/// <summary>
/// 钓鱼模块设置
/// </summary>
public class FishingSettings
{
    /// <summary>截取区域 X 起点（屏幕宽度百分比，0-100）</summary>
    public double CaptureXPercent { get; set; } = 84.8;
    /// <summary>截取区域 Y 起点（屏幕高度百分比，0-100）</summary>
    public double CaptureYPercent { get; set; } = 49.2;
    /// <summary>截取区域宽度（屏幕宽度百分比，0-100）</summary>
    public double CaptureWidthPercent { get; set; } = 14.6;
    /// <summary>截取区域高度（屏幕高度百分比，0-100）</summary>
    public double CaptureHeightPercent { get; set; } = 36.6;
    public int PollingIntervalMs { get; set; } = 200;

    public List<string> CastPhrases { get; set; } = new() { "浮漂甩出" };
    public List<string> BitePhrases { get; set; } = new() { "浮漂溅起水花" };
    public List<string> ReelPhrases { get; set; } = new() { "浮漂收回" };

    public int CastCooldownMs { get; set; } = 2000;
    public int RecastDelayMs { get; set; } = 400;
    /// <summary>ReelingIn 超时兜底 (ms)：提杆后最久等多久未识别到"收回"，强制重抛</summary>
    public int ReelingInTimeoutMs { get; set; } = 6000;

    /// <summary>钓鱼超时 (ms)：等待鱼上钩超过此时间未检测到咬钩，强制重抛（防止勾到溺尸等）</summary>
    public int FishingTimeoutMs { get; set; } = 120_000; // 2 分钟

    public bool AutoFishEnabled { get; set; } = true;

    /// <summary>空闲超时自动抛竿：停在 Idle 超过此时间后自动右键抛竿（如意外收杆）</summary>
    public bool AutoRecastFromIdleEnabled { get; set; } = true;
    /// <summary>空闲超时自动抛竿延迟 (ms)，默认 15 秒</summary>
    public int AutoRecastFromIdleDelayMs { get; set; } = 15_000;

    public bool DebugLogOcr { get; set; } = false;
    public bool DebugOverlayEnabled { get; set; } = false;
    public double FuzzyMatchThreshold { get; set; } = 0.75;

    // --- 自动更换鱼竿 ---
    public bool AutoSwitchRodEnabled { get; set; } = false;
    public bool DebugLogInput { get; set; } = false;
    public int SwitchRodDelayMs { get; set; } = 500;
    public int SwitchRodRecastMs { get; set; } = 1200;
    public List<string> BrokenPhrases { get; set; } = new() { "物品损坏" };
    public List<bool> RodSlots { get; set; } = new()
    {
        true, true, true, true, true, true, true, true, true
    };
}
