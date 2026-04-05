

namespace Servo.Sharp;

public enum ConsoleLogLevel : byte
{
    Log = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
    Trace = 5,
}

public enum LoadStatus : byte
{
    Started = 0,
    HeadParsed = 1,
    Complete = 2,
}

public enum ServoCursor : byte
{
    None = 0,
    Default = 1,
    Pointer = 2,
    ContextMenu = 3,
    Help = 4,
    Progress = 5,
    Wait = 6,
    Cell = 7,
    Crosshair = 8,
    Text = 9,
    VerticalText = 10,
    Alias = 11,
    Copy = 12,
    Move = 13,
    NoDrop = 14,
    NotAllowed = 15,
    Grab = 16,
    Grabbing = 17,
    EResize = 18,
    NResize = 19,
    NeResize = 20,
    NwResize = 21,
    SResize = 22,
    SeResize = 23,
    SwResize = 24,
    WResize = 25,
    EwResize = 26,
    NsResize = 27,
    NeswResize = 28,
    NwseResize = 29,
    ColResize = 30,
    RowResize = 31,
    AllScroll = 32,
    ZoomIn = 33,
    ZoomOut = 34,
}

public enum PixelFormat : byte
{
    K8 = 0,
    KA8 = 1,
    RGB8 = 2,
    RGBA8 = 3,
    BGRA8 = 4,
}

public enum MouseButtonAction : byte
{
    Down = 0,
    Up = 1,
}

public enum ServoMouseButton : ushort
{
    Left = 0,
    Middle = 1,
    Right = 2,
    Back = 3,
    Forward = 4,
}

[Flags]
public enum KeyModifiers : uint
{
    None = 0,
    Alt = 0x1,
    AltGraph = 0x2,
    CapsLock = 0x4,
    Control = 0x8,
    Fn = 0x10,
    FnLock = 0x20,
    Meta = 0x40,
    NumLock = 0x80,
    ScrollLock = 0x100,
    Shift = 0x200,
    Symbol = 0x400,
    SymbolLock = 0x800,
    Hyper = 0x1000,
    Super = 0x2000,
}

public enum WheelMode : byte
{
    DeltaPixel = 0,
    DeltaLine = 1,
    DeltaPage = 2,
}

public enum TouchEventType : byte
{
    Down = 0,
    Move = 1,
    Up = 2,
    Cancel = 3,
}

public enum EditingAction : byte
{
    Copy = 0,
    Cut = 1,
    Paste = 2,
}

public enum PermissionFeature : byte
{
    Geolocation = 0,
    Notifications = 1,
    Camera = 2,
    Microphone = 3,
}

public enum ServoTheme : byte
{
    Light = 0,
    Dark = 1,
}

public enum MediaSessionAction : byte
{
    Play = 0,
    Pause = 1,
    SeekBackward = 2,
    SeekForward = 3,
    PreviousTrack = 4,
    NextTrack = 5,
    SkipAd = 6,
    Stop = 7,
    SeekTo = 8,
}

public enum InputMethodType : byte
{
    Color = 0,
    Date = 1,
    DatetimeLocal = 2,
    Email = 3,
    Month = 4,
    Number = 5,
    Password = 6,
    Search = 7,
    Tel = 8,
    Text = 9,
    Time = 10,
    Url = 11,
    Week = 12,
}

public enum CompositionState : byte
{
    Start = 0,
    Update = 1,
    End = 2,
}

[Flags]
public enum StorageTypes : byte
{
    Cookies = 1 << 0,
    Local = 1 << 1,
    Session = 1 << 2,
    All = Cookies | Local | Session,
}
