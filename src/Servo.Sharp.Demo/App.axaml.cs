using Avalonia.Controls.ApplicationLifetimes;

namespace Servo.Sharp.Demo;

public partial class App : Servo.Sharp.Demo.Core.App
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = global::Avalonia.Controls.ShutdownMode.OnLastWindowClose;
            desktop.MainWindow = new MainWindow();
        }
        else
        {
            base.OnFrameworkInitializationCompleted();
        }
    }
}
