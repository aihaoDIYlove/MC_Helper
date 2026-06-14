using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MC_Helper.Services;

public static class ScreenCapture
{
    public static SoftwareBitmap CaptureRegion(int x, int y, int width, int height)
    {
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);
        g.CopyFromScreen(x, y, 0, 0, new Size(width, height));

        var rect = new Rectangle(0, 0, width, height);
        var bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

        try
        {
            var softwareBitmap = new SoftwareBitmap(
                BitmapPixelFormat.Bgra8,
                width, height,
                BitmapAlphaMode.Premultiplied);

            int byteCount = bmpData.Stride * height;
            byte[] pixelBytes = new byte[byteCount];
            Marshal.Copy(bmpData.Scan0, pixelBytes, 0, byteCount);

            using var dataWriter = new DataWriter();
            dataWriter.WriteBytes(pixelBytes);
            var buffer = dataWriter.DetachBuffer();

            softwareBitmap.CopyFromBuffer(buffer);
            return softwareBitmap;
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }
    }
}
