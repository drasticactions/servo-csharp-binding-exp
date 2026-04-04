namespace Servo.Sharp;

public sealed class NotificationEventArgs(string title, string body) : EventArgs
{
    public string Title { get; } = title;
    public string Body { get; } = body;
}
