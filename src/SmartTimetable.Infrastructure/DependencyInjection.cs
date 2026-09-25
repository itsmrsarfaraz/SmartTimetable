using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Application.Services;
using SmartTimetable.Infrastructure.Licensing;
using SmartTimetable.Infrastructure.Persistence;
using SmartTimetable.Infrastructure.Security;
using SmartTimetable.Infrastructure.Solving;
using SmartTimetable.Licensing;

namespace SmartTimetable.Infrastructure;

/// <summary>Composition root for the Infrastructure + Application services.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registers EF Core (SQLite), the unit-of-work factory, security, licensing and
    /// the application use-case services. The concrete <c>ITimetableSolver</c> lives in
    /// the Solver project and is registered by the host (Desktop) so this layer stays
    /// free of the OR-Tools dependency.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dbPath)
    {
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // A fresh, self-owned unit of work per call. Contexts come from the factory so
        // they are NOT tracked by the DI container; callers dispose via `using`.
        services.AddSingleton<Func<IUnitOfWork>>(sp =>
        {
            var factory = sp.GetRequiredService<IDbContextFactory<AppDbContext>>();
            return () => new UnitOfWork(factory.CreateDbContext());
        });

        // Security + licensing (stateless / process-wide).
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IMachineFingerprintProvider>(_ => MachineFingerprintProvider.ForThisMachine());
        services.AddSingleton<LicenseStore>(_ => LicenseStore.ForCurrentUser());
        services.AddSingleton(sp => new LicenseValidator(
            VendorPublicKey.GetBytes(),
            sp.GetRequiredService<IMachineFingerprintProvider>()));
        services.AddSingleton<LicenseService>();
        services.AddSingleton<IAppLicenseService, AppLicenseService>();

        // Application use cases.
        services.AddTransient<ISolverInputBuilder, SolverInputBuilder>();
        services.AddTransient<IAuthService, AuthService>();
        services.AddTransient<ITimetableGenerationService, TimetableGenerationService>();

        return services;
    }

    /// <summary>
    /// Creates the database if it does not exist and seeds the demo dataset.
    /// Call once at application startup.
    /// </summary>
    public static void InitializeDatabase(this IServiceProvider provider)
    {
        var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        using var db = factory.CreateDbContext();
        db.Database.EnsureCreated();
        DatabaseSeeder.Seed(db);
    }
}
