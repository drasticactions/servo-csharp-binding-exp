namespace Servo.Sharp;

public sealed class LoadStatusChangedEventArgs(LoadStatus status) : EventArgs
{
    public LoadStatus Status { get; } = status;
}
