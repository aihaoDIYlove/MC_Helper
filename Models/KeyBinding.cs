using System.Text.Json.Serialization;

namespace MC_Helper.Models;

/// <summary>
/// 按键绑定模型 — 支持单键、组合键、鼠标侧键
/// </summary>
public class KeyBinding
{
    /// <summary>虚拟键码（System.Windows.Forms.Keys 或 VK_*）</summary>
    public int VkCode { get; set; }

    /// <summary>修饰符标志位: 1=Ctrl, 2=Shift, 4=Alt, 8=Win。0 表示无修饰符</summary>
    public int Modifiers { get; set; }

    /// <summary>true 表示这是鼠标侧键 (XButton1=1, XButton2=2)</summary>
    public bool IsMouseButton { get; set; }

    /// <summary>鼠标侧键编号: 1=XButton1, 2=XButton2</summary>
    public int MouseButton { get; set; }

    public KeyBinding()
    {
        VkCode = 0;
        Modifiers = 0;
        IsMouseButton = false;
        MouseButton = 0;
    }

    public KeyBinding(int vkCode, int modifiers = 0)
    {
        VkCode = vkCode;
        Modifiers = modifiers;
        IsMouseButton = false;
        MouseButton = 0;
    }

    public static KeyBinding Mouse(int button)
    {
        return new KeyBinding { IsMouseButton = true, MouseButton = button };
    }

    /// <summary>人类可读的显示文本，如 "Ctrl+O"、"-"、"鼠标侧键1"</summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            if (IsMouseButton)
                return MouseButton == 1 ? "鼠标侧键1 (X1)" : "鼠标侧键2 (X2)";

            var parts = new List<string>();
            if ((Modifiers & 0x2) != 0) parts.Add("Ctrl");
            if ((Modifiers & 0x4) != 0) parts.Add("Shift");
            if ((Modifiers & 0x1) != 0) parts.Add("Alt");
            if ((Modifiers & 0x8) != 0) parts.Add("Win");

            var keyName = VkCodeToName(VkCode);
            parts.Add(keyName);

            return string.Join("+", parts);
        }
    }

    public bool IsEmpty => !IsMouseButton && VkCode == 0;

    private static string VkCodeToName(int vk)
    {
        return vk switch
        {
            0x20 => "Space",
            0x0D => "Enter",
            0x1B => "Escape",
            0x09 => "Tab",
            0x08 => "Backspace",
            0x2E => "Delete",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "←",
            0x26 => "↑",
            0x27 => "→",
            0x28 => "↓",
            0x70 => "F1", 0x71 => "F2", 0x72 => "F3", 0x73 => "F4",
            0x74 => "F5", 0x75 => "F6", 0x76 => "F7", 0x77 => "F8",
            0x78 => "F9", 0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
            0xBD => "-", 0xBB => "=",
            0xDB => "[", 0xDD => "]",
            0xDC => "\\", 0xBA => ";",
            0xDE => "'", 0xBC => ",",
            0xBE => ".", 0xBF => "/",
            0xC0 => "`",
            >= 0x30 and <= 0x39 => ((char)vk).ToString(),
            >= 0x41 and <= 0x5A => ((char)vk).ToString(),
            _ => $"0x{vk:X}"
        };
    }
}
