using Avalonia;
using System;
using Servo.Sharp.Avalonia;

namespace Servo.Sharp.Demo;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseServo()
            .WithInterFont()
            .LogToTrace();
}
