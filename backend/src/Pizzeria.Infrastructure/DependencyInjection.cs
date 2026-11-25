using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Application.Abstractions;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;
using Pizzeria.Infrastructure.Identity;
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

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));

        services.AddMemoryCache();
        services.AddScoped<ICompanyResolver, CompanyResolver>();
        services.AddScoped<IUserAccountService, UserAccountService>();

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
