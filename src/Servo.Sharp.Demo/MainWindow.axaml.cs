// Copyright 2026 The Servo C# Bindings Contributors
// SPDX-License-Identifier: MPL-2.0

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Servo.Sharp;
using Servo.Sharp.Avalonia;

namespace Servo.Sharp.Demo;

public partial class MainWindow : Window
{
    private ServoRenderingBackend _currentBackend = ServoRenderingBackend.Hardware;

    public MainWindow()
    {
        InitializeComponent();

        WebView.Source = new Uri("https://servo.org");
        WireWebViewEvents(WebView);
        UpdateBackendButton();

        BackButton.Click += (_, _) => WebView.GoBack();
        ForwardButton.Click += (_, _) => WebView.GoForward();
        ReloadButton.Click += (_, _) => WebView.Reload();
        GoButton.Click += (_, _) => NavigateToUrlBar();
        UrlBar.KeyDown += (_, e) => { if (e.Key == Key.Enter) NavigateToUrlBar(); };
        ZoomInButton.Click += (_, _) => WebView.ZoomLevel += 0.1f;
        ZoomOutButton.Click += (_, _) => WebView.ZoomLevel = Math.Max(0.1f, WebView.ZoomLevel - 0.1f);
        ScreenshotButton.Click += OnScreenshotClicked;
        JsRunButton.Click += OnJsRunClicked;
        JsInput.KeyDown += (_, e) => { if (e.Key == Key.Enter) OnJsRunClicked(null, e); };
        BackendButton.Click += OnBackendToggle;
    }

    private void WireWebViewEvents(ServoWebViewControl wv)
    {
        wv.Navigated += (_, e) => UrlBar.Text = e.Url;
        wv.TitleChanged += (_, e) =>
            Title = e.Title != null ? $"{e.Title} - Servo C# Demo" : "Servo C# Demo";
        wv.LoadStatusChanged += (_, e) =>
            StatusText.Text = e.Status switch
            {
                LoadStatus.Started => "Loading...",
                LoadStatus.HeadParsed => "Parsing...",
                LoadStatus.Complete => "Complete",
                _ => "Unknown",
            };
        wv.ConsoleMessage += (_, e) => StatusText.Text = $"[{e.Level}] {e.Message}";
        wv.Crashed += (_, e) => StatusText.Text = $"CRASHED: {e.Reason}";
        wv.PropertyChanged += (_, e) =>
        {
            if (e.Property == ServoWebViewControl.CanGoBackProperty)
                BackButton.IsEnabled = WebView.CanGoBack;
            else if (e.Property == ServoWebViewControl.CanGoForwardProperty)
                ForwardButton.IsEnabled = WebView.CanGoForward;
        };
    }

    private void OnBackendToggle(object? sender, EventArgs e)
    {
        var currentUrl = WebView.WebView?.Url;
        _currentBackend = _currentBackend == ServoRenderingBackend.Hardware
            ? ServoRenderingBackend.Software
            : ServoRenderingBackend.Hardware;

        WebViewContainer.Children.Clear();

        var newWebView = new ServoWebViewControl
        {
            RenderingBackend = _currentBackend,
            Source = currentUrl != null ? new Uri(currentUrl) : new Uri("https://servo.org"),
        };
        WireWebViewEvents(newWebView);
        WebView = newWebView;
        WebViewContainer.Children.Add(newWebView);

        UpdateBackendButton();
        StatusText.Text = $"Switched to {_currentBackend} rendering";
    }

    private void UpdateBackendButton()
    {
        BackendButton.Content = _currentBackend == ServoRenderingBackend.Hardware ? "HW" : "SW";
        ToolTip.SetTip(BackendButton,
            _currentBackend == ServoRenderingBackend.Hardware
                ? "Currently: Hardware (GPU). Click to switch to Software."
                : "Currently: Software (CPU). Click to switch to Hardware.");
    }

    private void NavigateToUrlBar()
    {
        var url = UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("://")) url = "https://" + url;
        WebView.Navigate(url);
        StatusText.Text = $"Loading {url}...";
        WebView.Focus();
    }

    private async void OnJsRunClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(JsInput.Text)) return;
        try
        {
            var result = await WebView.EvaluateJavaScriptAsync(JsInput.Text);
            StatusText.Text = $"JS result: {result}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"JS error: {ex.Message}";
        }
    }

    private async void OnScreenshotClicked(object? sender, EventArgs e)
    {
        var webView = WebView.WebView;
        if (webView == null) return;
        StatusText.Text = "Taking screenshot...";
        try
        {
            var pixels = await webView.TakeScreenshotAsync();
            if (pixels == null) { StatusText.Text = "Screenshot failed"; return; }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Screenshot",
                DefaultExtension = "png",
                FileTypeChoices = [new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
                SuggestedFileName = "servo-screenshot.png",
            });
            if (file == null) { StatusText.Text = "Screenshot cancelled"; return; }

            var bmp = new WriteableBitmap(
                new PixelSize((int)pixels.Width, (int)pixels.Height),
                new Vector(96, 96),
                global::Avalonia.Platform.PixelFormat.Rgba8888,
                global::Avalonia.Platform.AlphaFormat.Premul);
            using (var fb = bmp.Lock())
                System.Runtime.InteropServices.Marshal.Copy(pixels.Data, 0, fb.Address, pixels.Data.Length);

            await using var stream = await file.OpenWriteAsync();
            bmp.Save(stream);
            StatusText.Text = $"Screenshot saved to {file.Name}";
        }
        catch (Exception ex) { StatusText.Text = $"Screenshot error: {ex.Message}"; }
    }
}
