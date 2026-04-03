

using System.Runtime.InteropServices;

namespace Servo.Sharp;

public sealed class SoftwareRenderingContext : IDisposable
{
    private nint _handle;
    private bool _disposed;

    public unsafe SoftwareRenderingContext(uint width, uint height)
    {
        _handle = (nint)ServoNative.rendering_context_new_software(width, height);
        if (_handle == 0)
        {
            throw new InvalidOperationException("Failed to create SoftwareRenderingContext");
        }
    }

    internal nint Handle
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _handle;
        }
    }

    /// <summary>
    /// Resize the rendering surface.
    /// </summary>
    public unsafe void Resize(uint width, uint height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ServoNative.rendering_context_resize((void*)_handle, width, height);
    }

    /// <summary>
    /// Present the rendered frame (swap buffers).
    /// </summary>
    public unsafe void Present()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ServoNative.rendering_context_present((void*)_handle);
    }

    /// <summary>
    /// Make this rendering context current on the calling thread.
    /// </summary>
    public unsafe bool MakeCurrent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ServoNative.rendering_context_make_current((void*)_handle) == 0;
    }

    /// <summary>
    /// Read the rendered pixels as RGBA8 data.
    /// Returns null if no frame has been rendered yet.
    /// </summary>
    public unsafe PixelData? ReadPixels()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint width, height;
        nuint len;
        var ptr = ServoNative.rendering_context_read_pixels(
            (void*)_handle, &width, &height, &len);

        if (ptr == null || len == 0)
            return null;

        var data = new byte[len];
        Marshal.Copy((nint)ptr, data, 0, (int)len);
        ServoNative.servo_free_bytes(ptr, len);

        return new PixelData(data, width, height);
    }

    public unsafe bool ReadPixelsInto(nint destination, nuint destinationLength, out uint width, out uint height)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        uint w, h;
        var result = ServoNative.rendering_context_read_pixels_into(
            (void*)_handle, (byte*)destination, destinationLength, &w, &h);
        width = w;
        height = h;
        return result != 0;
    }

    public unsafe void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != 0)
        {
            ServoNative.rendering_context_destroy((void*)_handle);
            _handle = 0;
        }
    }
}