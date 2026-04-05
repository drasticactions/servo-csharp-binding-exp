using Avalonia;
using Avalonia.Input.TextInput;

namespace Servo.Sharp.Avalonia;

internal class ServoTextInputMethodClient : TextInputMethodClient
{
    private readonly ServoWebViewControl _control;
    private TextSelection _selection;
    private bool _composing;
    private Rect _cursorRect = new(0, 0, 1, 16);

    public ServoTextInputMethodClient(ServoWebViewControl control)
    {
        _control = control;
    }

    public override Visual TextViewVisual => _control;

    public override bool SupportsPreedit => true;

    public override bool SupportsSurroundingText => false;

    public override string SurroundingText => "";

    public override Rect CursorRectangle => _cursorRect;

    public override TextSelection Selection
    {
        get => _selection;
        set => _selection = value;
    }

    public void UpdateCursorRect(double x, double y, double width, double height)
    {
        _cursorRect = new Rect(x, y, Math.Max(1, width), Math.Max(1, height));
        RaiseCursorRectangleChanged();
    }

    public override void SetPreeditText(string? preeditText, int? cursorPos)
    {
        var wv = _control.WebView;
        if (wv == null) return;

        if (!_composing && !string.IsNullOrEmpty(preeditText))
        {
            // First preedit text — start composition
            _composing = true;
            _control.NotifyImeComposing(true);
            wv.SendImeComposition(CompositionState.Start, "");
        }

        if (_composing)
        {
            if (string.IsNullOrEmpty(preeditText))
            {
                // Preedit cleared without commit — composition was cancelled
                _composing = false;
                _control.NotifyImeComposing(false);
                wv.SendImeDismissed();
            }
            else
            {
                wv.SendImeComposition(CompositionState.Update, preeditText);
            }
        }
    }

    public void NotifyCompositionEnd(string committedText)
    {
        _composing = false;
        _control.WebView?.SendImeComposition(CompositionState.End, committedText);
    }
}
