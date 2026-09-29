using System;
using Avalonia;
using SmartTimetable.Desktop.Diagnostics;

namespace SmartTimetable.Desktop;

internal static class Program
{
    // Avalonia configuration. Do not remove the [STAThread] attribute.
    [STAThread]
    public static void Main(string[] args)
    {
        // Install first so anything that throws during Avalonia bootstrap is still recorded.
        CrashLogger.Install();
        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            // A crash this early means no window ever appeared; log it so the exit code
            // 0xe0434352 ("a .NET exception") turns into a readable cause.
            CrashLogger.Write("Fatal exception in Program.Main", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
