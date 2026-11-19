using Admissions.Data;
using Admissions.Models;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

/// <summary>Moves past-due HELD rows to EXPIRED and sends notifications.</summary>
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

                // Expire past-due holds and notify their owners
                var expired = await db.Holds
                    .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= DateTime.UtcNow)
                    .Select(h => new { h.Id, h.StudentId })
                    .ToListAsync(stoppingToken);
                if (expired.Count > 0)
                {
                    await db.Holds.Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= DateTime.UtcNow)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(h => h.Status, HoldStatus.Expired)
                            .SetProperty(h => h.UpdatedAt, DateTime.UtcNow), stoppingToken);
                    foreach (var h in expired)
                        await notifications.NotifyAsync(h.StudentId, NotificationType.Expired,
                            "Application expired",
                            "Your seat hold expired and the seat has been released.", h.Id);
                    logger.LogInformation("Expired {Count} holds", expired.Count);
                }

                // Warn about holds expiring soon
                await notifications.CreateExpiryWarningsAsync();
            }
            catch (Exception ex) { logger.LogWarning(ex, "Expiry sweep failed"); }
        }
    }
}
