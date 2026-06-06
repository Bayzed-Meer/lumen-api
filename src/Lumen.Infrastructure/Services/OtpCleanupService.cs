using Lumen.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lumen.Infrastructure.Services;

public sealed class OtpCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<OtpCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken).ConfigureAwait(false);

            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IOtpRepository repo = scope.ServiceProvider.GetRequiredService<IOtpRepository>();
                await repo.DeleteOlderThanAsync(DateTimeOffset.UtcNow.AddHours(-24), stoppingToken).ConfigureAwait(false);
                logger.LogInformation("Old OTP records deleted.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to delete old OTP records.");
            }
        }
    }
}
