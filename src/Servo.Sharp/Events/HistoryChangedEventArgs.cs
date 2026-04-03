namespace Servo.Sharp;

public sealed class HistoryChangedEventArgs(int currentIndex, int totalEntries) : EventArgs
{
    public int CurrentIndex { get; } = currentIndex;
    public int TotalEntries { get; } = totalEntries;
}
