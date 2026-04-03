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
    private RenderingContext? _renderingContext;

    public void SetRenderingContext(RenderingContext context)
    {
        _renderingContext = context;
    }

    public void MarkFrameReady()
    {
        InvalidateVisual();
    }

    public override unsafe void Render(DrawingContext context)
    {
        var pixels = _renderingContext?.ReadPixels();
        _renderingContext?.Present();
        if (pixels == null || pixels.Data.Length == 0)
        {
            context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));
            return;
        }

        if (pixels.Data.AsSpan().IndexOfAnyExcept((byte)0) == -1 && _bitmap != null)
        {
            context.DrawImage(_bitmap, new Rect(Bounds.Size));
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
