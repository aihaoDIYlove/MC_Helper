using System.Text.Json.Serialization;

namespace MC_Helper.Models;

public enum ClickButton { Left, Right }

public enum ClickBehavior
{
    Rapid,
    Hold
}

public enum TriggerMode
{
    /// <summary>按 O 切换开/关</summary>
    Toggle,
    /// <summary>按住 O 才启用，松开即停</summary>
    HoldActive
}

/// <summary>
/// 点击方案 — 含左右键、行为、间隔
/// </summary>
public class ClickPreset
{
    public string Name { get; set; } = "默认方案";
    public ClickButton Button { get; set; } = ClickButton.Left;
    public ClickBehavior Behavior { get; set; } = ClickBehavior.Rapid;
    public TriggerMode TriggerMode { get; set; } = TriggerMode.Toggle;
    public int IntervalMs { get; set; } = 500;
    public int HoldMs { get; set; } = 50;

    [JsonIgnore]
    public string ButtonLabel => Button == ClickButton.Left ? "左键" : "右键";
    [JsonIgnore]
    public string BehaviorLabel => Behavior == ClickBehavior.Hold ? "长按" : "连点";
    [JsonIgnore]
    public string Summary => $"{ButtonLabel} · {BehaviorLabel}";

    public ClickPreset Clone()
    {
        return new ClickPreset
        {
            Name = Name,
            Button = Button,
            Behavior = Behavior,
            TriggerMode = TriggerMode,
            IntervalMs = IntervalMs,
            HoldMs = HoldMs
        };
    }
}

/// <summary>
/// 点击工具设置（合并左键/右键）
/// </summary>
public class ClickToolSettings
{
    public List<ClickPreset> Presets { get; set; } = new()
    {
        new ClickPreset { Name = "挂机砍怪", Button = ClickButton.Left, Behavior = ClickBehavior.Rapid, IntervalMs = 1200, HoldMs = 50, TriggerMode = TriggerMode.Toggle },
        new ClickPreset { Name = "长按挖掘", Button = ClickButton.Left, Behavior = ClickBehavior.Hold, TriggerMode = TriggerMode.Toggle },
        new ClickPreset { Name = "破基岩", Button = ClickButton.Right, Behavior = ClickBehavior.Rapid, IntervalMs = 30, HoldMs = 20, TriggerMode = TriggerMode.HoldActive },
        new ClickPreset { Name = "树场种植", Button = ClickButton.Right, Behavior = ClickBehavior.Hold, TriggerMode = TriggerMode.Toggle }
    };

    public int ActivePresetIndex { get; set; } = 0;

    public bool Enabled { get; set; } = false;

    [JsonIgnore]
    public ClickPreset ActivePreset =>
        Presets.Count > 0 && ActivePresetIndex >= 0 && ActivePresetIndex < Presets.Count
            ? Presets[ActivePresetIndex]
            : Presets[0];
}
