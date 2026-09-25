using System;
using Avalonia;

namespace SmartTimetable.Desktop;

internal static class Program
{
    // Avalonia configuration. Do not remove the [STAThread] attribute.
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
