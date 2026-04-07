using Avalonia;
using System;
using Servo.Sharp.Demo.Core;

namespace Servo.Sharp.Demo;

class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseServoDefaults()
            .LogToTrace();
    }
}
