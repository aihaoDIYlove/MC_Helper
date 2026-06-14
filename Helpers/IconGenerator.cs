using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace MC_Helper.Helpers;

public static class IconGenerator
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon CreateFishIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var bodyBrush = new LinearGradientBrush(
                new Point(4, 16), new Point(24, 16),
                Color.FromArgb(0, 200, 200), Color.FromArgb(0, 120, 200));
            g.FillEllipse(bodyBrush, 3, 10, 22, 12);

            var tail = new Point[] {
                new(25, 10), new(30, 4), new(28, 16), new(30, 28), new(25, 22)
            };
            using var tailBrush = new SolidBrush(Color.FromArgb(0, 200, 200));
            g.FillPolygon(tailBrush, tail);

            g.FillEllipse(Brushes.White, 10, 13, 5, 5);
            g.FillEllipse(Brushes.Black, 11, 14, 3, 3);

            using var finPen = new Pen(Color.FromArgb(0, 180, 200), 2);
            g.DrawLine(finPen, 14, 6, 12, 10);
            g.DrawLine(finPen, 18, 6, 16, 10);
        }

        var hIcon = bmp.GetHicon();
        try { return (Icon)Icon.FromHandle(hIcon).Clone(); }
        finally { DestroyIcon(hIcon); }
    }
}
