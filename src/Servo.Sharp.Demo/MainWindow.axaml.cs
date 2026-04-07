// Copyright 2026 The Servo C# Bindings Contributors
// SPDX-License-Identifier: MPL-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Servo.Sharp;
using Servo.Sharp.Avalonia;
using Servo.Sharp.Demo.Core;

namespace Servo.Sharp.Demo;

public partial class MainWindow : Window
{
    private readonly List<TabInfo> _tabs = new();
    private int _activeTabIndex = -1;
    private bool _isDraggingTab;
    private bool _experimentalFeaturesEnabled;
    private bool _profilerRunning;

    public MainWindow() : this(ServoAppSetup.NewTabUrl) { }

    public MainWindow(string? initialUrl)
    {
        InitializeComponent();

        BackButton.Click += OnBackClick;
        BackButton.PointerPressed += OnBackButtonPointerPressed;
        ForwardButton.Click += OnForwardClick;
        ForwardButton.PointerPressed += OnForwardButtonPointerPressed;
        ReloadButton.Click += OnReloadClick;
        UrlBar.KeyDown += OnUrlBarKeyDown;
        NewTabButton.Click += OnNewTabClick;
        NewWindowButton.Click += OnNewWindowClick;
        SettingsButton.Click += OnSettingsClick;

        TabStrip.AddHandler(DragTabItem.TabClickedEvent, OnTabClicked);
        TabStrip.AddHandler(DragTabItem.DragStartedEvent, OnDragStarted, handledEventsToo: true);
        TabStrip.AddHandler(DragTabItem.DragCompletedEvent, OnDragCompleted, handledEventsToo: true);
        TabStrip.TabReordered += OnTabReordered;

        AddTab(initialUrl ?? ServoAppSetup.NewTabUrl);
    }

    protected override void OnClosed(EventArgs e)
    {
        BackButton.Click -= OnBackClick;
        BackButton.PointerPressed -= OnBackButtonPointerPressed;
        ForwardButton.Click -= OnForwardClick;
        ForwardButton.PointerPressed -= OnForwardButtonPointerPressed;
        ReloadButton.Click -= OnReloadClick;
        UrlBar.KeyDown -= OnUrlBarKeyDown;
        NewTabButton.Click -= OnNewTabClick;
        NewWindowButton.Click -= OnNewWindowClick;
        SettingsButton.Click -= OnSettingsClick;

        TabStrip.RemoveHandler(DragTabItem.TabClickedEvent, OnTabClicked);
        TabStrip.RemoveHandler(DragTabItem.DragStartedEvent, OnDragStarted);
        TabStrip.RemoveHandler(DragTabItem.DragCompletedEvent, OnDragCompleted);
        TabStrip.TabReordered -= OnTabReordered;

        if (SettingsPanel.IsVisible)
            UnwireSettingsEvents();

        foreach (var tab in _tabs)
            UnwireWebViewEvents(tab);

        _tabs.Clear();

        base.OnClosed(e);
    }

    private ServoWebViewControl? ActiveWebView => _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count
        ? _tabs[_activeTabIndex].WebView
        : null;

    private void OnBackClick(object? sender, RoutedEventArgs e) => ActiveWebView?.GoBack();
    private void OnForwardClick(object? sender, RoutedEventArgs e) => ActiveWebView?.GoForward();
    private void OnReloadClick(object? sender, RoutedEventArgs e) => ActiveWebView?.Reload();
    private void OnNewTabClick(object? sender, RoutedEventArgs e) => AddTab(ServoAppSetup.NewTabUrl);
    private void OnNewWindowClick(object? sender, RoutedEventArgs e) => OpenNewWindow();
    private void OnSettingsClick(object? sender, RoutedEventArgs e) => ToggleSettingsPanel();
    private void OnCloseSettingsClick(object? sender, RoutedEventArgs e) => ToggleSettingsPanel();
    private void OnDragStarted(object? sender, DragTabEventArgs e) => _isDraggingTab = true;
    private void OnDragCompleted(object? sender, DragTabEventArgs e) => _isDraggingTab = false;

    private void OnUrlBarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) NavigateToUrlBar();
    }

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
        UnwireWebViewEvents(tab);
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
        var window = new MainWindow(ServoAppSetup.NewTabUrl);
        window.Show();
    }

    private void ToggleSettingsPanel()
    {
        var visible = !SettingsPanel.IsVisible;
        SettingsPanel.IsVisible = visible;

        if (visible)
        {
            ExperimentalFeaturesToggle.IsChecked = _experimentalFeaturesEnabled;
            WireSettingsEvents();
        }
        else
        {
            UnwireSettingsEvents();
        }
    }

    private void WireSettingsEvents()
    {
        ExperimentalFeaturesToggle.IsCheckedChanged += OnExperimentalToggled;
        ProfilerToggle.IsCheckedChanged += OnProfilerToggled;
        TextureCacheToggle.IsCheckedChanged += OnTextureCacheToggled;
        RenderTargetToggle.IsCheckedChanged += OnRenderTargetToggled;
        ToggleProfilerButton.Click += OnToggleSamplingProfiler;
        CaptureButton.Click += OnCaptureWebRender;
        MemoryReportButton.Click += OnMemoryReport;
        CloseSettingsButton.Click += OnCloseSettingsClick;
    }

    private void UnwireSettingsEvents()
    {
        ExperimentalFeaturesToggle.IsCheckedChanged -= OnExperimentalToggled;
        ProfilerToggle.IsCheckedChanged -= OnProfilerToggled;
        TextureCacheToggle.IsCheckedChanged -= OnTextureCacheToggled;
        RenderTargetToggle.IsCheckedChanged -= OnRenderTargetToggled;
        ToggleProfilerButton.Click -= OnToggleSamplingProfiler;
        CaptureButton.Click -= OnCaptureWebRender;
        MemoryReportButton.Click -= OnMemoryReport;
        CloseSettingsButton.Click -= OnCloseSettingsClick;
    }

    private void OnExperimentalToggled(object? sender, RoutedEventArgs e)
    {
        _experimentalFeaturesEnabled = ExperimentalFeaturesToggle.IsChecked == true;
        var value = _experimentalFeaturesEnabled ? "true" : "false";

        foreach (var pref in ServoProtocolHandler.ExperimentalPrefs)
            ServoLocator.Engine.SetPreference(pref, value);

        foreach (var tab in _tabs)
            tab.WebView.Reload();

        StatusText.Text = _experimentalFeaturesEnabled
            ? "Experimental web platform features enabled"
            : "Experimental web platform features disabled";
    }

    private void OnProfilerToggled(object? sender, RoutedEventArgs e) =>
        ActiveWebView?.WebView?.ToggleWebRenderDebugging(WebRenderDebugOption.Profiler);

    private void OnTextureCacheToggled(object? sender, RoutedEventArgs e) =>
        ActiveWebView?.WebView?.ToggleWebRenderDebugging(WebRenderDebugOption.TextureCacheDebug);

    private void OnRenderTargetToggled(object? sender, RoutedEventArgs e) =>
        ActiveWebView?.WebView?.ToggleWebRenderDebugging(WebRenderDebugOption.RenderTargetDebug);

    private void OnToggleSamplingProfiler(object? sender, RoutedEventArgs e)
    {
        var wv = ActiveWebView?.WebView;
        if (wv == null) return;

        wv.ToggleSamplingProfiler(
            TimeSpan.FromMilliseconds((double)(ProfilerRate.Value ?? 1)),
            TimeSpan.FromSeconds((double)(ProfilerDuration.Value ?? 5)));

        _profilerRunning = !_profilerRunning;
        ToggleProfilerButton.Content = _profilerRunning ? "Stop Profiler" : "Start Profiler";
    }

    private void OnCaptureWebRender(object? sender, RoutedEventArgs e) =>
        ActiveWebView?.WebView?.CaptureWebRender();

    private async void OnMemoryReport(object? sender, RoutedEventArgs e)
    {
        MemoryReportButton.IsEnabled = false;
        MemoryReportButton.Content = "Generating...";
        MemoryReportOutput.IsVisible = true;
        MemoryReportOutput.Text = "Requesting memory report...";

        try
        {
            var json = await ServoLocator.Engine.CreateMemoryReportAsync();
            if (json == null)
            {
                MemoryReportOutput.Text = "Failed to generate memory report.";
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                MemoryReportOutput.Text = JsonSerializer.Serialize(doc, DemoJsonContext.Default.JsonDocument);
            }
            catch
            {
                MemoryReportOutput.Text = json;
            }
        }
        catch (Exception ex)
        {
            MemoryReportOutput.Text = $"Error: {ex.Message}";
        }
        finally
        {
            MemoryReportButton.IsEnabled = true;
            MemoryReportButton.Content = "Generate Report";
        }
    }

    private void WireWebViewEvents(TabInfo tab)
    {
        var wv = tab.WebView;
        tab.OnNavigated = (_, e) =>
        {
            tab.Url = e.Url;
            if (IsActiveTab(tab))
                UrlBar.Text = e.Url;
        };
        tab.OnTitleChanged = (_, e) =>
        {
            tab.Title = e.Title ?? "New Tab";
            if (IsActiveTab(tab))
                UpdateWindowTitle(tab);
            RebuildTabStrip();
        };
        tab.OnLoadStatusChanged = (_, e) =>
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
        tab.OnPropertyChanged = (_, e) =>
        {
            if (!IsActiveTab(tab)) return;
            if (e.Property == ServoWebViewControl.CanGoBackProperty)
                BackButton.IsEnabled = wv.CanGoBack;
            else if (e.Property == ServoWebViewControl.CanGoForwardProperty)
                ForwardButton.IsEnabled = wv.CanGoForward;
        };
        tab.OnConsoleMessage = (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = $"[{e.Level}] {e.Message}";
        };
        tab.OnStatusTextChanged = (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = e.StatusText ?? "Ready";
        };
        tab.OnMoveToRequested = (_, e) => Position = new PixelPoint(e.X, e.Y);
        tab.OnResizeToRequested = (_, e) =>
        {
            Width = e.Width;
            Height = e.Height;
        };
        tab.OnTraversalCompleted = (_, _) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = "Navigation traversal complete";
        };
        tab.OnCrashed = (_, e) =>
        {
            if (IsActiveTab(tab))
                StatusText.Text = $"CRASHED: {e.Reason}";
        };
        tab.OnHistoryChanged = (_, e) =>
        {
            tab.HistoryUrls = e.Urls;
            tab.HistoryIndex = e.CurrentIndex;
        };
        tab.OnCreateNewWebViewRequested = (_, e) =>
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

        wv.Navigated += tab.OnNavigated;
        wv.TitleChanged += tab.OnTitleChanged;
        wv.LoadStatusChanged += tab.OnLoadStatusChanged;
        wv.PropertyChanged += tab.OnPropertyChanged;
        wv.ConsoleMessage += tab.OnConsoleMessage;
        wv.StatusTextChanged += tab.OnStatusTextChanged;
        wv.MoveToRequested += tab.OnMoveToRequested;
        wv.ResizeToRequested += tab.OnResizeToRequested;
        wv.TraversalCompleted += tab.OnTraversalCompleted;
        wv.Crashed += tab.OnCrashed;
        wv.HistoryChanged += tab.OnHistoryChanged;
        wv.CreateNewWebViewRequested += tab.OnCreateNewWebViewRequested;
    }

    private static void UnwireWebViewEvents(TabInfo tab)
    {
        var wv = tab.WebView;
        if (tab.OnNavigated != null) wv.Navigated -= tab.OnNavigated;
        if (tab.OnTitleChanged != null) wv.TitleChanged -= tab.OnTitleChanged;
        if (tab.OnLoadStatusChanged != null) wv.LoadStatusChanged -= tab.OnLoadStatusChanged;
        if (tab.OnPropertyChanged != null) wv.PropertyChanged -= tab.OnPropertyChanged;
        if (tab.OnConsoleMessage != null) wv.ConsoleMessage -= tab.OnConsoleMessage;
        if (tab.OnStatusTextChanged != null) wv.StatusTextChanged -= tab.OnStatusTextChanged;
        if (tab.OnMoveToRequested != null) wv.MoveToRequested -= tab.OnMoveToRequested;
        if (tab.OnResizeToRequested != null) wv.ResizeToRequested -= tab.OnResizeToRequested;
        if (tab.OnTraversalCompleted != null) wv.TraversalCompleted -= tab.OnTraversalCompleted;
        if (tab.OnCrashed != null) wv.Crashed -= tab.OnCrashed;
        if (tab.OnHistoryChanged != null) wv.HistoryChanged -= tab.OnHistoryChanged;
        if (tab.OnCreateNewWebViewRequested != null) wv.CreateNewWebViewRequested -= tab.OnCreateNewWebViewRequested;
    }

    private bool IsActiveTab(TabInfo tab) =>
        _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count && _tabs[_activeTabIndex] == tab;

    private void NavigateToUrlBar()
    {
        var url = UrlBar.Text;
        if (string.IsNullOrWhiteSpace(url)) return;
        url = ServoAppSetup.NormalizeUrl(url);
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

    private void OnBackButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(BackButton).Properties.IsRightButtonPressed) return;
        e.Handled = true;
        var tab = _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;
        if (tab == null || tab.HistoryUrls.Count == 0) return;

        var menu = new ContextMenu();
        for (int i = tab.HistoryIndex - 1; i >= 0; i--)
        {
            var steps = tab.HistoryIndex - i;
            var url = tab.HistoryUrls[i];
            var item = new MenuItem { Header = TruncateTitle(url, 60) };
            item.Click += (_, _) => ActiveWebView?.GoBack(steps);
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.PlacementTarget = BackButton;
            menu.Open(BackButton);
        }
    }

    private void OnForwardButtonPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(ForwardButton).Properties.IsRightButtonPressed) return;
        e.Handled = true;
        var tab = _activeTabIndex >= 0 && _activeTabIndex < _tabs.Count ? _tabs[_activeTabIndex] : null;
        if (tab == null || tab.HistoryUrls.Count == 0) return;

        var menu = new ContextMenu();
        for (int i = tab.HistoryIndex + 1; i < tab.HistoryUrls.Count; i++)
        {
            var steps = i - tab.HistoryIndex;
            var url = tab.HistoryUrls[i];
            var item = new MenuItem { Header = TruncateTitle(url, 60) };
            item.Click += (_, _) => ActiveWebView?.GoForward(steps);
            menu.Items.Add(item);
        }

        if (menu.Items.Count > 0)
        {
            menu.PlacementTarget = ForwardButton;
            menu.Open(ForwardButton);
        }
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
        public IReadOnlyList<string> HistoryUrls { get; set; } = [];
        public int HistoryIndex { get; set; }

        // Stored event handlers for unwiring
        public EventHandler<UrlChangedEventArgs>? OnNavigated { get; set; }
        public EventHandler<TitleChangedEventArgs>? OnTitleChanged { get; set; }
        public EventHandler<LoadStatusChangedEventArgs>? OnLoadStatusChanged { get; set; }
        public EventHandler<AvaloniaPropertyChangedEventArgs>? OnPropertyChanged { get; set; }
        public EventHandler<ConsoleMessageEventArgs>? OnConsoleMessage { get; set; }
        public EventHandler<StatusTextChangedEventArgs>? OnStatusTextChanged { get; set; }
        public EventHandler<MoveToRequestEventArgs>? OnMoveToRequested { get; set; }
        public EventHandler<ResizeToRequestEventArgs>? OnResizeToRequested { get; set; }
        public EventHandler? OnTraversalCompleted { get; set; }
        public EventHandler<CrashedEventArgs>? OnCrashed { get; set; }
        public EventHandler<HistoryChangedEventArgs>? OnHistoryChanged { get; set; }
        public EventHandler<CreateNewWebViewRequestEventArgs>? OnCreateNewWebViewRequested { get; set; }
    }
}
