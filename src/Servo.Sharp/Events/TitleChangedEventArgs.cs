namespace Servo.Sharp;

public sealed class TitleChangedEventArgs(string? title) : EventArgs
{
    public string? Title { get; } = title;
}
