using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using SmartTimetable.Desktop.Composition;
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
            // CommunityToolkit.Mvvm already validates; remove Avalonia's duplicate validator.
            DisableAvaloniaDataAnnotationValidation();

            var services = ServiceConfig.Build();
            services.InitializeDatabase();

            var root = services.GetRequiredService<RootViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = root };
            root.Start();
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
}
