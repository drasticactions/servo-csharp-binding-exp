using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Servo.Sharp;
using Servo.Sharp.Avalonia;

namespace Servo.Sharp.Demo.Core;

public partial class MobileMainPage : UserControl
{
    private ServoWebViewControl? _webView;
    private bool _isLoading;

    public MobileMainPage()
    {
        InitializeComponent();

        BackButton.Click += OnBackClick;
        ForwardButton.Click += OnForwardClick;
        ReloadButton.Click += OnReloadClick;
        HomeButton.Click += OnHomeClick;
        StopButton.Click += OnStopClick;
        GoButton.Click += OnGoClick;
        UrlBar.KeyDown += OnUrlBarKeyDown;

        CreateWebView();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        BackButton.Click -= OnBackClick;
        ForwardButton.Click -= OnForwardClick;
        ReloadButton.Click -= OnReloadClick;
        HomeButton.Click -= OnHomeClick;
        StopButton.Click -= OnStopClick;
        GoButton.Click -= OnGoClick;
        UrlBar.KeyDown -= OnUrlBarKeyDown;

        if (_webView != null)
        {
            _webView.Navigated -= OnNavigated;
            _webView.LoadStatusChanged -= OnLoadStatusChanged;
            _webView.PropertyChanged -= OnWebViewPropertyChanged;
            _webView.TitleChanged -= OnTitleChanged;
            _webView.CreateNewWebViewRequested -= OnCreateNewWebViewRequested;
            WebViewContainer.Children.Remove(_webView);
            _webView = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void CreateWebView()
    {
        _webView = new ServoWebViewControl
        {
            Source = new Uri(ServoAppSetup.NewTabUrl),
        };

        _webView.Navigated += OnNavigated;
        _webView.LoadStatusChanged += OnLoadStatusChanged;
        _webView.PropertyChanged += OnWebViewPropertyChanged;
        _webView.TitleChanged += OnTitleChanged;
        _webView.CreateNewWebViewRequested += OnCreateNewWebViewRequested;

        WebViewContainer.Children.Add(_webView);
    }

    private void OnBackClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        _webView?.GoBack();

    private void OnForwardClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        _webView?.GoForward();

    private void OnReloadClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        _webView?.Reload();

    private void OnHomeClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        _webView?.Navigate(ServoAppSetup.NewTabUrl);

    private void OnStopClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        _webView?.Reload(); // TODO: stop navigation when API available

    private void OnGoClick(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) =>
        NavigateToUrlBar();

    private void OnUrlBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) NavigateToUrlBar();
    }

    private void OnNavigated(object? sender, UrlChangedEventArgs e)
    {
        UrlBar.Text = e.Url;
    }

    private void OnLoadStatusChanged(object? sender, LoadStatusChangedEventArgs e)
    {
        _isLoading = e.Status is LoadStatus.Started or LoadStatus.HeadParsed;
        LoadingBar.IsVisible = _isLoading;
        ReloadButton.IsVisible = !_isLoading;
        StopButton.IsVisible = _isLoading;
    }

    private void OnWebViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ServoWebViewControl.CanGoBackProperty)
            BackButton.IsEnabled = _webView!.CanGoBack;
        else if (e.Property == ServoWebViewControl.CanGoForwardProperty)
            ForwardButton.IsEnabled = _webView!.CanGoForward;
    }

    private void OnTitleChanged(object? sender, TitleChangedEventArgs e)
    {
        // Could update a title display if added later
    }

    private void OnCreateNewWebViewRequested(object? sender, CreateNewWebViewRequestEventArgs e)
    {
        // On mobile, navigate in the same view instead of opening a new tab
        e.MarkHandled();
    }

    private void NavigateToUrlBar()
    {
        var url = UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url)) return;
        url = ServoAppSetup.NormalizeUrl(url);
        _webView?.Navigate(url);
        _webView?.Focus();
    }
}
