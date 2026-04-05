// Copyright 2026 The Servo C# Bindings Contributors
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Servo.Sharp.Avalonia;

namespace Servo.Sharp.Demo;

public partial class MainWindow : Window
{
    private readonly List<TabInfo> _tabs = new();
    private int _activeTabIndex = -1;
    private bool _isDraggingTab;
    private bool _experimentalFeaturesEnabled;

    private const string NewTabUrl = "servo:newtab";

    public MainWindow() : this(NewTabUrl) { }

    public MainWindow(string? initialUrl)
    {
        InitializeComponent();

        BackButton.Click += (_, _) => ActiveWebView?.GoBack();
        ForwardButton.Click += (_, _) => ActiveWebView?.GoForward();
        ReloadButton.Click += (_, _) => ActiveWebView?.Reload();
        UrlBar.KeyDown += (_, e) => { if (e.Key == Key.Enter) NavigateToUrlBar(); };
        NewTabButton.Click += (_, _) => AddTab(NewTabUrl);
        NewWindowButton.Click += (_, _) => OpenNewWindow();
        ExperimentalButton.Click += (_, _) => ToggleExperimentalFeatures();

        TabStrip.AddHandler(DragTabItem.TabClickedEvent, OnTabClicked);
        TabStrip.AddHandler(DragTabItem.DragStartedEvent, (_, _) => _isDraggingTab = true, handledEventsToo: true);
        TabStrip.AddHandler(DragTabItem.DragCompletedEvent, (_, _) => _isDraggingTab = false, handledEventsToo: true);
        TabStrip.TabReordered += OnTabReordered;

        AddTab(initialUrl ?? NewTabUrl);
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
            var newIndex = Math.Min(index, _tabs.Count - 1);
            _activeTabIndex = -1;
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
        var window = new MainWindow(NewTabUrl);
        window.Show();
    }

    private void ToggleExperimentalFeatures()
    {
        _experimentalFeaturesEnabled = !_experimentalFeaturesEnabled;
        var value = _experimentalFeaturesEnabled ? "true" : "false";

        foreach (var pref in ServoProtocolHandler.ExperimentalPrefs)
            ServoLocator.Engine.SetPreference(pref, value);

        foreach (var tab in _tabs)
            tab.WebView.Reload();

        ExperimentalButton.Background = _experimentalFeaturesEnabled
            ? Brushes.DodgerBlue
            : null;

        StatusText.Text = _experimentalFeaturesEnabled
            ? "Experimental web platform features enabled"
            : "Experimental web platform features disabled";
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

        wv.StatusTextChanged += (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = e.StatusText ?? "Ready";
        };

        wv.MoveToRequested += (_, e) => Position = new PixelPoint(e.X, e.Y);

        wv.ResizeToRequested += (_, e) =>
        {
            Width = e.Width;
            Height = e.Height;
        };

        wv.TraversalCompleted += (_, _) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = "Navigation traversal complete";
        };

        wv.Crashed += (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = $"CRASHED: {e.Reason}";
        };

        wv.CreateNewWebViewRequested += (_, e) =>
        {
            var newWebView = new ServoWebViewControl
            {
                PendingCreateNewWebViewRequest = e.RequestHandle,
                IsVisible = false,
            };

            var newTab = new TabInfo
            {
                Title = "New Tab",
                WebView = newWebView,
            };

            WireWebViewEvents(newTab);
            _tabs.Add(newTab);
            WebViewContainer.Children.Add(newWebView);
            SwitchToTab(_tabs.Count - 1);
            e.MarkHandled();
        };
    }

    private bool IsActiveTab(TabInfo tab) =>
        _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count && _tabs[_activeTabIndex] == tab;

    private void NavigateToUrlBar()
    {
        var url = UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.Contains("://") && !url.StartsWith("data:") && !HasRegisteredScheme(url)) url = "https://" + url;
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

    private void OnTabClicked(object? sender, RoutedEventArgs e)
    {
        _isDraggingTab = false;
        if (e.Source is DragTabItem item)
            SwitchToTab(item.LogicalIndex);
    }

    private void OnTabReordered(int oldIndex, int newIndex)
    {
        var tab = _tabs[oldIndex];
        _tabs.RemoveAt(oldIndex);
        _tabs.Insert(newIndex, tab);

        if (_activeTabIndex == oldIndex)
            _activeTabIndex = newIndex;
        else if (oldIndex < _activeTabIndex && newIndex >= _activeTabIndex)
            _activeTabIndex--;
        else if (oldIndex > _activeTabIndex && newIndex <= _activeTabIndex)
            _activeTabIndex++;

        RebuildTabStrip();
    }

    private void RebuildTabStrip()
    {
        if (_isDraggingTab) return;

        TabStrip.Children.Clear();

        for (int i = 0; i < _tabs.Count; i++)
        {
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
                Content = "\u2715",
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

            var closedIndex = i;
            closeBtn.Click += (_, _) => CloseTab(closedIndex);

            var tabContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            tabContent.Children.Add(titleText);
            tabContent.Children.Add(closeBtn);

            var tabItem = new DragTabItem
            {
                Content = tabContent,
                Padding = new Thickness(8, 4),
                BorderThickness = new Thickness(1, 1, 1, isActive ? 0 : 1),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin = new Thickness(1, 0),
                MinWidth = 60,
                LogicalIndex = i,
            };

            tabItem.Bind(DragTabItem.BackgroundProperty,
                tabItem.GetResourceObservable(isActive
                    ? "SystemControlBackgroundAltHighBrush"
                    : "SystemControlBackgroundBaseLowBrush"));
            tabItem.Bind(DragTabItem.BorderBrushProperty,
                tabItem.GetResourceObservable("SystemControlForegroundBaseLowBrush"));

            TabStrip.Children.Add(tabItem);
        }
    }

    private static bool HasRegisteredScheme(string url)
    {
        var colonIndex = url.IndexOf(':');
        if (colonIndex <= 0) return false;
        var scheme = url[..colonIndex];
        return ServoLocator.Engine.RegisteredSchemes.Contains(scheme);
    }

    private static string TruncateTitle(string? title, int maxLength)
    {
        if (string.IsNullOrEmpty(title)) return "New Tab";
        return title.Length <= maxLength ? title : title[..(maxLength - 1)] + "\u2026";
    }

    private sealed class TabInfo
    {
        public required ServoWebViewControl WebView { get; init; }
        public string Title { get; set; } = "New Tab";
        public string? Url { get; set; }
    }
}
