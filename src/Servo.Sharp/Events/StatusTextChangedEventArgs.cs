namespace Servo.Sharp;

public sealed class StatusTextChangedEventArgs(string? statusText) : EventArgs
{
    public string? StatusText { get; } = statusText;
}
