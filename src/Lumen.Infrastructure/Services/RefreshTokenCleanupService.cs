using Lumen.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lumen.Infrastructure.Services;

public sealed class RefreshTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);

            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                IRefreshTokenRepository repo = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
                await repo.DeleteExpiredAsync(stoppingToken).ConfigureAwait(false);
                logger.LogInformation("Expired refresh tokens deleted.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to delete expired refresh tokens.");
            }
        }
    }
}
