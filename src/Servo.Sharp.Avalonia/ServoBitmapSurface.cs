using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Servo.Sharp.Avalonia;

internal class ServoBitmapSurface : Control
{
    private WriteableBitmap? _bitmap;
    private PixelData? _pendingPixels;
    private readonly object _pixelLock = new();

    public void UpdatePixels(PixelData? pixels)
    {
        lock (_pixelLock) { _pendingPixels = pixels; }
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        PixelData? pixels;
        lock (_pixelLock) { pixels = _pendingPixels; }

        if (pixels == null || pixels.Data.Length == 0)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            return;
        }

        if (_bitmap == null || _bitmap.PixelSize.Width != (int)pixels.Width ||
            _bitmap.PixelSize.Height != (int)pixels.Height)
        {
            _bitmap?.Dispose();
            _bitmap = new WriteableBitmap(
                new PixelSize((int)pixels.Width, (int)pixels.Height),
                new Vector(96, 96),
                PixelFormats.Rgba8888,
                AlphaFormat.Premul);
        }

        using (var fb = _bitmap.Lock())
        {
            System.Runtime.InteropServices.Marshal.Copy(
                pixels.Data, 0, fb.Address, pixels.Data.Length);
        }

        var destRect = new Rect(Bounds.Size);
        context.DrawImage(_bitmap, destRect);
    }
}
