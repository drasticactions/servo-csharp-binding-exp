// Copyright 2026 The Servo C# Bindings Contributors
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Servo.Sharp.Avalonia;

namespace Servo.Sharp.Demo;

public partial class MainWindow : Window
{
    private readonly List<TabInfo> _tabs = new();
    private int _activeTabIndex = -1;

    public MainWindow() : this("https://servo.org") { }

    public MainWindow(string? initialUrl)
    {
        InitializeComponent();

        BackButton.Click += (_, _) => ActiveWebView?.GoBack();
        ForwardButton.Click += (_, _) => ActiveWebView?.GoForward();
        ReloadButton.Click += (_, _) => ActiveWebView?.Reload();
        UrlBar.KeyDown += (_, e) => { if (e.Key == Key.Enter) NavigateToUrlBar(); };
        NewTabButton.Click += (_, _) => AddTab("https://servo.org");
        NewWindowButton.Click += (_, _) => OpenNewWindow();

        AddTab(initialUrl ?? "https://servo.org");
    }

    private ServoWebViewControl? ActiveWebView => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count
        ? _tabs[_activeTabIndex].WebView
        : null;

    public void AddTab(string url)
    {
        var webView = new ServoWebViewControl
        {
            Source = new Uri(url),
            IsVisible = false,
        };

        var tab = new TabInfo
        {
            Title = "New Tab",
            WebView = webView,
        };

        WireWebViewEvents(tab);
        _tabs.Add(tab);
        WebViewContainer.Children.Add(webView);
        SwitchToTab(_tabs.Count - 1);
    }

    private void SwitchToTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        // Hide current
        if (_activeTabIndex >= 0 && _activeTabIndex < _tabs.Count)
            _tabs[_activeTabIndex].WebView.IsVisible = false;

        _activeTabIndex = index;
        var tab = _tabs[index];
        tab.WebView.IsVisible = true;
        tab.WebView.Focus();

        UpdateUrlBar(tab);
        UpdateNavigationButtons(tab);
        UpdateWindowTitle(tab);
        RebuildTabStrip();
    }

    private void CloseTab(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;

        if (_tabs.Count == 1)
        {
            Close();
            return;
        }

        var tab = _tabs[index];
        _tabs.RemoveAt(index);
        WebViewContainer.Children.Remove(tab.WebView);

        if (_activeTabIndex == index)
        {
            // Switch to nearest tab
            var newIndex = Math.Min(index, _tabs.Count - 1);
            _activeTabIndex = -1; // force re-switch
            SwitchToTab(newIndex);
        }
        else if (_activeTabIndex > index)
        {
            _activeTabIndex--;
            RebuildTabStrip();
        }
        else
        {
            RebuildTabStrip();
        }
    }

    private void OpenNewWindow()
    {
        var window = new MainWindow("https://servo.org");
        window.Show();
    }

    private void WireWebViewEvents(TabInfo tab)
    {
        var wv = tab.WebView;

        wv.Navigated += (_, e) =>
        {
            tab.Url = e.Url;
            if (IsActiveTab(tab))
                UrlBar.Text = e.Url;
        };

        wv.TitleChanged += (_, e) =>
        {
            tab.Title = e.Title ?? "New Tab";
            if (IsActiveTab(tab))
                UpdateWindowTitle(tab);
            RebuildTabStrip();
        };

        wv.LoadStatusChanged += (_, e) =>
        {
            if (IsActiveTab(tab))
            {
                StatusText.Text = e.Status switch
                {
                    LoadStatus.Started => "Loading...",
                    LoadStatus.HeadParsed => "Parsing...",
                    LoadStatus.Complete => "Complete",
                    _ => "Unknown",
                };
            }
        };

        wv.PropertyChanged += (_, e) =>
        {
            if (!IsActiveTab(tab)) return;
            if (e.Property == ServoWebViewControl.CanGoBackProperty)
                BackButton.IsEnabled = wv.CanGoBack;
            else if (e.Property == ServoWebViewControl.CanGoForwardProperty)
                ForwardButton.IsEnabled = wv.CanGoForward;
        };

        wv.ConsoleMessage += (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = $"[{e.Level}] {e.Message}";
        };

        wv.Crashed += (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = $"CRASHED: {e.Reason}";
        };
    }

    private bool IsActiveTab(TabInfo tab) =>
        _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count && _tabs[_activeTabIndex] == tab;

    private void NavigateToUrlBar()
    {
        var url = UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("://")) url = "https://" + url;
        ActiveWebView?.Navigate(url);
        ActiveWebView?.Focus();
    }

    private void UpdateUrlBar(TabInfo tab)
    {
        UrlBar.Text = tab.Url ?? tab.WebView.Source?.AbsoluteUri ?? "";
    }

    private void UpdateNavigationButtons(TabInfo tab)
    {
        BackButton.IsEnabled = tab.WebView.CanGoBack;
        ForwardButton.IsEnabled = tab.WebView.CanGoForward;
    }

    private void UpdateWindowTitle(TabInfo tab)
    {
        Title = !string.IsNullOrEmpty(tab.Title) && tab.Title != "New Tab"
            ? $"{tab.Title}"
            : "Servo C# Demo";
    }

    private void RebuildTabStrip()
    {
        TabStrip.Children.Clear();

        for (int i = 0; i < _tabs.Count; i++)
        {
            var index = i;
            var tab = _tabs[i];
            var isActive = i == _activeTabIndex;

            var titleText = new TextBlock
            {
                Text = TruncateTitle(tab.Title, 20),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 12,
            };

            var closeBtn = new Button
            {
                Content = "✕",
                Padding = new Thickness(2, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0),
                Width = 18,
                Height = 18,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            closeBtn.Click += (_, _) => CloseTab(index);

            var tabContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            tabContent.Children.Add(titleText);
            tabContent.Children.Add(closeBtn);

            var tabButton = new Button
            {
                Content = tabContent,
                Padding = new Thickness(8, 4),
                Background = isActive
                    ? Brushes.White
                    : new SolidColorBrush(Color.Parse("#E8E8E8")),
                BorderThickness = new Thickness(1, 1, 1, isActive ? 0 : 1),
                BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin = new Thickness(1, 0),
                MinWidth = 60,
            };
            tabButton.Click += (_, _) => SwitchToTab(index);

            TabStrip.Children.Add(tabButton);
        }
    }

    private static string TruncateTitle(string? title, int maxLength)
    {
        if (string.IsNullOrEmpty(title)) return "New Tab";
        return title.Length <= maxLength ? title : title[..(maxLength - 1)] + "…";
    }

    private sealed class TabInfo
    {
        public required ServoWebViewControl WebView { get; init; }
        public string Title { get; set; } = "New Tab";
        public string? Url { get; set; }
    }
}
