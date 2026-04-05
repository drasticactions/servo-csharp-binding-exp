using System.IO;
using Avalonia;
using System;
using Servo.Sharp;
using Servo.Sharp.Avalonia;
using Servo.Sharp.Protocols;

namespace Servo.Sharp.Demo;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        var registry = CreateProtocolRegistry();
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseServo(protocolRegistry: registry)
            .WithInterFont()
            .LogToTrace();
    }

    private static ProtocolRegistry CreateProtocolRegistry()
    {
        var resourceDir = Path.Combine(AppContext.BaseDirectory, "resources", "resource_protocol");
        var registry = new ProtocolRegistry();

        var resourceHandler = new ResourceProtocolHandler(resourceDir);
        registry.Register("resource", resourceHandler);
        registry.Register("servo", new ServoProtocolHandler(resourceHandler));
        registry.Register("urlinfo", new UrlInfoProtocolHandler());

        return registry;
    }
}
