using MC_Helper.Helpers;
using MC_Helper.Models;

namespace MC_Helper.Services;

/// <summary>
/// 截取区域百分比 ↔ 物理像素互转。
/// 百分比基于物理分辨率（不受 DPI 缩放影响），与 CopyFromScreen 坐标系一致。
/// </summary>
public static class CaptureRegionHelper
{
    /// <summary>百分比 → 物理像素矩形</summary>
    public static (int x, int y, int w, int h) ToPixels(FishingSettings s)
    {
        var sw = Win32.PhysicalScreenWidth;
        var sh = Win32.PhysicalScreenHeight;
        return (
            (int)(s.CaptureXPercent / 100.0 * sw),
            (int)(s.CaptureYPercent / 100.0 * sh),
            Math.Max(1, (int)(s.CaptureWidthPercent / 100.0 * sw)),
            Math.Max(1, (int)(s.CaptureHeightPercent / 100.0 * sh))
        );
    }

    /// <summary>物理像素 → 百分比，回写 settings</summary>
    public static void FromPixels(int x, int y, int w, int h, FishingSettings s)
    {
        var sw = Win32.PhysicalScreenWidth;
        var sh = Win32.PhysicalScreenHeight;
        s.CaptureXPercent = Math.Round(x / (double)sw * 100.0, 1);
        s.CaptureYPercent = Math.Round(y / (double)sh * 100.0, 1);
        s.CaptureWidthPercent = Math.Round(w / (double)sw * 100.0, 1);
        s.CaptureHeightPercent = Math.Round(h / (double)sh * 100.0, 1);
    }
}
