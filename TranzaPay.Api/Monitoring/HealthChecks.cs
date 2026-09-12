using Microsoft.Extensions.Diagnostics.HealthChecks;
using TranzaPay.Infrastructure.Data;

namespace TranzaPay.Api.Monitoring;

public sealed class DatabaseHealthCheck(TranzaPayDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database is reachable.")
                : HealthCheckResult.Unhealthy("Database is unavailable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connectivity check failed.", ex);
        }
    }
}

public static class HealthChecks
{
    public static void AddTranzaPayHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["db", "ready"]);
    }
}
