using Admissions.Data;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

/// <summary>Moves past-due HELD rows to EXPIRED so seat counts stay accurate.</summary>
public sealed class ExpiredHoldSweeper(IServiceScopeFactory scopes, ILogger<ExpiredHoldSweeper> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
                var n = await db.Database.ExecuteSqlAsync($"""
                    UPDATE "Holds" SET "Status" = 'EXPIRED', "UpdatedAt" = now()
                    WHERE "Status" = 'HELD' AND "ExpiresAt" <= now()
                    """, stoppingToken);
                if (n > 0) logger.LogInformation("Expired {Count} holds", n);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Expiry sweep failed"); }
        }
    }
}
