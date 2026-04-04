// Copyright 2026 The Servo C# Bindings Contributors
// SPDX-License-Identifier: MPL-2.0

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Servo.Sharp.Demo;

public class AuthenticationDialog : Window
{
    private readonly TextBox _usernameBox;
    private readonly TextBox _passwordBox;

    public string? Username { get; private set; }
    public string? Password { get; private set; }
    public bool WasSubmitted { get; private set; }

    public AuthenticationDialog(string url, bool forProxy)
    {
        Title = forProxy ? "Proxy Authentication Required" : "Authentication Required";
        Width = 380;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var prompt = new TextBlock
        {
            Text = forProxy
                ? $"The proxy server requires a username and password."
                : $"The server at {new Uri(url).Host} requires a username and password.",
            TextWrapping = TextWrapping.Wrap,
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

        var okButton = new Button
        {
            Content = "Sign In",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 80,
            IsDefault = true,
        };
        okButton.Click += (_, _) =>
        {
            Username = _usernameBox.Text;
            Password = _passwordBox.Text;
            WasSubmitted = true;
            Close();
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = 80,
            IsCancel = true,
        };
        cancelButton.Click += (_, _) => Close();

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttonPanel.Children.Add(cancelButton);
        buttonPanel.Children.Add(okButton);

        var stack = new StackPanel
        {
            Margin = new Thickness(20),
        };
        stack.Children.Add(prompt);
        stack.Children.Add(_usernameBox);
        stack.Children.Add(_passwordBox);
        stack.Children.Add(buttonPanel);

        Content = stack;
    }
}
