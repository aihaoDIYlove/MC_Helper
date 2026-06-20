namespace MC_Helper.Models;

/// <summary>
/// 模式切换设置
/// </summary>
public class ModeSettings
{
    /// <summary>当前活跃的模式索引</summary>
    public int CurrentModeIndex { get; set; } = 0;

    /// <summary>切换模式按键 (默认 =)</summary>
    public KeyBinding ToggleModeKey { get; set; } = new(0xBB); // VK_OEM_PLUS

    /// <summary>快速启停按键 (默认 O)</summary>
    public KeyBinding QuickToggleKey { get; set; } = new(0xC0); // '`' (VK_OEM_3)

    /// <summary>上一个方案按键（点击模式，默认 [）</summary>
    public KeyBinding PrevPresetKey { get; set; } = new(0xDB); // VK_OEM_4 = '['

    /// <summary>下一个方案按键（点击模式，默认 ]）</summary>
    public KeyBinding NextPresetKey { get; set; } = new(0xDD); // VK_OEM_6 = ']'
}
