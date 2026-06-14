using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MC_Helper.Helpers;
using MC_Helper.Models;
using MC_Helper.Services;

namespace MC_Helper.Panels;

/// <summary>
/// 全屏透明选区层 — 仅负责框选/微调交互和调试面板显示
/// </summary>
public partial class FishingOverlay : Window
{
    private FishingSettings _settings = null!;
    private double _dpiScaleX = 1.0, _dpiScaleY = 1.0;

    // 拖拽期间的物理像素工作值（操作时直接修改，保存时转百分比）
    private int _px, _py, _pw, _ph;

    private bool _isSelecting;
    private bool _isAdjusting;
    private Rect _savedRect;

    private enum DragMode { None, Draw, Move, ResizeNW, ResizeNE, ResizeSW, ResizeSE }
    private DragMode _dragMode;
    private Point _dragStart;
    private bool _isDragging;
    private bool _showDebug;

    public bool IsSelecting => _isSelecting || _isAdjusting;
    public event Action<bool>? SelectModeChanged;
    public event Action? SettingsSaved;

    public FishingOverlay()
    {
        InitializeComponent();
    }

    public void Init(FishingSettings settings)
    {
        _settings = settings;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            _dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            _dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }
        SyncPixelsFromSettings();
        SetClickThrough(true);
        UpdateSelectionVisual();
    }

    /// <summary>
    /// 绑定 DetectionLoop，用于调试面板显示 OCR 文字和状态信息
    /// </summary>
    public void BindDetection(DetectionLoop detection)
    {
        detection.TextRecognized += OnTextRecognized;
        detection.DebugInfo += OnDebugInfo;
    }

    public void UnbindDetection(DetectionLoop detection)
    {
        detection.TextRecognized -= OnTextRecognized;
        detection.DebugInfo -= OnDebugInfo;
    }

    private void SetClickThrough(bool ct)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var es = Win32.GetWindowLong(hwnd, Win32.GWL_EXSTYLE).ToInt32();
        if (ct) { es |= Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE; }
        else { es &= ~Win32.WS_EX_TRANSPARENT; es &= ~Win32.WS_EX_NOACTIVATE; }
        Win32.SetWindowLong(hwnd, Win32.GWL_EXSTYLE, (IntPtr)es);

        // 同时更新 Grid 的命中测试
        RootGrid.IsHitTestVisible = !ct;
    }

    public void UpdateFromSettings()
    {
        SyncPixelsFromSettings();
        UpdateSelectionVisual();
    }

    /// <summary>从百分比设置同步到物理像素工作值</summary>
    private void SyncPixelsFromSettings()
    {
        var (x, y, w, h) = CaptureRegionHelper.ToPixels(_settings);
        _px = x; _py = y; _pw = w; _ph = h;
    }

    /// <summary>把当前物理像素工作值写入百分比设置</summary>
    private void SavePixelsToSettings()
    {
        CaptureRegionHelper.FromPixels(_px, _py, _pw, _ph, _settings);
    }

    private void UpdateSelectionVisual()
    {
        var sw = SystemParameters.PrimaryScreenWidth;
        var sh = SystemParameters.PrimaryScreenHeight;
        var sx = _px / _dpiScaleX;
        var sy = _py / _dpiScaleY;
        var sw2 = Math.Max(1, _pw / _dpiScaleX);
        var sh2 = Math.Max(1, _ph / _dpiScaleY);

        OverlayPath.Data = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, sw, sh)),
            new RectangleGeometry(new Rect(sx, sy, sw2, sh2)));

        SelectionBorder.Data = new RectangleGeometry(
            new Rect(sx - 1, sy - 1, sw2 + 2, sh2 + 2));

        HandleNW.Margin = new Thickness(sx - 5, sy - 5, 0, 0);
        HandleNE.Margin = new Thickness(sx + sw2 - 5, sy - 5, 0, 0);
        HandleSW.Margin = new Thickness(sx - 5, sy + sh2 - 5, 0, 0);
        HandleSE.Margin = new Thickness(sx + sw2 - 5, sy + sh2 - 5, 0, 0);

        ConfirmLabel.Margin = new Thickness(sx + sw2 + 8, sy - 4, 0, 0);

        double labelX = sx, labelY = sy - 28;
        if (labelY < 0) labelY = sy + sh2 + 6;
        SizeLabel.Margin = new Thickness(labelX, labelY, 0, 0);
        SizeLabelText.Text = $"{_pw} × {_ph}";
    }

    // ── 选区模式切换 ─────────────────────────────

    public void EnterSelectMode()
    {
        if (_isSelecting || _isAdjusting) return;

        SyncPixelsFromSettings();
        _savedRect = new Rect(_px, _py, _pw, _ph);

        _isSelecting = true;
        SelectModeChanged?.Invoke(true);
        SetClickThrough(false);
        Cursor = Cursors.Cross;

        SelectionBorder.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x9F, 0x0A));
        SelectionBorder.StrokeDashArray = null;
        SelectionBorder.StrokeThickness = 3;

        ShowHandles(false);
        SizeLabel.Visibility = Visibility.Collapsed;

        MouseLeftButtonDown += OnDrawMouseDown;
        MouseMove += OnDrawMouseMove;
        MouseLeftButtonUp += OnDrawMouseUp;
        KeyDown += OnDrawKeyDown;
        Focusable = true;
        Focus();
    }

    private void EnterAdjustMode()
    {
        _isSelecting = false;
        _isAdjusting = true;
        _dragMode = DragMode.None;
        Cursor = Cursors.Arrow;

        SelectionBorder.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
        SelectionBorder.StrokeDashArray = new DoubleCollection { 6, 3 };
        SelectionBorder.StrokeThickness = 2;

        ShowHandles(true);
        SizeLabel.Visibility = Visibility.Visible;
        UpdateSelectionVisual();

        MouseLeftButtonDown -= OnDrawMouseDown;
        MouseMove -= OnDrawMouseMove;
        MouseLeftButtonUp -= OnDrawMouseUp;

        MouseLeftButtonDown += OnAdjustMouseDown;
        MouseMove += OnAdjustMouseMove;
        MouseLeftButtonUp += OnAdjustMouseUp;
        KeyDown += OnAdjustKeyDown;
        Focusable = true;
        Focus();
    }

    public void ExitSelectMode(bool save)
    {
        if (!_isSelecting && !_isAdjusting) return;

        if (!save && _isAdjusting)
        {
            _px = (int)_savedRect.X; _py = (int)_savedRect.Y;
            _pw = (int)_savedRect.Width; _ph = (int)_savedRect.Height;
        }
        SavePixelsToSettings();

        _isSelecting = false;
        _isAdjusting = false;
        _dragMode = DragMode.None;
        _isDragging = false;
        SelectModeChanged?.Invoke(false);

        SetClickThrough(true);
        Cursor = Cursors.Arrow;

        SelectionBorder.Stroke = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0xD1, 0x58));
        SelectionBorder.StrokeDashArray = new DoubleCollection { 6, 3 };
        SelectionBorder.StrokeThickness = 2;

        ShowHandles(false);
        SizeLabel.Visibility = Visibility.Collapsed;

        MouseLeftButtonDown -= OnDrawMouseDown;
        MouseMove -= OnDrawMouseMove;
        MouseLeftButtonUp -= OnDrawMouseUp;
        MouseLeftButtonDown -= OnAdjustMouseDown;
        MouseMove -= OnAdjustMouseMove;
        MouseLeftButtonUp -= OnAdjustMouseUp;
        KeyDown -= OnDrawKeyDown;
        KeyDown -= OnAdjustKeyDown;

        if (save) SettingsSaved?.Invoke();
        UpdateSelectionVisual();
    }

    public void ToggleSelectMode()
    {
        if (_isSelecting) ExitSelectMode(true);
        else if (_isAdjusting) ExitSelectMode(true);
        else EnterSelectMode();
    }

    // ── 调试面板 ─────────────────────────────────

    public void SetDebugOverlayEnabled(bool enabled)
    {
        _showDebug = enabled;
        DebugPanel.Visibility = _showDebug ? Visibility.Visible : Visibility.Collapsed;
    }

    public void ShowOverlay() { Show(); }
    public void HideOverlay()
    {
        if (_isSelecting || _isAdjusting) ExitSelectMode(true);
        Hide();
    }

    // ── 绘制选区 ─────────────────────────────────

    private void OnDrawMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = true;
        _px = (int)(_dragStart.X * _dpiScaleX);
        _py = (int)(_dragStart.Y * _dpiScaleY);
        _pw = 1;
        _ph = 1;
        CaptureMouse();
    }

    private void OnDrawMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cur = e.GetPosition(this);
        var x1 = Math.Min(_dragStart.X, cur.X);
        var y1 = Math.Min(_dragStart.Y, cur.Y);
        var x2 = Math.Max(_dragStart.X, cur.X);
        var y2 = Math.Max(_dragStart.Y, cur.Y);
        _px = (int)(x1 * _dpiScaleX);
        _py = (int)(y1 * _dpiScaleY);
        _pw = (int)((x2 - x1) * _dpiScaleX);
        _ph = (int)((y2 - y1) * _dpiScaleY);
        UpdateSelectionVisual();
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabelText.Text = $"{_pw} × {_ph}";
    }

    private void OnDrawMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging) return;
        _isDragging = false;
        ReleaseMouseCapture();

        if (_pw <= 10 && _ph <= 10 && _savedRect.Width > 10)
        {
            _px = (int)_savedRect.X; _py = (int)_savedRect.Y;
            _pw = (int)_savedRect.Width; _ph = (int)_savedRect.Height;
        }
        else
        {
            if (_pw < 10) _pw = 10;
            if (_ph < 10) _ph = 10;
        }

        UpdateSelectionVisual();
        EnterAdjustMode();
    }

    private void OnDrawKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) ExitSelectMode(false);
    }

    // ── 微调选区 ─────────────────────────────────

    private void OnAdjustMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = true;

        var sx = _px / _dpiScaleX;
        var sy = _py / _dpiScaleY;
        var sw = _pw / _dpiScaleX;
        var sh = _ph / _dpiScaleY;
        double cx = _dragStart.X, cy = _dragStart.Y;
        const double hz = 16;

        if (Math.Abs(cx - sx) < hz && Math.Abs(cy - sy) < hz)
            _dragMode = DragMode.ResizeNW;
        else if (Math.Abs(cx - (sx + sw)) < hz && Math.Abs(cy - sy) < hz)
            _dragMode = DragMode.ResizeNE;
        else if (Math.Abs(cx - sx) < hz && Math.Abs(cy - (sy + sh)) < hz)
            _dragMode = DragMode.ResizeSW;
        else if (Math.Abs(cx - (sx + sw)) < hz && Math.Abs(cy - (sy + sh)) < hz)
            _dragMode = DragMode.ResizeSE;
        else if (cx >= sx && cx <= sx + sw && cy >= sy && cy <= sy + sh)
            _dragMode = DragMode.Move;
        else
        {
            _isDragging = false;
            double distX = 0, distY = 0;
            if (cx < sx) distX = sx - cx;
            else if (cx > sx + sw) distX = cx - (sx + sw);
            if (cy < sy) distY = sy - cy;
            else if (cy > sy + sh) distY = cy - (sy + sh);
            if (Math.Sqrt(distX * distX + distY * distY) > 60)
                ExitSelectMode(true);
        }

        if (_isDragging) CaptureMouse();
    }

    private void OnAdjustMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var cur = e.GetPosition(this);
        var dx = (int)((cur.X - _dragStart.X) * _dpiScaleX);
        var dy = (int)((cur.Y - _dragStart.Y) * _dpiScaleY);

        switch (_dragMode)
        {
            case DragMode.Move:
                _px += dx; _py += dy; break;
            case DragMode.ResizeNW:
                _px += dx; _py += dy;
                _pw -= dx; _ph -= dy; break;
            case DragMode.ResizeNE:
                _py += dy;
                _pw += dx; _ph -= dy; break;
            case DragMode.ResizeSW:
                _px += dx;
                _pw -= dx; _ph += dy; break;
            case DragMode.ResizeSE:
                _pw += dx; _ph += dy; break;
        }

        if (_pw < 10) _pw = 10;
        if (_ph < 10) _ph = 10;

        _dragStart = cur;
        UpdateSelectionVisual();
    }

    private void OnAdjustMouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _dragMode = DragMode.None;
        ReleaseMouseCapture();
    }

    private void OnAdjustKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ExitSelectMode(true);
        else if (e.Key == Key.Escape) ExitSelectMode(false);
    }

    private void Confirm_Click(object sender, MouseButtonEventArgs e) => ExitSelectMode(true);
    private void Handle_MouseDown(object sender, MouseButtonEventArgs e) { }

    // ── 辅助 ─────────────────────────────────────

    private void ShowHandles(bool show)
    {
        var v = show ? Visibility.Visible : Visibility.Collapsed;
        HandleNW.Visibility = v;
        HandleNE.Visibility = v;
        HandleSW.Visibility = v;
        HandleSE.Visibility = v;
        ConfirmLabel.Visibility = v;
    }

    private void OnTextRecognized(string text)
    {
        if (!_showDebug) return;
        Dispatcher.Invoke(() =>
            DebugText.Text = string.IsNullOrWhiteSpace(text) ? "(空)" : text);
    }

    private void OnDebugInfo(string msg)
    {
        if (!_showDebug) return;
        Dispatcher.Invoke(() =>
        {
            if (msg.Length > 50) msg = msg[..50];
            DebugState.Text = msg;
        });
    }
}
