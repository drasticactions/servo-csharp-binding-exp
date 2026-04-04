namespace Servo.Sharp;

public enum GamepadHapticEffectType : byte
{
    Play = 0,
    Stop = 1,
}

public sealed class GamepadHapticEffectEventArgs : EventArgs
{
    private readonly nuint _handle;
    private bool _responded;

    public int GamepadIndex { get; }
    public GamepadHapticEffectType EffectType { get; }

    internal GamepadHapticEffectEventArgs(int gamepadIndex, GamepadHapticEffectType effectType, nuint handle)
    {
        GamepadIndex = gamepadIndex;
        EffectType = effectType;
        _handle = handle;
    }

    /// <summary>
    /// Report that the haptic effect completed successfully.
    /// </summary>
    public void Succeeded()
    {
        if (_responded) return;
        _responded = true;
        ServoNative.gamepad_haptic_effect_succeeded(_handle);
    }

    /// <summary>
    /// Report that the haptic effect failed.
    /// </summary>
    public void Failed()
    {
        if (_responded) return;
        _responded = true;
        ServoNative.gamepad_haptic_effect_failed(_handle);
    }
}
