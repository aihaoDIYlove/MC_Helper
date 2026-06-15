namespace MC_Helper.Models;

/// <summary>
/// 顶层设置模型 — 序列化到 %AppData%/MC_Helper/settings.json
/// </summary>
public class RootSettings
{
    public ModeSettings ModeSwitching { get; set; } = new();
    public ClickToolSettings Click { get; set; } = new();
    public FishingSettings Fishing { get; set; } = new();

    /// <summary>双击托盘图标强制退出</summary>
    public bool DoubleClickTrayToExit { get; set; } = true;
}
