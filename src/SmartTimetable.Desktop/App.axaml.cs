using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Core.Plugins;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using SmartTimetable.Desktop.Composition;
using SmartTimetable.Desktop.Diagnostics;
using SmartTimetable.Desktop.ViewModels;
using SmartTimetable.Desktop.Views;
using SmartTimetable.Infrastructure;

namespace SmartTimetable.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            try
            {
                // CommunityToolkit.Mvvm already validates; remove Avalonia's duplicate validator.
                DisableAvaloniaDataAnnotationValidation();

                var services = ServiceConfig.Build();
                services.InitializeDatabase();

                var root = services.GetRequiredService<RootViewModel>();
                desktop.MainWindow = new MainWindow { DataContext = root };
                root.Start();
            }
            catch (Exception ex)
            {
                // Startup failed before any window could appear. Record the real cause and
                // show it, rather than letting the process die with a bare 0xe0434352.
                CrashLogger.Write("Startup failed in OnFrameworkInitializationCompleted", ex);
                desktop.MainWindow = BuildStartupErrorWindow(ex);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var toRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        foreach (var plugin in toRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }

    /// <summary>
    /// A dependency-free window that surfaces a fatal startup exception so the user (or the
    /// developer) can read and copy it, instead of the app silently disappearing. Built in
    /// code because the normal view/DI pipeline is exactly what may have just failed.
    /// </summary>
    private static Window BuildStartupErrorWindow(Exception ex)
    {
        string details = CrashLogger.Format("Startup failed", ex);

        var title = new TextBlock
        {
            Text = "SmartTimetable couldn't start",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 6)
        };

        var subtitle = new TextBlock
        {
            Text = "The app hit an error while starting. The full details are below and were " +
                   "saved to:\n" + CrashLogger.LogPath,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 0, 0, 4)
        };

        var header = new StackPanel();
        header.Children.Add(title);
        header.Children.Add(subtitle);
        DockPanel.SetDock(header, Dock.Top);

        var box = new TextBox
        {
            Text = details,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 8)
        };
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);

        var copy = new Button { Content = "Copy details" };
        var close = new Button { Content = "Close", Margin = new Thickness(8, 0, 0, 0) };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 4, 0, 0)
        };
        buttons.Children.Add(copy);
        buttons.Children.Add(close);
        DockPanel.SetDock(buttons, Dock.Bottom);

        var layout = new DockPanel { Margin = new Thickness(16) };
        layout.Children.Add(header);   // top
        layout.Children.Add(buttons);  // bottom
        layout.Children.Add(box);      // fills the remaining space

        var window = new Window
        {
            Title = "SmartTimetable — startup error",
            Width = 720,
            Height = 540,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = layout
        };

        close.Click += (_, _) => window.Close();
        copy.Click += async (_, _) =>
        {
            var top = TopLevel.GetTopLevel(copy);
            if (top?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(details);
        };

        return window;
    }
}
