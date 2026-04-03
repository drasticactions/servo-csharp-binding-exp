namespace Servo.Sharp;

public sealed class MediaSessionEventArgs(byte eventType, string json) : EventArgs
{
    /// 0=SetMetadata, 1=PlaybackStateChange, 2=SetPositionState
    public byte EventType { get; } = eventType;
    /// JSON string with event data.
    public string Json { get; } = json;
}
