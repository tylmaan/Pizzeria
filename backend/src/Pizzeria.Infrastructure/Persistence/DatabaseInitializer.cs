using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Domain.Identity;
using Pizzeria.Domain.Tenancy;
using Pizzeria.Infrastructure.Identity;

namespace Pizzeria.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending migrations at startup. Meant for development and tests, not for production.</summary>
    public bool MigrateOnStartup { get; set; }
}

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Bootstrap PlatformAdmin account. Created once if both values are set (any environment).</summary>
    public string? PlatformAdminEmail { get; set; }

    public string? PlatformAdminPassword { get; set; }

    /// <summary>Development only: demo company with one location and one user per role.</summary>
    public bool DemoData { get; set; }

    /// <summary>Password for all demo users. Keep it in user-secrets, never in the repo.</summary>
    public string? DemoPassword { get; set; }
}

public static partial class DatabaseInitializer
{
    public const string DemoCompanySlug = "demo";

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var db = provider.GetRequiredService<AppDbContext>();

        if (provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.MigrateOnStartup)
        {
            await db.Database.MigrateAsync(ct).ConfigureAwait(false);
        }

        await SeedRolesAsync(provider.GetRequiredService<RoleManager<AppRole>>()).ConfigureAwait(false);

        var seed = provider.GetRequiredService<IOptions<SeedOptions>>().Value;
        var accounts = provider.GetRequiredService<IUserAccountService>();
        var userManager = provider.GetRequiredService<UserManager<AppUser>>();

        if (!string.IsNullOrWhiteSpace(seed.PlatformAdminEmail) && !string.IsNullOrWhiteSpace(seed.PlatformAdminPassword)
            && await userManager.FindByNameAsync(seed.PlatformAdminEmail).ConfigureAwait(false) is null)
        {
            await accounts.CreateAsync(
                new NewUserAccount(null, seed.PlatformAdminEmail, seed.PlatformAdminPassword, "Platform admin", Roles.PlatformAdmin, []),
                ct).ConfigureAwait(false);
            LogPlatformAdminCreated(logger, seed.PlatformAdminEmail);
        }

        if (seed.DemoData)
        {
            if (string.IsNullOrWhiteSpace(seed.DemoPassword))
            {
                LogDemoPasswordMissing(logger);
            }
            else
            {
                await SeedDemoCompanyAsync(provider, db, accounts, seed.DemoPassword, ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task SeedRolesAsync(RoleManager<AppRole> roleManager)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role).ConfigureAwait(false))
            {
                await roleManager.CreateAsync(new AppRole(role)).ConfigureAwait(false);
            }
        }
    }

    private static async Task SeedDemoCompanyAsync(
        IServiceProvider provider,
        AppDbContext db,
        IUserAccountService accounts,
        string password,
        CancellationToken ct)
    {
        // Existence check spans all tenants: no tenant is set during startup.
        if (await db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Slug == DemoCompanySlug, ct).ConfigureAwait(false))
        {
            return;
        }

        var now = provider.GetRequiredService<TimeProvider>().GetUtcNow();
        var company = Company.Create("Demo Pizza", DemoCompanySlug, now);
        db.Companies.Add(company);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        provider.GetRequiredService<TenantContext>().SetSystemForCompany(company.Id);
        var location = Location.Create(company.Id, "Demo Pizza Centrum", new Address("ul. Lipowa 1", "15-001", "Białystok"), null, now);
        var geocoded = await provider.GetRequiredService<IGeocodingService>()
            .GeocodeAsync(new AddressQuery("ul. Lipowa 1", "15-001", "Białystok"), ct).ConfigureAwait(false);
        location.SetPosition(geocoded?.Point);
        db.Locations.Add(location);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        (string Email, string Name, string Role, bool AtLocation)[] users =
        [
            ("owner@demo.local", "Właściciel", Roles.CompanyAdmin, false),
            ("manager@demo.local", "Kierownik", Roles.LocationManager, true),
            ("staff@demo.local", "Obsługa", Roles.Staff, true),
            ("kitchen@demo.local", "Kuchnia", Roles.Kitchen, true),
            ("driver@demo.local", "Kierowca", Roles.Driver, true),
        ];
        foreach (var (email, name, role, atLocation) in users)
        {
            await accounts.CreateAsync(
                new NewUserAccount(company.Id, email, password, name, role, atLocation ? [location.Id] : []),
                ct).ConfigureAwait(false);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Created PlatformAdmin account {Email}")]
    private static partial void LogPlatformAdminCreated(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed:DemoData is enabled but Seed:DemoPassword is not set; skipping demo data")]
    private static partial void LogDemoPasswordMissing(ILogger logger);
}
