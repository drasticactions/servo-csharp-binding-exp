

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Servo.Sharp;

public sealed class ServoErrorEventArgs(byte errorCode, string message) : EventArgs
{
    public byte ErrorCode { get; } = errorCode;
    public string Message { get; } = message;
}

public sealed class ConsoleMessageEventArgs(ConsoleLogLevel level, string message) : EventArgs
{
    public ConsoleLogLevel Level { get; } = level;
    public string Message { get; } = message;
}

public sealed class DevtoolsStartedEventArgs(ushort port, string token) : EventArgs
{
    public ushort Port { get; } = port;
    public string Token { get; } = token;
}

public sealed class ServoEngine : IDisposable
{
    private nint _handle;
    private GCHandle _wakerHandle;
    private GCHandle _delegateHandle;
    private bool _disposed;

    public Action? EventLoopWaker { get; set; }

    public event EventHandler<ServoErrorEventArgs>? Error;

    public event EventHandler<DevtoolsStartedEventArgs>? DevtoolsStarted;

    public event EventHandler<ConsoleMessageEventArgs>? ConsoleMessage;

    public event EventHandler<WebResourceLoadEventArgs>? WebResourceLoadRequested;

    public unsafe ServoEngine(string? resourcePath = null)
    {
        _wakerHandle = GCHandle.Alloc(this);

        var waker = new CEventLoopWaker
        {
            user_data = (void*)GCHandle.ToIntPtr(_wakerHandle),
            wake = &WakeCallbackImpl,
        };

        if (resourcePath != null)
        {
            var pResource = Marshal.StringToCoTaskMemUTF8(resourcePath);
            try { _handle = (nint)ServoNative.servo_new(waker, (byte*)pResource); }
            finally { Marshal.FreeCoTaskMem(pResource); }
        }
        else
        {
            _handle = (nint)ServoNative.servo_new(waker, null);
        }

        if (_handle == 0)
        {
            _wakerHandle.Free();
            var error = GetLastError();
            throw new InvalidOperationException($"Failed to create Servo engine: {error}");
        }

        _delegateHandle = GCHandle.Alloc(this);
        var servoCallbacks = new ServoCallbacks
        {
            user_data = (void*)GCHandle.ToIntPtr(_delegateHandle),
            on_error = &OnErrorImpl,
            on_devtools_started = &OnDevtoolsStartedImpl,
            on_console_message = &OnConsoleMessageImpl,
            on_load_web_resource = &OnLoadWebResourceImpl,
        };
        ServoNative.servo_set_delegate((void*)_handle, servoCallbacks);
    }

    public unsafe void SpinEventLoop()
    {
        ThrowIfDisposed();
        ServoNative.servo_spin_event_loop((void*)_handle);
    }

    public unsafe void SetPreference(string name, string value)
    {
        ThrowIfDisposed();
        var pName = Marshal.StringToCoTaskMemUTF8(name);
        var pValue = Marshal.StringToCoTaskMemUTF8(value);
        try
        {
            ServoNative.servo_set_preference((void*)_handle, (byte*)pName, (byte*)pValue);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pName);
            Marshal.FreeCoTaskMem(pValue);
        }
    }

    internal nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    public unsafe void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_handle != 0)
        {
            ServoNative.servo_destroy((void*)_handle);
            _handle = 0;
        }
        if (_wakerHandle.IsAllocated) _wakerHandle.Free();
        if (_delegateHandle.IsAllocated) _delegateHandle.Free();
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    private static unsafe string GetLastError()
    {
        var ptr = ServoNative.servo_last_error();
        return ptr == null ? "unknown error" : Marshal.PtrToStringUTF8((nint)ptr) ?? "unknown error";
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void WakeCallbackImpl(void* userData)
    {
        var handle = GCHandle.FromIntPtr((nint)userData);
        if (handle.Target is ServoEngine engine)
            engine.EventLoopWaker?.Invoke();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnErrorImpl(void* ud, byte code, byte* msg)
    {
        var h = GCHandle.FromIntPtr((nint)ud);
        if (h.Target is ServoEngine e)
            e.Error?.Invoke(e, new ServoErrorEventArgs(code, Marshal.PtrToStringUTF8((nint)msg) ?? ""));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnDevtoolsStartedImpl(void* ud, ushort port, byte* token)
    {
        var h = GCHandle.FromIntPtr((nint)ud);
        if (h.Target is ServoEngine e)
            e.DevtoolsStarted?.Invoke(e, new DevtoolsStartedEventArgs(port, Marshal.PtrToStringUTF8((nint)token) ?? ""));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnConsoleMessageImpl(void* ud, byte level, byte* msg)
    {
        var h = GCHandle.FromIntPtr((nint)ud);
        if (h.Target is ServoEngine e)
            e.ConsoleMessage?.Invoke(e, new ConsoleMessageEventArgs((ConsoleLogLevel)level, Marshal.PtrToStringUTF8((nint)msg) ?? ""));
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnLoadWebResourceImpl(void* ud, byte* url, byte* method, byte isMainFrame, byte isRedirect, nuint handle)
    {
        var h = GCHandle.FromIntPtr((nint)ud);
        if (h.Target is not ServoEngine e) return;
        var urlStr = Marshal.PtrToStringUTF8((nint)url) ?? "";
        var methodStr = Marshal.PtrToStringUTF8((nint)method) ?? "";
        var args = new WebResourceLoadEventArgs(urlStr, methodStr, isMainFrame != 0, isRedirect != 0, handle);
        var handler = e.WebResourceLoadRequested;
        if (handler != null)
            handler.Invoke(e, args);
        else
            args.Allow();
    }
}
