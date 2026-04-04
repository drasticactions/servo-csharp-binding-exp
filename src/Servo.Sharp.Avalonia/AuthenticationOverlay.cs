using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Servo.Sharp.Avalonia;

public class AuthenticationOverlay : Canvas
{
    private readonly AuthenticationRequestEventArgs _request;
    private readonly Panel _host;
    private readonly TextBox _usernameBox;
    private readonly TextBox _passwordBox;
    private bool _closed;

    public AuthenticationOverlay(Panel host, AuthenticationRequestEventArgs request)
    {
        _request = request;
        _host = host;

        Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0));

        string hostName;
        try { hostName = new Uri(request.Url).Host; }
        catch { hostName = request.Url; }

        var prompt = new TextBlock
        {
            Text = request.ForProxy
                ? "The proxy server requires authentication."
                : $"The server at {hostName} requires a username and password.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Black,
            Margin = new Thickness(0, 0, 0, 12),
        };

        _usernameBox = new TextBox
        {
            PlaceholderText = "Username",
            Margin = new Thickness(0, 0, 0, 8),
        };

        _passwordBox = new TextBox
        {
            PlaceholderText = "Password",
            PasswordChar = '\u2022',
            Margin = new Thickness(0, 0, 0, 16),
        };

        _passwordBox.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                Submit();
                e.Handled = true;
            }
        };

        var signInButton = new Button
        {
            Content = "Sign In",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 80,
        };
        signInButton.Click += (_, _) => Submit();

        var cancelButton = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 80,
        };
        cancelButton.Click += (_, _) => Close(() => _request.Dismiss());

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(signInButton);

        var form = new StackPanel();
        form.Children.Add(prompt);
        form.Children.Add(_usernameBox);
        form.Children.Add(_passwordBox);
        form.Children.Add(buttonPanel);

        var card = new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.Parse("#CCCCCC")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(24),
            MinWidth = 340,
            MaxWidth = 400,
            Child = form,
            BoxShadow = new BoxShadows(new BoxShadow
            {
                OffsetX = 0, OffsetY = 4, Blur = 16,
                Color = Color.FromArgb(80, 0, 0, 0),
            }),
        };

        Children.Add(card);

        // Center the card after layout
        card.LayoutUpdated += OnCardLayout;
    }

    private void OnCardLayout(object? sender, EventArgs e)
    {
        if (sender is not Border card) return;
        card.LayoutUpdated -= OnCardLayout;

        var hostW = _host.Bounds.Width;
        var hostH = _host.Bounds.Height;
        var cardW = card.DesiredSize.Width;
        var cardH = card.DesiredSize.Height;

        SetLeft(card, Math.Max(0, (hostW - cardW) / 2));
        SetTop(card, Math.Max(0, (hostH - cardH) / 2));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _usernameBox.Focus();
    }

    /// <summary>
    /// Dismiss the overlay programmatically (e.g. from hide_embedder_control).
    /// </summary>
    public void DismissIfOpen()
    {
        Close(() => _request.Dismiss());
    }

    private void Submit()
    {
        Close(() => _request.Authenticate(
            _usernameBox.Text ?? "",
            _passwordBox.Text ?? ""));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        // Click on backdrop (not the card) dismisses
        if (e.Source == this)
        {
            Close(() => _request.Dismiss());
            e.Handled = true;
        }
    }

    private void Close(Action respond)
    {
        if (_closed) return;
        _closed = true;
        _host.Children.Remove(this);
        respond();
    }
}
