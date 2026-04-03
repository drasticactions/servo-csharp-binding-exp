using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Servo.Sharp.Avalonia;

internal class ServoBitmapSurface : Control
{
    private WriteableBitmap? _bitmap;
    private bool _frameReady;

    private HardwareRenderingContext? _hwContext;
    private SoftwareRenderingContext? _swContext;

    public void SetRenderingContext(HardwareRenderingContext? hw, SoftwareRenderingContext? sw)
    {
        _hwContext = hw;
        _swContext = sw;
    }

    public void MarkFrameReady()
    {
        _frameReady = true;
        InvalidateVisual();
    }

    public override unsafe void Render(DrawingContext context)
    {
        if (!_frameReady)
        {
            if (_bitmap != null)
            {
                context.DrawImage(_bitmap, new Rect(Bounds.Size));
            }
            else
            {
                context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            }
            return;
        }

        _frameReady = false;

        uint w, h;
        bool ok;

        if (_bitmap != null)
        {
            using (var fb = _bitmap.Lock())
            {
                var capacity = (nuint)(fb.RowBytes * _bitmap.PixelSize.Height);
                if (_hwContext != null)
                    ok = _hwContext.ReadPixelsInto(fb.Address, capacity, out w, out h);
                else if (_swContext != null)
                    ok = _swContext.ReadPixelsInto(fb.Address, capacity, out w, out h);
                else
                {
                    context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
                    return;
                }

                if (ok && w == (uint)_bitmap.PixelSize.Width && h == (uint)_bitmap.PixelSize.Height)
                {
                    // Wrote directly into the bitmap - just draw it.
                }
                else if (ok)
                {
                    // Size changed. We need to recreate the bitmap and re-read.
                    // Fall through to the reallocation path below.
                    _bitmap.Dispose();
                    _bitmap = null;
                }
                else
                {
                    context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
                    return;
                }
            }

            if (_bitmap != null)
            {
                context.DrawImage(_bitmap, new Rect(Bounds.Size));
                return;
            }
        }

        // First frame or size changed: read via old path to learn size, create bitmap, re-read.
        var pixels = _hwContext?.ReadPixels() ?? _swContext?.ReadPixels();
        if (pixels == null || pixels.Data.Length == 0)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            return;
        }

        _bitmap = new WriteableBitmap(
            new PixelSize((int)pixels.Width, (int)pixels.Height),
            new Vector(96, 96),
            PixelFormats.Rgba8888,
            AlphaFormat.Premul);

        using (var fb = _bitmap.Lock())
        {
            Unsafe.CopyBlock(fb.Address.ToPointer(), Unsafe.AsPointer(ref pixels.Data[0]),
                (uint)pixels.Data.Length);
        }

        context.DrawImage(_bitmap, new Rect(Bounds.Size));
    }
}
