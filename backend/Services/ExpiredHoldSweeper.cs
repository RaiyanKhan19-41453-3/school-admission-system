using Admissions.Data;
using Admissions.Models;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

/// <summary>Moves past-due HELD rows to EXPIRED and sends notifications.
/// All time comparisons use the DATABASE clock (now()) so multiple app instances agree.</summary>
public sealed class ExpiredHoldSweeper(
    IServiceScopeFactory scopes,
    ILogger<ExpiredHoldSweeper> logger) : BackgroundService
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
                var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

                // Find past-due holds using DB time, and expire them in the same statement set.
                var expired = await db.Holds
                    .FromSqlInterpolated($"""
                        SELECT * FROM "Holds"
                        WHERE "Status" = 'HELD' AND "ExpiresAt" <= now()
                        """)
                    .Select(h => new { h.Id, h.StudentId, h.ClassId })
                    .ToListAsync(stoppingToken);

                if (expired.Count > 0)
                {
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE "Holds" SET "Status" = 'EXPIRED', "UpdatedAt" = now()
                        WHERE "Status" = 'HELD' AND "ExpiresAt" <= now()
                        """, stoppingToken);
                    // Restore each expired seat to the atomic counter
                    foreach (var h in expired)
                        await db.Database.ExecuteSqlInterpolatedAsync($"""
                            UPDATE "Classes" SET "SeatsRemaining" = "SeatsRemaining" + 1
                            WHERE "Id" = {h.ClassId}
                            """, stoppingToken);
                    foreach (var h in expired)
                        await notifications.NotifyAsync(h.StudentId, NotificationType.Expired,
                            "Application expired",
                            "Your seat hold expired and the seat has been released.", h.Id);
                    logger.LogInformation("Expired {Count} holds", expired.Count);
                }

                // Warn about holds expiring soon (also DB-clock based)
                await notifications.CreateExpiryWarningsAsync();
            }
            catch (Exception ex) { logger.LogWarning(ex, "Expiry sweep failed"); }
        }
    }
}
