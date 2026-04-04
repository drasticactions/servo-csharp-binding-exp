using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Servo.Sharp.Avalonia;

/// <summary>
/// A dropdown overlay for HTML &lt;select&gt; elements. Add this to a Panel
/// overlaying the web view. It positions itself relative to the select element,
/// handles user selection, and removes itself when done.
/// </summary>
public class SelectElementOverlay : Canvas
{
    private readonly SelectElementRequestEventArgs _request;
    private readonly Panel _host;
    private readonly ListBox _listBox;
    private readonly Border _dropdown;
    private bool _closed;

    public SelectElementOverlay(Panel host, SelectElementRequestEventArgs request)
    {
        _request = request;
        _host = host;

        // Transparent background so the entire overlay is hit-testable (catches outside clicks)
        Background = Brushes.Transparent;

        _listBox = new ListBox
        {
            MaxHeight = 300,
            MinWidth = Math.Max(150, request.PositionWidth),
            // Disable selection entirely — we handle clicks ourselves to avoid
            // spurious SelectionChanged events during layout/resize.
            SelectionMode = SelectionMode.Toggle,
        };

        BuildItems();

        _dropdown = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Child = _listBox,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0, OffsetY = 2, Blur = 8,
                Color = Color.FromArgb(64, 0, 0, 0),
            }),
        };

        Children.Add(_dropdown);

        // Initial position (will be adjusted after layout measures the dropdown)
        SetLeft(_dropdown, request.PositionX);
        SetTop(_dropdown, request.PositionY + request.PositionHeight);
    }

    private void BuildItems()
    {
        foreach (var item in _request.Options)
        {
            if (item.IsGroup)
            {
                _listBox.Items.Add(new ListBoxItem
                {
                    Content = new TextBlock
                    {
                        Text = item.GroupLabel ?? "",
                        FontWeight = FontWeight.Bold,
                        Margin = new Thickness(4, 2),
                    },
                    IsEnabled = false,
                    IsHitTestVisible = false,
                });

                foreach (var opt in item.GroupOptions!)
                    AddOptionItem(opt);
            }
            else
            {
                AddOptionItem(item.Option!);
            }
        }
    }

    private void AddOptionItem(SelectOption opt)
    {
        var item = new ListBoxItem
        {
            Content = new TextBlock
            {
                Text = opt.Label,
                Margin = new Thickness(4, 2, 4, 2),
            },
            Tag = opt.Id,
            IsEnabled = !opt.IsDisabled,
        };

        if (_request.SelectedOptionId == opt.Id)
            item.IsSelected = true;

        // Use PointerReleased instead of SelectionChanged to detect actual user clicks.
        // SelectionChanged fires spuriously when items resize during layout.
        if (!opt.IsDisabled)
        {
            item.PointerReleased += OnItemPointerReleased;
        }

        _listBox.Items.Add(item);
    }

    private void OnItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is ListBoxItem { Tag: int id })
            Close(() => _request.Select(id));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _dropdown.LayoutUpdated += OnFirstLayout;
        _host.PropertyChanged += OnHostPropertyChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _host.PropertyChanged -= OnHostPropertyChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnHostPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
            Close(() => _request.Dismiss());
    }

    private void OnFirstLayout(object? sender, EventArgs e)
    {
        _dropdown.LayoutUpdated -= OnFirstLayout;
        AdjustPosition();
    }

    private void AdjustPosition()
    {
        var containerH = _host.Bounds.Height;
        var containerW = _host.Bounds.Width;
        var ddH = _dropdown.DesiredSize.Height;
        var ddW = _dropdown.DesiredSize.Width;

        double dipX = _request.PositionX;
        double dipY = _request.PositionY;
        double dipH = _request.PositionHeight;

        // Prefer below the element; flip above if it would overflow
        var top = dipY + dipH;
        if (top + ddH > containerH && dipY - ddH >= 0)
            top = dipY - ddH;

        // Clamp so it doesn't overflow any edge
        top = Math.Max(0, Math.Min(top, containerH - ddH));
        var left = Math.Max(0, Math.Min(dipX, containerW - ddW));

        SetLeft(_dropdown, left);
        SetTop(_dropdown, top);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // If the click landed on the overlay background (not the dropdown), dismiss
        if (e.Source == this)
        {
            Close(() => _request.Dismiss());
            e.Handled = true;
        }
    }

    public void DismissIfOpen()
    {
        Close(() => _request.Dismiss());
    }

    private void Close(Action respond)
    {
        if (_closed) return;
        _closed = true;

        _host.Children.Remove(this);
        respond();
    }
}
