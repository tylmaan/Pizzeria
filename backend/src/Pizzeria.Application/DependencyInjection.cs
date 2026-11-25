using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Pizzeria.Application.Locations;
using Pizzeria.Application.Menu;
using Pizzeria.Application.Platform;
using Pizzeria.Application.Tenancy;
using Pizzeria.Application.Users;

namespace Pizzeria.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<CompanyService>();
        services.AddScoped<LocationService>();
        services.AddScoped<MenuService>();
        services.AddScoped<UserService>();

        return services;
    }
}
