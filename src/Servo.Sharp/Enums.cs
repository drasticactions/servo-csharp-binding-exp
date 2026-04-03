

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
