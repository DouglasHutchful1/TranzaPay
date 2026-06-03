using Microsoft.EntityFrameworkCore;
using TranzaPay.Domain.Enums;
using TranzaPay.Infrastructure.Data;

namespace TranzaPay.Api.Services;

public sealed class PendingWithdrawalMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<PendingWithdrawalMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TranzaPayDbContext>();
            var pendingCount = await db.WithdrawalRequests.CountAsync(x => x.Status == WithdrawalStatus.Pending, stoppingToken);
            logger.LogInformation("Pending withdrawal requests: {PendingCount}", pendingCount);
        }
    }
}
