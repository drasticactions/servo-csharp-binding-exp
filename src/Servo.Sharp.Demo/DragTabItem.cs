using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Servo.Sharp.Demo;

public class DragTabItem : ContentControl
{
    public static readonly StyledProperty<double> XProperty =
        AvaloniaProperty.Register<DragTabItem, double>(nameof(X));

    public static readonly RoutedEvent<DragTabEventArgs> DragStartedEvent =
        RoutedEvent.Register<DragTabItem, DragTabEventArgs>(nameof(DragStarted), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<DragTabEventArgs> DragDeltaEvent =
        RoutedEvent.Register<DragTabItem, DragTabEventArgs>(nameof(DragDelta), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<DragTabEventArgs> DragCompletedEvent =
        RoutedEvent.Register<DragTabItem, DragTabEventArgs>(nameof(DragCompleted), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<RoutedEventArgs> TabClickedEvent =
        RoutedEvent.Register<DragTabItem, RoutedEventArgs>(nameof(TabClicked), RoutingStrategies.Bubble);

    private Point? _lastPoint;
    private bool _isDragging;

    public double X
    {
        get => GetValue(XProperty);
        set => SetValue(XProperty, value);
    }

    public bool IsDragging
    {
        get => _isDragging;
        internal set => _isDragging = value;
    }

    public int LogicalIndex { get; internal set; }

    public event EventHandler<DragTabEventArgs>? DragStarted
    {
        add => AddHandler(DragStartedEvent, value);
        remove => RemoveHandler(DragStartedEvent, value);
    }

    public event EventHandler<DragTabEventArgs>? DragDelta
    {
        add => AddHandler(DragDeltaEvent, value);
        remove => RemoveHandler(DragDeltaEvent, value);
    }

    public event EventHandler<DragTabEventArgs>? DragCompleted
    {
        add => AddHandler(DragCompletedEvent, value);
        remove => RemoveHandler(DragCompletedEvent, value);
    }

    public event EventHandler<RoutedEventArgs>? TabClicked
    {
        add => AddHandler(TabClickedEvent, value);
        remove => RemoveHandler(TabClickedEvent, value);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // Don't intercept child button clicks (e.g., close button)
        var source = e.Source as Visual;
        while (source != null && source != this)
        {
            if (source is Button) return;
            source = source.GetVisualParent() as Visual;
        }

        e.Handled = true;
        _lastPoint = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.PreventGestureRecognition();

        RaiseEvent(new DragTabEventArgs(DragStartedEvent, this));
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_lastPoint == null) return;

        var pos = e.GetPosition(this);
        var deltaX = pos.X - _lastPoint.Value.X;

        if (!_isDragging && Math.Abs(deltaX) < 5) return;

        _isDragging = true;

        RaiseEvent(new DragTabEventArgs(DragDeltaEvent, this, deltaX));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_lastPoint == null) return;
        CompleteDragInternal();
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        CompleteDragInternal();
        base.OnPointerCaptureLost(e);
    }

    private void CompleteDragInternal()
    {
        if (_lastPoint == null) return;
        _lastPoint = null;

        var wasDragging = _isDragging;
        _isDragging = false;

        if (wasDragging)
            RaiseEvent(new DragTabEventArgs(DragCompletedEvent, this));
        else
            RaiseEvent(new RoutedEventArgs(TabClickedEvent));
    }
}

public class DragTabEventArgs : RoutedEventArgs
{
    public DragTabEventArgs(RoutedEvent routedEvent, DragTabItem tabItem, double deltaX = 0)
        : base(routedEvent)
    {
        TabItem = tabItem;
        DeltaX = deltaX;
    }

    public DragTabItem TabItem { get; }
    public double DeltaX { get; }
}
