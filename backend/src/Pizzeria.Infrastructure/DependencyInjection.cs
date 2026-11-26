using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Delivery;
using Pizzeria.Application.Orders;
using Pizzeria.Application.Payments;
using Pizzeria.Application.Reports;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Infrastructure.Geocoding;
using Pizzeria.Infrastructure.Identity;
using Pizzeria.Infrastructure.Orders;
using Pizzeria.Infrastructure.Payments;
using Pizzeria.Infrastructure.Reports;
using Pizzeria.Infrastructure.Persistence;
using Pizzeria.Infrastructure.Tenancy;

namespace Pizzeria.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Pizzeria";

    /// <summary>Registers persistence and Identity stores. Returns the Identity builder for host-specific additions.</summary>
    public static IdentityBuilder AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured (use user-secrets or ConnectionStrings__{ConnectionStringName}).");

        // Single query on purpose: menus are small and one query gives a consistent snapshot of an aggregate.
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql
                .UseNetTopologySuite()
                .UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddMemoryCache();
        services.AddScoped<ICompanyResolver, CompanyResolver>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IOrderNumberGenerator, OrderNumberGenerator>();
        services.AddScoped<IReportQueries, ReportQueries>();

        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
        services.AddSingleton<FakePaymentProvider>();
        services.AddSingleton<IPaymentProvider>(sp => sp.GetRequiredService<FakePaymentProvider>());
        services.AddSingleton<IPaymentProviderResolver, ConfiguredPaymentProviderResolver>();

        services.Configure<GeocodingOptions>(configuration.GetSection(GeocodingOptions.SectionName));
        // The real geocoding provider is an open decision (CLAUDE.md); the fake is the only implementation so far.
        services.AddSingleton<IGeocodingService, FakeGeocodingService>();

        return services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();
    }
}
