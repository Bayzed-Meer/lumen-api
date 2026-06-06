using System.Reflection;
using Lumen.Application.Services.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace Lumen.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(configuration => configuration.AddMaps(Assembly.GetExecutingAssembly()));
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
