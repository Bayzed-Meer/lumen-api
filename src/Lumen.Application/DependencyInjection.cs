using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Lumen.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAutoMapper(configuration => configuration.AddMaps(Assembly.GetExecutingAssembly()));

        return services;
    }
}
