using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Lumen.API.Extensions;

public static class RateLimiterExtensions
{
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(opts =>
        {
            static RateLimitPartition<string> PerIpFixedWindow(HttpContext context) =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });

            opts.AddPolicy("forgot-password", PerIpFixedWindow);
            opts.AddPolicy("resend-reset-otp", PerIpFixedWindow);
            opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
