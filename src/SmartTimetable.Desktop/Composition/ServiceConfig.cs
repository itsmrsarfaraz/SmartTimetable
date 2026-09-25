using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using SmartTimetable.Application.Solving;
using SmartTimetable.Desktop.ViewModels;
using SmartTimetable.Infrastructure;
using SmartTimetable.Solver;

namespace SmartTimetable.Desktop.Composition;

/// <summary>Builds the application's dependency-injection container.</summary>
public static class ServiceConfig
{
    public static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SmartTimetable");
        Directory.CreateDirectory(dir);
        string dbPath = Path.Combine(dir, "smarttimetable.db");

        // Application + Infrastructure services (EF Core, security, licensing, use cases).
        services.AddInfrastructure(dbPath);

        // The OR-Tools solver lives in the Solver project; registered here so the
        // Infrastructure layer stays free of the OR-Tools dependency.
        services.AddSingleton<ITimetableSolver, CpSatTimetableSolver>();

        // Shared UI state.
        services.AddSingleton<AppState>();

        // Root + navigation.
        services.AddSingleton<RootViewModel>();
        services.AddSingleton<ShellViewModel>();

        // Screens.
        services.AddTransient<LicenseViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<AcademicStructureViewModel>();
        services.AddTransient<SubjectsViewModel>();
        services.AddTransient<RoomsViewModel>();
        services.AddTransient<PeriodsViewModel>();
        services.AddTransient<TeachersViewModel>();
        services.AddTransient<TeacherAvailabilityViewModel>();
        services.AddTransient<ClassesViewModel>();
        services.AddTransient<CombinedClassesViewModel>();
        services.AddTransient<ContradictorySubjectsViewModel>();
        services.AddTransient<SubjectPairingViewModel>();
        services.AddTransient<TeacherSubjectViewModel>();
        services.AddTransient<TeacherPeriodsViewModel>();
        services.AddTransient<GenerateViewModel>();
        services.AddTransient<TimetableViewModel>();

        return services.BuildServiceProvider();
    }
}
