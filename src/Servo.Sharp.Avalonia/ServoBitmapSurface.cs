using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Servo.Sharp.Avalonia;

internal class ServoBitmapSurface : Control, ILogicalScrollable
{
    private const string ScrollMessagePrefix = "__servo_scroll:";

    private WriteableBitmap? _bitmap;
    private RenderingContext? _renderingContext;
    private ServoWebView? _webView;
    private bool _scriptInjected;

    // Scroll state from JS bridge
    private Size _extent;
    private Size _viewport;
    private Vector _offset;
    private bool _updatingOffset;

    public void SetRenderingContext(RenderingContext context)
    {
        _renderingContext = context;
    }

    public void SetWebView(ServoWebView? webView)
    {
        _webView = webView;
        _scriptInjected = false;
    }

    public void MarkFrameReady()
    {
        InvalidateVisual();
    }

    public override unsafe void Render(DrawingContext context)
    {
        var pixels = _renderingContext?.ReadPixels();
        _renderingContext?.Present();
        if (pixels == null || pixels.Data.Length == 0)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            return;
        }

        // During DPI transitions, Servo may produce all-zero (blank) frames while
        // reconfiguring its rendering pipeline. Keep showing the previous valid bitmap
        // to avoid flashing blank content.
        if (pixels.Data.AsSpan().IndexOfAnyExcept((byte)0) == -1 && _bitmap != null)
        {
            context.DrawImage(_bitmap, new Rect(Bounds.Size));
            return;
        }

        var neededSize = new PixelSize((int)pixels.Width, (int)pixels.Height);
        if (_bitmap == null || _bitmap.PixelSize != neededSize)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                neededSize,
                new Vector(96, 96),
                PixelFormats.Rgba8888,
                AlphaFormat.Premul);
        }

        using (var fb = _bitmap.Lock())
        {
            Unsafe.CopyBlock(fb.Address.ToPointer(), Unsafe.AsPointer(ref pixels.Data[0]),
                (uint)pixels.Data.Length);
        }

        context.DrawImage(_bitmap, new Rect(Bounds.Size));
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bitmap?.Dispose();
        _bitmap = null;
        base.OnDetachedFromVisualTree(e);
    }

    private static string ScrollTrackingScript => """
        (function() {
            if (window.__servoScrollTrackerInstalled) return;
            window.__servoScrollTrackerInstalled = true;

            function report() {
                var de = document.documentElement;
                var b = document.body || de;
                var sw = Math.max(de.scrollWidth, b.scrollWidth);
                var sh = Math.max(de.scrollHeight, b.scrollHeight);
                console.log('__servo_scroll:' + JSON.stringify({
                    x: Math.round(window.scrollX),
                    y: Math.round(window.scrollY),
                    sw: sw,
                    sh: sh,
                    cw: de.clientWidth,
                    ch: de.clientHeight
                }));
            }

            window.addEventListener('scroll', report, { passive: true });
            window.addEventListener('resize', report, { passive: true });

            var observer = new MutationObserver(function() {
                clearTimeout(window.__servoScrollTimer);
                window.__servoScrollTimer = setTimeout(report, 100);
            });
            observer.observe(document.documentElement, {
                childList: true, subtree: true, attributes: true
            });

            report();
        })();
        """;

    public void InjectScrollTracking()
    {
        if (_webView == null || _scriptInjected) return;
        _scriptInjected = true;
        _ = _webView.EvaluateJavaScriptAsync(ScrollTrackingScript);
    }

    public void OnNavigationStarted()
    {
        _scriptInjected = false;
        _extent = default;
        _viewport = default;
        _offset = default;
        RaiseScrollInvalidated(EventArgs.Empty);
    }

    public bool TryHandleConsoleMessage(string message)
    {
        if (!message.StartsWith(ScrollMessagePrefix, StringComparison.Ordinal))
            return false;

        var json = message.AsSpan(ScrollMessagePrefix.Length);
        try
        {
            var doc = JsonDocument.Parse(json.ToString());
            var root = doc.RootElement;

            var scrollX = root.GetProperty("x").GetDouble();
            var scrollY = root.GetProperty("y").GetDouble();
            var scrollWidth = root.GetProperty("sw").GetDouble();
            var scrollHeight = root.GetProperty("sh").GetDouble();
            var clientWidth = root.GetProperty("cw").GetDouble();
            var clientHeight = root.GetProperty("ch").GetDouble();

            _extent = new Size(scrollWidth, scrollHeight);
            _viewport = new Size(clientWidth, clientHeight);

            _updatingOffset = true;
            _offset = new Vector(scrollX, scrollY);
            _updatingOffset = false;

            RaiseScrollInvalidated(EventArgs.Empty);
        }
        catch
        {
            // Malformed scroll message, ignore
        }

        return true;
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size IScrollable.Extent => _extent;

    Size IScrollable.Viewport => _viewport;

    Vector IScrollable.Offset
    {
        get => _offset;
        set
        {
            if (_updatingOffset || _webView == null || value == _offset) return;

            _offset = value;
            var x = Math.Round(value.X);
            var y = Math.Round(value.Y);
            _ = _webView.EvaluateJavaScriptAsync($"window.scrollTo({x}, {y})");
        }
    }

    Size ILogicalScrollable.ScrollSize => new(16, 16);

    Size ILogicalScrollable.PageScrollSize => _viewport;

    bool IScrollable.CanHorizontallyScroll => _extent.Width > _viewport.Width + 1;

    bool IScrollable.CanVerticallyScroll => _extent.Height > _viewport.Height + 1;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _extent.Width > _viewport.Width + 1;
        set { } // controlled by content, not by ScrollViewer
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _extent.Height > _viewport.Height + 1;
        set { } // controlled by content, not by ScrollViewer
    }

    public event EventHandler? ScrollInvalidated;

    public void RaiseScrollInvalidated(EventArgs e)
    {
        ScrollInvalidated?.Invoke(this, e);
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) => null;
}
