using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace MC_Helper.Services;

public class OcrService
{
    private OcrEngine? _engine;
    private int _engineThreadId = -1;
    private readonly object _engineLock = new();

    public bool IsAvailable
    {
        get
        {
            TryEnsureEngine();
            return _engine != null;
        }
    }

    public OcrService()
    {
        TryEnsureEngine();
    }

    public IReadOnlyList<string> RecognizeLines(SoftwareBitmap bitmap)
    {
        var engine = GetEngineForCurrentThread();
        if (engine == null) return Array.Empty<string>();

        try
        {
            using var normalized = NormalizeForOcr(bitmap);
            var input = normalized ?? bitmap;

            var result = engine.RecognizeAsync(input)
                .AsTask().GetAwaiter().GetResult();

            var lines = new List<string>(result.Lines.Count);
            foreach (var line in result.Lines)
            {
                var text = line.Text.Trim();
                if (!string.IsNullOrEmpty(text))
                    lines.Add(text);
            }
            return lines;
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("InvalidCast") || ex.Message.Contains("IInspectable"))
            {
                lock (_engineLock) { _engine = null; _engineThreadId = -1; }
            }
            throw;
        }
    }

    private void TryEnsureEngine()
    {
        lock (_engineLock)
        {
            if (_engine == null)
            {
                string dummy;
                _engine = TryCreateEngine(out dummy);
                _engineThreadId = Environment.CurrentManagedThreadId;
            }
        }
    }

    private OcrEngine? GetEngineForCurrentThread()
    {
        var tid = Environment.CurrentManagedThreadId;
        lock (_engineLock)
        {
            if (_engine != null && _engineThreadId == tid)
                return _engine;

            string dummy;
            _engine = TryCreateEngine(out dummy);
            _engineThreadId = tid;
            return _engine;
        }
    }

    private static SoftwareBitmap? NormalizeForOcr(SoftwareBitmap bitmap)
    {
        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Gray8)
            return null;

        if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 &&
            bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied)
            return null;

        try
        {
            return SoftwareBitmap.Convert(
                bitmap,
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);
        }
        catch
        {
            return null;
        }
    }

    private static OcrEngine? TryCreateEngine(out string info)
    {
        try
        {
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine != null)
            {
                info = $"OCR: {engine.RecognizerLanguage.DisplayName} (user)";
                return engine;
            }
        }
        catch { }

        try
        {
            var lang = new Windows.Globalization.Language("en-US");
            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine != null)
            {
                info = $"OCR: {engine.RecognizerLanguage.DisplayName} (en-US)";
                return engine;
            }
        }
        catch { }

        try
        {
            var lang = new Windows.Globalization.Language("zh-Hans");
            var engine = OcrEngine.TryCreateFromLanguage(lang);
            if (engine != null)
            {
                info = $"OCR: {engine.RecognizerLanguage.DisplayName} (zh-Hans)";
                return engine;
            }
        }
        catch { }

        info = "OCR: 不可用";
        return null;
    }
}
