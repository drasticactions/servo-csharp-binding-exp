namespace Servo.Sharp;

public sealed class ResizeToRequestEventArgs(int width, int height) : EventArgs
{
    public int Width { get; } = width;
    public int Height { get; } = height;
}
