using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.Styling;
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
    public event EventHandler<SelectElementRequestEventArgs>? SelectElementRequested;
    public event EventHandler<ContextMenuRequestEventArgs>? ContextMenuRequested;
    public event EventHandler<CreateNewWebViewRequestEventArgs>? CreateNewWebViewRequested;
    public event EventHandler<AuthenticationRequestEventArgs>? AuthenticationRequested;
    public event EventHandler? HideEmbedderControlRequested;
    public event EventHandler<WebResourceLoadEventArgs>? WebResourceLoadRequested;
    public event EventHandler<StatusTextChangedEventArgs>? StatusTextChanged;
    public event EventHandler? TraversalCompleted;
    public event EventHandler<MoveToRequestEventArgs>? MoveToRequested;
    public event EventHandler<ResizeToRequestEventArgs>? ResizeToRequested;
    public event EventHandler<ProtocolHandlerRequestEventArgs>? ProtocolHandlerRequested;
    public event EventHandler<NotificationEventArgs>? NotificationRequested;
    public event EventHandler<BluetoothDeviceSelectionEventArgs>? BluetoothDeviceSelectionRequested;
    public event EventHandler<GamepadHapticEffectEventArgs>? GamepadHapticEffectRequested;
    public event EventHandler<HistoryChangedEventArgs>? HistoryChanged;
    public event EventHandler<FilePickerRequestEventArgs>? FilePickerRequested;
    public event EventHandler<ColorPickerRequestEventArgs>? ColorPickerRequested;
    public event EventHandler<InputMethodEventArgs>? InputMethodRequested;

    private string? _pageTitle;
    private bool _isLoading;
    private bool _canGoBack;
    private bool _canGoForward;

    private RenderingContext? _renderingContext;
    private ServoWebView? _webView;
    private ServoBitmapSurface? _surface;
    private Panel? _contentHost;
    private double _lastScaling = 1.0;
    private double _cachedScaling = 1.0;
    private TopLevel? _topLevel;
    private SelectElementOverlay? _activeSelectOverlay;
    private AuthenticationOverlay? _activeAuthOverlay;
    private ProtocolHandlerOverlay? _activeProtocolHandlerOverlay;
    private BluetoothDeviceOverlay? _activeBluetoothOverlay;
    private ContextMenu? _activeContextMenu;
    private ColorPickerOverlay? _activeColorPickerOverlay;
    private ServoTextInputMethodClient? _imeClient;
    private bool _imeComposing;
    private ScrollViewer? _scrollViewer;

    private bool HasModalOverlay =>
        _activeSelectOverlay != null ||
        _activeAuthOverlay != null ||
        _activeProtocolHandlerOverlay != null ||
        _activeBluetoothOverlay != null ||
        _activeColorPickerOverlay != null ||
        _activeContextMenu != null;

    static ServoWebViewControl()
    {
        TextInputMethodClientRequestedEvent.AddClassHandler<ServoWebViewControl>(
            (control, e) =>
            {
                control._imeClient ??= new ServoTextInputMethodClient(control);
                e.Client = control._imeClient;
            });
    }

    public ServoWebViewControl()
    {
        Focusable = true;
    }

    public static readonly StyledProperty<bool> ShowScrollBarsProperty =
        AvaloniaProperty.Register<ServoWebViewControl, bool>(nameof(ShowScrollBars), true);

    public bool ShowScrollBars
    {
        get => GetValue(ShowScrollBarsProperty);
        set => SetValue(ShowScrollBarsProperty, value);
    }

    public ServoRenderingBackend RenderingBackend { get; set; } = ServoRenderingBackend.Hardware;

    public nuint? PendingCreateNewWebViewRequest { get; set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _surface = new ServoBitmapSurface();
        _scrollViewer = new ScrollViewer
        {
            Content = _surface,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _contentHost = new Panel();
        _contentHost.Children.Add(_scrollViewer);

        ((ISetLogicalParent)_contentHost).SetParent(this);
        VisualChildren.Add(_contentHost);
        LogicalChildren.Add(_contentHost);

        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel != null)
            _topLevel.ScalingChanged += OnScalingChanged;

        ActualThemeVariantChanged += OnThemeVariantChanged;
        AddHandler(PointerTouchPadGestureMagnifyEvent, OnTouchPadMagnify);

        Dispatcher.UIThread.Post(InitializeServo, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_topLevel != null)
        {
            _topLevel.ScalingChanged -= OnScalingChanged;
            _topLevel = null;
        }

        ActualThemeVariantChanged -= OnThemeVariantChanged;
        RemoveHandler(PointerTouchPadGestureMagnifyEvent, OnTouchPadMagnify);

        Cleanup();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _contentHost?.Arrange(new Rect(finalSize));
        return finalSize;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _contentHost?.Measure(availableSize);
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
        else if (change.Property == ShowScrollBarsProperty)
        {
            if (change.GetNewValue<bool>())
                _surface?.InjectScrollTracking();
            else
                _surface?.OnNavigationStarted(); // resets scroll state
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

    public void NotifyThemeChange(ServoTheme theme) => _webView?.NotifyThemeChange(theme);

    public void NotifyMediaSessionAction(MediaSessionAction action) =>
        _webView?.NotifyMediaSessionAction(action);

    public void AdjustPinchZoom(float delta, float centerX, float centerY) =>
        _webView?.AdjustPinchZoom(delta, centerX, centerY);

    private void InitializeServo()
    {
        if (_webView != null) return; // already initialized

        var engine = Engine ?? ServoLocator.Engine;

        var scaling = GetScaling();
        _lastScaling = scaling;
        _cachedScaling = scaling;
        var (pw, ph) = GetPixelSize(scaling);

        _renderingContext = RenderingBackend == ServoRenderingBackend.Hardware
            ? new HardwareRenderingContext(pw, ph)
            : new SoftwareRenderingContext(pw, ph);

        if (PendingCreateNewWebViewRequest is { } requestHandle)
        {
            PendingCreateNewWebViewRequest = null;
            _webView = new ServoWebView(_renderingContext, requestHandle);
        }
        else
        {
            var initialUrl = Source?.AbsoluteUri;
            _webView = new ServoWebView(engine, _renderingContext, initialUrl);
        }

        _surface!.SetRenderingContext(_renderingContext);
        _surface.SetWebView(_webView);
        _webView.SetHidpiScale((float)scaling);
        _webView.NotifyThemeChange(GetServoTheme());

        _webView.NewFrameReady += OnNewFrameReady;
        _webView.LoadStatusChanged += OnWebViewLoadStatusChanged;
        _webView.UrlChanged += OnWebViewUrlChanged;
        _webView.TitleChanged += OnWebViewTitleChanged;
        _webView.CursorChanged += OnWebViewCursorChanged;
        _webView.HistoryChanged += OnWebViewHistoryChanged;
        _webView.Crashed += OnWebViewCrashed;
        _webView.WebViewConsoleMessage += OnWebViewConsoleMessage;
        _webView.NavigationRequested += OnWebViewNavigationRequested;
        _webView.AlertRequested += OnWebViewAlertRequested;
        _webView.ConfirmRequested += OnWebViewConfirmRequested;
        _webView.PromptRequested += OnWebViewPromptRequested;
        _webView.SelectElementRequested += OnWebViewSelectElementRequested;
        _webView.ContextMenuRequested += OnWebViewContextMenuRequested;
        _webView.UnloadRequested += OnWebViewUnloadRequested;
        _webView.CreateNewWebViewRequested += OnWebViewCreateNewWebViewRequested;
        _webView.AuthenticationRequested += OnWebViewAuthenticationRequested;
        _webView.HideEmbedderControlRequested += OnWebViewHideEmbedderControlRequested;
        _webView.WebResourceLoadRequested += OnWebViewWebResourceLoadRequested;
        _webView.StatusTextChanged += OnWebViewStatusTextChanged;
        _webView.TraversalCompleted += OnWebViewTraversalCompleted;
        _webView.MoveToRequested += OnWebViewMoveToRequested;
        _webView.ResizeToRequested += OnWebViewResizeToRequested;
        _webView.ProtocolHandlerRequested += OnWebViewProtocolHandlerRequested;
        _webView.NotificationRequested += OnWebViewNotificationRequested;
        _webView.BluetoothDeviceSelectionRequested += OnWebViewBluetoothDeviceSelectionRequested;
        _webView.GamepadHapticEffectRequested += OnWebViewGamepadHapticEffectRequested;
        _webView.FilePickerRequested += OnWebViewFilePickerRequested;
        _webView.ColorPickerRequested += OnWebViewColorPickerRequested;
        _webView.InputMethodRequested += OnWebViewInputMethodRequested;

        _webView.Show();
        _webView.Focus();
    }

    private void Cleanup()
    {
        if (_webView != null)
        {
            _webView.NewFrameReady -= OnNewFrameReady;
            _webView.LoadStatusChanged -= OnWebViewLoadStatusChanged;
            _webView.UrlChanged -= OnWebViewUrlChanged;
            _webView.TitleChanged -= OnWebViewTitleChanged;
            _webView.CursorChanged -= OnWebViewCursorChanged;
            _webView.HistoryChanged -= OnWebViewHistoryChanged;
            _webView.Crashed -= OnWebViewCrashed;
            _webView.WebViewConsoleMessage -= OnWebViewConsoleMessage;
            _webView.NavigationRequested -= OnWebViewNavigationRequested;
            _webView.AlertRequested -= OnWebViewAlertRequested;
            _webView.ConfirmRequested -= OnWebViewConfirmRequested;
            _webView.PromptRequested -= OnWebViewPromptRequested;
            _webView.SelectElementRequested -= OnWebViewSelectElementRequested;
            _webView.ContextMenuRequested -= OnWebViewContextMenuRequested;
            _webView.UnloadRequested -= OnWebViewUnloadRequested;
            _webView.CreateNewWebViewRequested -= OnWebViewCreateNewWebViewRequested;
            _webView.AuthenticationRequested -= OnWebViewAuthenticationRequested;
            _webView.HideEmbedderControlRequested -= OnWebViewHideEmbedderControlRequested;
            _webView.WebResourceLoadRequested -= OnWebViewWebResourceLoadRequested;
            _webView.StatusTextChanged -= OnWebViewStatusTextChanged;
            _webView.TraversalCompleted -= OnWebViewTraversalCompleted;
            _webView.MoveToRequested -= OnWebViewMoveToRequested;
            _webView.ResizeToRequested -= OnWebViewResizeToRequested;
            _webView.ProtocolHandlerRequested -= OnWebViewProtocolHandlerRequested;
            _webView.NotificationRequested -= OnWebViewNotificationRequested;
            _webView.BluetoothDeviceSelectionRequested -= OnWebViewBluetoothDeviceSelectionRequested;
            _webView.GamepadHapticEffectRequested -= OnWebViewGamepadHapticEffectRequested;
            _webView.FilePickerRequested -= OnWebViewFilePickerRequested;
            _webView.ColorPickerRequested -= OnWebViewColorPickerRequested;
            _webView.InputMethodRequested -= OnWebViewInputMethodRequested;
            _webView.Dispose();
            _webView = null;
        }
        if (_contentHost != null)
        {
            VisualChildren.Remove(_contentHost);
            LogicalChildren.Remove(_contentHost);
            _contentHost = null;
        }
        _surface?.SetWebView(null);
        _scrollViewer = null;
        _surface = null;
        // Engine is NOT disposed here — it's owned by the app, not the control.
        _renderingContext?.Dispose(); _renderingContext = null;
    }

    private void OnNewFrameReady(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_webView == null || _surface == null) return;
            _webView.Paint();
            _surface.MarkFrameReady();
        }, DispatcherPriority.Render);
    }

    private void OnScalingChanged(object? sender, EventArgs e) => ResizeServo();

    private void OnThemeVariantChanged(object? sender, EventArgs e)
    {
        _webView?.NotifyThemeChange(GetServoTheme());
    }

    private ServoTheme GetServoTheme() =>
        ActualThemeVariant == ThemeVariant.Dark ? ServoTheme.Dark : ServoTheme.Light;

    private void OnTouchPadMagnify(object? sender, PointerDeltaEventArgs e)
    {
        if (_webView == null || HasModalOverlay) return;
        var pos = e.GetPosition(this);
        var s = _cachedScaling;
        // Delta.X contains the magnification delta (e.g. 0.02 for 2% zoom in)
        var delta = 1.0f + (float)e.Delta.X;
        _webView.AdjustPinchZoom(delta, (float)(pos.X * s), (float)(pos.Y * s));
        (Engine ?? ServoLocator.Engine).SpinEventLoop();
    }

    internal void NotifyImeComposing(bool composing)
    {
        _imeComposing = composing;
    }

    private void ResizeServo()
    {
        if (_webView == null) return;
        var scaling = GetScaling();
        _cachedScaling = scaling;
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
        if (_webView == null || HasModalOverlay) return;
        Focus();
        var pos = e.GetPosition(this);
        var s = _cachedScaling;
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
        if (_webView == null || HasModalOverlay) return;
        var pos = e.GetPosition(this);
        var s = _cachedScaling;
        var button = AvaloniaKeyMapping.ToServoButton(e.InitialPressMouseButton);
        _webView.SendMouseButton(MouseButtonAction.Up, button, (float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_webView == null || HasModalOverlay) return;
        var pos = e.GetPosition(this);
        var s = _cachedScaling;
        _webView.SendMouseMove((float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _webView?.SendMouseLeftViewport();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_webView == null || HasModalOverlay) return;
        var pos = e.GetPosition(this);
        var s = _cachedScaling;
        _webView.SendWheel(e.Delta.X * 40.0, e.Delta.Y * 40.0, WheelMode.DeltaPixel,
            (float)(pos.X * s), (float)(pos.Y * s));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_webView == null || HasModalOverlay) return;

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
        if (HasModalOverlay) return;
        _webView?.SendKeyEvent(down: false, keyChar: 0,
            keyCode: AvaloniaKeyMapping.ToServoCode(e.Key),
            modifiers: AvaloniaKeyMapping.ToServoModifiers(e.KeyModifiers));
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (_webView == null || HasModalOverlay || string.IsNullOrEmpty(e.Text)) return;

        if (_imeComposing)
        {
            _imeClient?.NotifyCompositionEnd(e.Text);
            _imeComposing = false;
        }
        else
        {
            foreach (var ch in e.Text)
            {
                _webView.SendKeyEvent(down: true, keyChar: ch, keyCode: null);
                _webView.SendKeyEvent(down: false, keyChar: ch, keyCode: null);
            }
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

    private void OnWebViewLoadStatusChanged(object? sender, LoadStatusChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            IsLoading = e.Status != Sharp.LoadStatus.Complete;

            if (ShowScrollBars)
            {
                if (e.Status == Sharp.LoadStatus.Started)
                    _surface?.OnNavigationStarted();
                else if (e.Status == Sharp.LoadStatus.HeadParsed || e.Status == Sharp.LoadStatus.Complete)
                    _surface?.InjectScrollTracking();
            }

            LoadStatusChanged?.Invoke(this, e);
        });

    private void OnWebViewUrlChanged(object? sender, UrlChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Navigated?.Invoke(this, e));

    private void OnWebViewTitleChanged(object? sender, TitleChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            PageTitle = e.Title;
            TitleChanged?.Invoke(this, e);
        });

    private void OnWebViewCursorChanged(object? sender, CursorChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
            Cursor = new Cursor(AvaloniaKeyMapping.ToAvaloniaCursor(e.Cursor)));

    private void OnWebViewHistoryChanged(object? sender, HistoryChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            CanGoBack = _webView?.CanGoBack ?? false;
            CanGoForward = _webView?.CanGoForward ?? false;
            HistoryChanged?.Invoke(this, e);
        });

    private void OnWebViewCrashed(object? sender, CrashedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Crashed?.Invoke(this, e));

    private void OnWebViewConsoleMessage(object? sender, ConsoleMessageEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            // Intercept scroll bridge messages before forwarding to consumers
            if (ShowScrollBars && _surface != null &&
                _surface.TryHandleConsoleMessage(e.Message))
                return;

            ConsoleMessage?.Invoke(this, e);
        });

    private void OnWebViewNavigationRequested(object? sender, NavigationRequestEventArgs e)
    {
        if (NavigationRequested != null) NavigationRequested.Invoke(this, e);
        else e.Allow();
    }

    private void OnWebViewAlertRequested(object? sender, AlertRequestEventArgs e)
    {
        if (AlertRequested != null) AlertRequested.Invoke(this, e);
        else e.Dismiss();
    }

    private void OnWebViewConfirmRequested(object? sender, ConfirmRequestEventArgs e)
    {
        if (ConfirmRequested != null) ConfirmRequested.Invoke(this, e);
        else e.Cancel();
    }

    private void OnWebViewPromptRequested(object? sender, PromptRequestEventArgs e)
    {
        if (PromptRequested != null) PromptRequested.Invoke(this, e);
        else e.Cancel();
    }

    private void OnWebViewSelectElementRequested(object? sender, SelectElementRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_contentHost == null) { e.Dismiss(); return; }
            if (SelectElementRequested != null)
            {
                SelectElementRequested.Invoke(this, e);
                return;
            }
            var overlay = new SelectElementOverlay();
            overlay.Initialize(_contentHost, e);
            _activeSelectOverlay = overlay;
            _contentHost.Children.Add(overlay);
        });

    private void OnWebViewContextMenuRequested(object? sender, ContextMenuRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (ContextMenuRequested != null)
            {
                ContextMenuRequested.Invoke(this, e);
                return;
            }
            ShowDefaultContextMenu(e);
        });

    private void OnWebViewUnloadRequested(object? sender, UnloadRequestEventArgs e) => e.Allow();

    private void OnWebViewCreateNewWebViewRequested(object? sender, CreateNewWebViewRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            var handler = CreateNewWebViewRequested;
            if (handler != null)
            {
                handler.Invoke(this, e);
                if (!e.IsHandled)
                    e.Dismiss();
            }
            else
            {
                e.Dismiss();
            }
        });

    private void OnWebViewAuthenticationRequested(object? sender, AuthenticationRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_contentHost == null) { e.Dismiss(); return; }
            if (AuthenticationRequested != null)
            {
                AuthenticationRequested.Invoke(this, e);
                return;
            }
            var overlay = new AuthenticationOverlay();
            overlay.Initialize(_contentHost, e);
            _activeAuthOverlay = overlay;
            _contentHost.Children.Add(overlay);
        });

    private void OnWebViewHideEmbedderControlRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            DismissActiveEmbedderControls();
            HideEmbedderControlRequested?.Invoke(this, e);
        });

    private void OnWebViewWebResourceLoadRequested(object? sender, WebResourceLoadEventArgs e)
    {
        if (WebResourceLoadRequested != null)
            WebResourceLoadRequested.Invoke(this, e);
        else
            e.Allow();
    }

    private void OnWebViewStatusTextChanged(object? sender, StatusTextChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() => StatusTextChanged?.Invoke(this, e));

    private void OnWebViewTraversalCompleted(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => TraversalCompleted?.Invoke(this, e));

    private void OnWebViewMoveToRequested(object? sender, MoveToRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() => MoveToRequested?.Invoke(this, e));

    private void OnWebViewResizeToRequested(object? sender, ResizeToRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() => ResizeToRequested?.Invoke(this, e));

    private void OnWebViewProtocolHandlerRequested(object? sender, ProtocolHandlerRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_contentHost == null) { e.Deny(); return; }
            if (ProtocolHandlerRequested != null)
            {
                ProtocolHandlerRequested.Invoke(this, e);
                return;
            }
            var overlay = new ProtocolHandlerOverlay();
            overlay.Initialize(_contentHost, e);
            _activeProtocolHandlerOverlay = overlay;
            _contentHost.Children.Add(overlay);
        });

    private void OnWebViewNotificationRequested(object? sender, NotificationEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_contentHost == null) return;
            if (NotificationRequested != null)
            {
                NotificationRequested.Invoke(this, e);
                return;
            }
            var overlay = new NotificationOverlay();
            overlay.Initialize(_contentHost, e);
            _contentHost.Children.Add(overlay);
        });

    private void OnWebViewBluetoothDeviceSelectionRequested(object? sender, BluetoothDeviceSelectionEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_contentHost == null) { e.Cancel(); return; }
            if (BluetoothDeviceSelectionRequested != null)
            {
                BluetoothDeviceSelectionRequested.Invoke(this, e);
                return;
            }
            var overlay = new BluetoothDeviceOverlay();
            overlay.Initialize(_contentHost, e);
            _activeBluetoothOverlay = overlay;
            _contentHost.Children.Add(overlay);
        });

    private void OnWebViewGamepadHapticEffectRequested(object? sender, GamepadHapticEffectEventArgs e)
    {
        if (GamepadHapticEffectRequested != null)
            GamepadHapticEffectRequested.Invoke(this, e);
        else
            e.Failed();
    }

    private void OnWebViewFilePickerRequested(object? sender, FilePickerRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (FilePickerRequested != null)
            {
                FilePickerRequested.Invoke(this, e);
                return;
            }
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
                _ = FilePickerHandler.HandleRequest(topLevel, e);
            else
                e.Dismiss();
        });

    private void OnWebViewColorPickerRequested(object? sender, ColorPickerRequestEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_contentHost == null) { e.Dismiss(); return; }
            if (ColorPickerRequested != null)
            {
                ColorPickerRequested.Invoke(this, e);
                return;
            }
            var overlay = new ColorPickerOverlay();
            overlay.Initialize(_contentHost, e);
            _activeColorPickerOverlay = overlay;
            _contentHost.Children.Add(overlay);
        });

    private void OnWebViewInputMethodRequested(object? sender, InputMethodEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _imeClient?.UpdateCursorRect(
                e.PositionX,
                e.PositionY,
                e.PositionWidth,
                e.PositionHeight);
            InputMethodRequested?.Invoke(this, e);
        });

    private void DismissActiveEmbedderControls()
    {
        if (_activeSelectOverlay != null)
        {
            _activeSelectOverlay.DismissIfOpen();
            _activeSelectOverlay = null;
        }

        if (_activeAuthOverlay != null)
        {
            _activeAuthOverlay.DismissIfOpen();
            _activeAuthOverlay = null;
        }

        if (_activeProtocolHandlerOverlay != null)
        {
            _activeProtocolHandlerOverlay.DismissIfOpen();
            _activeProtocolHandlerOverlay = null;
        }

        if (_activeBluetoothOverlay != null)
        {
            _activeBluetoothOverlay.DismissIfOpen();
            _activeBluetoothOverlay = null;
        }

        if (_activeColorPickerOverlay != null)
        {
            _activeColorPickerOverlay.DismissIfOpen();
            _activeColorPickerOverlay = null;
        }

        if (_activeContextMenu != null)
        {
            _activeContextMenu.Close();
            _activeContextMenu = null;
        }
    }

    private void ShowDefaultContextMenu(ContextMenuRequestEventArgs e)
    {
        var menu = new ContextMenu();

        foreach (var item in e.Items)
        {
            var menuItem = new MenuItem
            {
                Header = item.Label,
                IsEnabled = item.Enabled,
            };
            var action = item.Action;
            menuItem.Click += (_, _) => e.Select(action);
            menu.Items.Add(menuItem);
        }

        menu.Closed += (_, _) =>
        {
            _activeContextMenu = null;
            e.Dismiss();
        };

        _activeContextMenu = menu;
        menu.PlacementTarget = this;
        menu.Open(this);
    }
}
