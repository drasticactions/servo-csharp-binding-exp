using Avalonia.Threading;

namespace Servo.Sharp.Avalonia;

public static class ServoAppBuilderExtensions
{
    public static global::Avalonia.AppBuilder UseServo(
        this global::Avalonia.AppBuilder builder,
        string? resourcePath = null,
        ProtocolRegistry? protocolRegistry = null)
    {
        builder.AfterSetup(_ =>
        {
            var engine = new ServoEngine(resourcePath, protocolRegistry);
            engine.EventLoopWaker = () =>
                Dispatcher.UIThread.Post(() => engine.SpinEventLoop(), DispatcherPriority.Render);
            ServoLocator.Engine = engine;
        });

        return builder;
    }
}
