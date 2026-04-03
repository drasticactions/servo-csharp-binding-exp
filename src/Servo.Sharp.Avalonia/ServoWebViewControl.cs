using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Servo.Sharp.Avalonia;

public class ServoWebViewControl : Control
{
    public static readonly StyledProperty<ServoEngine?> EngineProperty =
        AvaloniaProperty.Register<ServoWebViewControl, ServoEngine?>(nameof(Engine));

    public static readonly StyledProperty<Uri?> SourceProperty =
        AvaloniaProperty.Register<ServoWebViewControl, Uri?>(nameof(Source));

    public static readonly DirectProperty<ServoWebViewControl, string?> PageTitleProperty =
        AvaloniaProperty.RegisterDirect<ServoWebViewControl, string?>(nameof(PageTitle), o => o.PageTitle);

    public static readonly DirectProperty<ServoWebViewControl, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<ServoWebViewControl, bool>(nameof(IsLoading), o => o.IsLoading);

    public static readonly DirectProperty<ServoWebViewControl, bool> CanGoBackProperty =
        AvaloniaProperty.RegisterDirect<ServoWebViewControl, bool>(nameof(CanGoBack), o => o.CanGoBack);

    public static readonly DirectProperty<ServoWebViewControl, bool> CanGoForwardProperty =
        AvaloniaProperty.RegisterDirect<ServoWebViewControl, bool>(nameof(CanGoForward), o => o.CanGoForward);

    public static readonly StyledProperty<float> ZoomLevelProperty =
        AvaloniaProperty.Register<ServoWebViewControl, float>(nameof(ZoomLevel), 1.0f);

    public ServoEngine? Engine
    {
        get => GetValue(EngineProperty);
        set => SetValue(EngineProperty, value);
    }

    public Uri? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? PageTitle
    {
        get => _pageTitle;
        private set => SetAndRaise(PageTitleProperty, ref _pageTitle, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        private set => SetAndRaise(CanGoBackProperty, ref _canGoBack, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        private set => SetAndRaise(CanGoForwardProperty, ref _canGoForward, value);
    }

    public float ZoomLevel
    {
        get => GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public event EventHandler<UrlChangedEventArgs>? Navigated;
    public event EventHandler<TitleChangedEventArgs>? TitleChanged;
    public event EventHandler<LoadStatusChangedEventArgs>? LoadStatusChanged;
    public event EventHandler<NavigationRequestEventArgs>? NavigationRequested;
    public event EventHandler<ConsoleMessageEventArgs>? ConsoleMessage;
    public event EventHandler<CrashedEventArgs>? Crashed;
    public event EventHandler<AlertRequestEventArgs>? AlertRequested;
    public event EventHandler<ConfirmRequestEventArgs>? ConfirmRequested;
    public event EventHandler<PromptRequestEventArgs>? PromptRequested;

    private string? _pageTitle;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;

    private HardwareRenderingContext? _hwRenderingContext;
    private SoftwareRenderingContext? _swRenderingContext;
    private ServoWebView? _webView;
    private ServoBitmapSurface? _surface;
    private double _lastScaling = 1.0;

    public ServoWebViewControl()
    {
        Focusable = true;
    }

    public ServoRenderingBackend RenderingBackend { get; set; } = ServoRenderingBackend.Hardware;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _surface = new ServoBitmapSurface();
        ((ISetLogicalParent)_surface).SetParent(this);
        VisualChildren.Add(_surface);
        LogicalChildren.Add(_surface);

        Dispatcher.UIThread.Post(InitializeServo, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Cleanup();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _surface?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _surface?.Measure(availableSize);
        return availableSize;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty && _webView != null)
        {
            var uri = change.GetNewValue<Uri?>();
            if (uri != null) _webView.Load(uri.AbsoluteUri);
        }
        else if (change.Property == ZoomLevelProperty && _webView != null)
        {
            _webView.PageZoom = change.GetNewValue<float>();
        }
        else if (change.Property == BoundsProperty)
        {
            ResizeServo();
        }
    }

    public void Navigate(string url) => _webView?.Load(url);
    public void Navigate(Uri uri) => Navigate(uri.AbsoluteUri);
    public void Reload() => _webView?.Reload();
    public void GoBack(int steps = 1) => _webView?.GoBack(steps);
    public void GoForward(int steps = 1) => _webView?.GoForward(steps);
    public Task<string> EvaluateJavaScriptAsync(string script) =>
        _webView?.EvaluateJavaScriptAsync(script) ?? Task.FromResult("undefined");

    public ServoWebView? WebView => _webView;

    private void InitializeServo()
    {
        if (_webView != null) return; // already initialized

        var engine = Engine ?? ServoLocator.Engine;

        var scaling = GetScaling();
        _lastScaling = scaling;
        var (pw, ph) = GetPixelSize(scaling);

        var initialUrl = Source?.AbsoluteUri;
        if (RenderingBackend == ServoRenderingBackend.Hardware)
        {
            _hwRenderingContext = new HardwareRenderingContext(pw, ph);
            _webView = new ServoWebView(engine, _hwRenderingContext, initialUrl);
        }
        else
        {
            _swRenderingContext = new SoftwareRenderingContext(pw, ph);
            _webView = new ServoWebView(engine, _swRenderingContext, initialUrl);
        }
        _surface!.SetRenderingContext(_hwRenderingContext, _swRenderingContext);
        _webView.SetHidpiScale((float)scaling);

        _webView.NewFrameReady += OnNewFrameReady;
        _webView.LoadStatusChanged += (_, e) => Dispatcher.UIThread.Post(() =>
        {
            IsLoading = e.Status != Sharp.LoadStatus.Complete;
            LoadStatusChanged?.Invoke(this, e);
        });
        _webView.UrlChanged += (_, e) => Dispatcher.UIThread.Post(() =>
            Navigated?.Invoke(this, e));
        _webView.TitleChanged += (_, e) => Dispatcher.UIThread.Post(() =>
        {
            PageTitle = e.Title;
            TitleChanged?.Invoke(this, e);
        });
        _webView.CursorChanged += (_, e) => Dispatcher.UIThread.Post(() =>
            Cursor = new Cursor(AvaloniaKeyMapping.ToAvaloniaCursor(e.Cursor)));
        _webView.HistoryChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            CanGoBack = _webView?.CanGoBack ?? false;
            CanGoForward = _webView?.CanGoForward ?? false;
        });
        _webView.Crashed += (_, e) => Dispatcher.UIThread.Post(() => Crashed?.Invoke(this, e));
        _webView.WebViewConsoleMessage += (_, e) => Dispatcher.UIThread.Post(() => ConsoleMessage?.Invoke(this, e));
        _webView.NavigationRequested += (_, e) =>
        {
            if (NavigationRequested != null) NavigationRequested.Invoke(this, e);
            else e.Allow();
        };
        _webView.AlertRequested += (_, e) =>
        {
            if (AlertRequested != null) AlertRequested.Invoke(this, e);
            else e.Dismiss();
        };
        _webView.ConfirmRequested += (_, e) =>
        {
            if (ConfirmRequested != null) ConfirmRequested.Invoke(this, e);
            else e.Cancel();
        };
        _webView.PromptRequested += (_, e) =>
        {
            if (PromptRequested != null) PromptRequested.Invoke(this, e);
            else e.Cancel();
        };
        _webView.UnloadRequested += (_, e) => e.Allow();

        _webView.Show();
        _webView.Focus();
    }

    private void Cleanup()
    {
        if (_surface != null)
        {
            VisualChildren.Remove(_surface);
            LogicalChildren.Remove(_surface);
            _surface = null;
        }
        _webView?.Dispose(); _webView = null;
        // Engine is NOT disposed here — it's owned by the app, not the control.
        _hwRenderingContext?.Dispose(); _hwRenderingContext = null;
        _swRenderingContext?.Dispose(); _swRenderingContext = null;
    }

    private void OnNewFrameReady(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_webView == null || _surface == null) return;
            _webView.Paint();
            _hwRenderingContext?.Present();
            _swRenderingContext?.Present();
            _surface.MarkFrameReady();
        }, DispatcherPriority.Render);
    }

    private void ResizeServo()
    {
        if (_webView == null) return;
        var scaling = GetScaling();
        var (pw, ph) = GetPixelSize(scaling);
        if (pw == 0 || ph == 0) return;

        _webView.Resize(pw, ph);
        if (Math.Abs(scaling - _lastScaling) > 0.01)
        {
            _lastScaling = scaling;
            _webView.SetHidpiScale((float)scaling);
        }
    }

    private double GetScaling() =>
        (this.GetPresentationSource()?.RenderScaling) ?? 1.0;

    private (uint w, uint h) GetPixelSize(double scaling)
    {
        var w = (uint)Math.Max(1, (int)(Bounds.Width * scaling));
        var h = (uint)Math.Max(1, (int)(Bounds.Height * scaling));
        return (w, h);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_webView == null) return;
        Focus();
        var pos = e.GetPosition(this);
        var s = GetScaling();
        var props = e.GetCurrentPoint(this).Properties;
        var button = ServoMouseButton.Left;
        if (props.IsMiddleButtonPressed) button = ServoMouseButton.Middle;
        else if (props.IsRightButtonPressed) button = ServoMouseButton.Right;
        else if (props.IsXButton1Pressed) button = ServoMouseButton.Back;
        else if (props.IsXButton2Pressed) button = ServoMouseButton.Forward;
        _webView.SendMouseButton(MouseButtonAction.Down, button, (float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_webView == null) return;
        var pos = e.GetPosition(this);
        var s = GetScaling();
        var button = AvaloniaKeyMapping.ToServoButton(e.InitialPressMouseButton);
        _webView.SendMouseButton(MouseButtonAction.Up, button, (float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_webView == null) return;
        var pos = e.GetPosition(this);
        var s = GetScaling();
        _webView.SendMouseMove((float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_webView == null) return;
        var pos = e.GetPosition(this);
        var s = GetScaling();
        _webView.SendWheel(e.Delta.X * 40.0, e.Delta.Y * 40.0, WheelMode.DeltaPixel,
            (float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_webView == null) return;

        if (e.KeyModifiers.HasFlag(global::Avalonia.Input.KeyModifiers.Control))
        {
            var action = e.Key switch
            {
                Key.C => (EditingAction?)EditingAction.Copy,
                Key.X => (EditingAction?)EditingAction.Cut,
                Key.V => (EditingAction?)EditingAction.Paste,
                _ => null,
            };
            if (action.HasValue)
            {
                _webView.SendEditingAction(action.Value);
                e.Handled = true;
                return;
            }
        }

        _webView.SendKeyEvent(down: true, keyChar: 0,
            keyCode: AvaloniaKeyMapping.ToServoCode(e.Key),
            modifiers: AvaloniaKeyMapping.ToServoModifiers(e.KeyModifiers));
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _webView?.SendKeyEvent(down: false, keyChar: 0,
            keyCode: AvaloniaKeyMapping.ToServoCode(e.Key),
            modifiers: AvaloniaKeyMapping.ToServoModifiers(e.KeyModifiers));
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_webView == null || string.IsNullOrEmpty(e.Text)) return;
        foreach (var ch in e.Text)
        {
            _webView.SendKeyEvent(down: true, keyChar: ch, keyCode: null);
            _webView.SendKeyEvent(down: false, keyChar: ch, keyCode: null);
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _webView?.Focus();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _webView?.Blur();
    }
}
