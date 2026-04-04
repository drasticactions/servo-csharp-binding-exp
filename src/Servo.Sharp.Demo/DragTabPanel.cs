using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Servo.Sharp.Demo;

public class DragTabPanel : Panel
{
    private readonly Dictionary<DragTabItem, double> _homePositions = new();
    private readonly Dictionary<DragTabItem, double> _animatingTargets = new();
    private DragTabItem? _dragItem;
    private DragTabItem? _completedDragItem;

    public event Action<int, int>? TabReordered;

    public DragTabPanel()
    {
        AddHandler(DragTabItem.DragStartedEvent, OnDragStarted, handledEventsToo: true);
        AddHandler(DragTabItem.DragDeltaEvent, OnDragDelta);
        AddHandler(DragTabItem.DragCompletedEvent, OnDragCompleted, handledEventsToo: true);

        Children.CollectionChanged += (_, _) =>
        {
            _homePositions.Clear();
            _animatingTargets.Clear();
            _dragItem = null;
            _completedDragItem = null;
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;

        foreach (var child in Children)
        {
            child.Measure(availableSize);
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        // If dragged item extends beyond normal width, expand
        if (_dragItem is not null)
        {
            var dragEnd = _dragItem.X + _dragItem.DesiredSize.Width;
            if (dragEnd > width) width = dragEnd;
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_completedDragItem is not null)
        {
            var item = _completedDragItem;
            _completedDragItem = null;
            return DragCompletedArrange(item, finalSize);
        }

        return _dragItem is not null
            ? DragArrange(finalSize)
            : NormalArrange(finalSize);
    }

    private Size NormalArrange(Size finalSize)
    {
        double x = 0;
        int index = 0;
        _homePositions.Clear();

        foreach (var child in Children)
        {
            if (child is DragTabItem item)
            {
                item.X = x;
                item.LogicalIndex = index++;
                _homePositions[item] = x;
            }

            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }

        return finalSize;
    }

    private Size DragArrange(Size finalSize)
    {
        var items = Children.OfType<DragTabItem>().ToList();
        var ordered = GetOrderedItems(items, _dragItem!);

        double x = 0;
        foreach (var item in ordered)
        {
            if (item == _dragItem)
            {
                double maxX = Math.Max(0, finalSize.Width - item.DesiredSize.Width);
                item.X = Math.Clamp(item.X, 0, maxX);
                item.Arrange(new Rect(item.X, 0, item.DesiredSize.Width, finalSize.Height));
            }
            else
            {
                SendToLocation(item, x, finalSize.Height);
            }

            x += item.DesiredSize.Width;
        }

        return finalSize;
    }

    private Size DragCompletedArrange(DragTabItem dragItem, Size finalSize)
    {
        var items = Children.OfType<DragTabItem>().ToList();
        var ordered = GetOrderedItems(items, dragItem);

        // Find original index of dragged item in Children order
        int oldIndex = -1;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] == dragItem) { oldIndex = i; break; }
        }

        double x = 0;
        int logicalIndex = 0;
        int newIndex = -1;

        foreach (var item in ordered)
        {
            item.X = x;
            item.Arrange(new Rect(x, 0, item.DesiredSize.Width, finalSize.Height));
            if (item == dragItem) newIndex = logicalIndex;
            item.LogicalIndex = logicalIndex++;
            _homePositions[item] = x;
            x += item.DesiredSize.Width;
        }

        if (oldIndex >= 0 && newIndex >= 0 && oldIndex != newIndex)
        {
            Dispatcher.UIThread.Post(() => TabReordered?.Invoke(oldIndex, newIndex));
        }

        return finalSize;
    }

    private List<DragTabItem> GetOrderedItems(List<DragTabItem> items, DragTabItem dragItem)
    {
        double dragMid = dragItem.X + dragItem.DesiredSize.Width / 2;

        var others = items.Where(i => i != dragItem).ToList();
        var result = new List<DragTabItem>();
        bool inserted = false;

        foreach (var other in others)
        {
            double otherMid = GetStablePosition(other) + other.DesiredSize.Width / 2;
            if (!inserted && dragMid < otherMid)
            {
                result.Add(dragItem);
                inserted = true;
            }

            result.Add(other);
        }

        if (!inserted) result.Add(dragItem);

        return result;
    }

    private double GetStablePosition(DragTabItem item)
    {
        if (_animatingTargets.TryGetValue(item, out double target))
            return target;
        return item.X;
    }

    private async void SendToLocation(DragTabItem item, double targetX, double height)
    {
        if (_animatingTargets.TryGetValue(item, out double activeTarget))
        {
            // Already animating - arrange at current X (the animation is updating it)
            item.Arrange(new Rect(item.X, 0, item.DesiredSize.Width, height));

            // If target changed, update it but let current animation finish
            if (Math.Abs(activeTarget - targetX) > 1)
                _animatingTargets[item] = targetX;

            return;
        }

        if (Math.Abs(item.X - targetX) < 1)
        {
            item.X = targetX;
            item.Arrange(new Rect(targetX, 0, item.DesiredSize.Width, height));
            return;
        }

        _animatingTargets[item] = targetX;

        var animation = new Animation
        {
            Easing = new CubicEaseOut(),
            Duration = TimeSpan.FromMilliseconds(200),
            FillMode = FillMode.None,
            Children =
            {
                new KeyFrame
                {
                    KeyTime = TimeSpan.FromMilliseconds(200),
                    Setters = { new Setter(DragTabItem.XProperty, targetX) }
                }
            }
        };

        await animation.RunAsync(item);

        item.X = targetX;
        item.Arrange(new Rect(targetX, 0, item.DesiredSize.Width, height));
        _animatingTargets.Remove(item);
    }

    private void OnDragStarted(object? sender, DragTabEventArgs e)
    {
        _dragItem = e.TabItem;
        e.Handled = true;
    }

    private void OnDragDelta(object? sender, DragTabEventArgs e)
    {
        if (_dragItem == null || e.TabItem != _dragItem) return;

        if (!_dragItem.IsDragging)
        {
            _dragItem.IsDragging = true;
            _dragItem.Opacity = 0.7;
            _dragItem.ZIndex = int.MaxValue;
        }

        _dragItem.X += e.DeltaX;
        Dispatcher.UIThread.Post(InvalidateMeasure, DispatcherPriority.Loaded);
        e.Handled = true;
    }

    private void OnDragCompleted(object? sender, DragTabEventArgs e)
    {
        if (_dragItem != null)
        {
            _dragItem.Opacity = 1.0;
            _dragItem.ZIndex = 0;
            _completedDragItem = _dragItem;
            _dragItem = null;
        }

        _animatingTargets.Clear();
        Dispatcher.UIThread.Post(InvalidateMeasure, DispatcherPriority.Loaded);
        e.Handled = true;
    }
}
