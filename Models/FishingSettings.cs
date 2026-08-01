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
    /// <summary>水中状态短语：鱼漂在水中时持续出现的字幕（咬钩时是额外的"浮漂溅起水花"）</summary>
    public List<string> SplashPhrases { get; set; } = new() { "溅起水花" };

    public int CastCooldownMs { get; set; } = 2000;
    public int RecastDelayMs { get; set; } = 400;
    /// <summary>待抛竿状态下，持续识别到水中短语多久 (ms) 确认鱼漂在水中（默认 3 秒）</summary>
    public int CastSplashConfirmMs { get; set; } = 3000;
    /// <summary>钓鱼中，水中短语消失多久 (ms) 判定鱼漂不在水中，触发状态探测（默认 4 秒，须大于抛竿后鱼漂落水前的飞行时长）</summary>
    public int NoSplashTimeoutMs { get; set; } = 4000;

    public bool AutoFishEnabled { get; set; } = true;

    /// <summary>空闲超时自动抛竿：停在 待抛竿 状态超过此时间后自动右键抛竿（如意外收杆）</summary>
    public bool AutoRecastFromIdleEnabled { get; set; } = true;
    /// <summary>空闲超时自动抛竿延迟 (ms)，默认 10 秒</summary>
    public int AutoRecastFromIdleDelayMs { get; set; } = 10_000;

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
