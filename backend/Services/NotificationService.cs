using Admissions.Data;
using Admissions.Dtos;
using Admissions.Models;
using Admissions.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Admissions.Services;

public interface INotificationService
{
    Task<NotificationListResponse> GetMyAsync(string nid);
    Task<int> MarkReadAsync(string id, string nid);
    Task<int> MarkAllReadAsync(string nid);
    Task NotifyAsync(string studentId, string type, string title, string message, string? holdId = null);
    Task CreateExpiryWarningsAsync();
}

public sealed class NotificationService(
    AdmissionDbContext db,
    IStudentRepository students) : INotificationService
{
    static string NewId(string p) => p + Guid.NewGuid().ToString("N")[0..12].ToLowerInvariant();

    static NotificationResponse ToDto(Notification n) => new(
        n.Id, n.Type, n.Title, n.Message, n.HoldId, n.IsRead, n.CreatedAt);

    async Task<string> ResolveOwnerAsync(string nid)
        => (await students.GetByNidAsync(nid)
            ?? throw new NotFoundException("Student not found.")).Id;

    public async Task<NotificationListResponse> GetMyAsync(string nid)
    {
        var owner = await ResolveOwnerAsync(nid);
        var list = await db.Notifications.AsNoTracking()
            .Where(n => n.StudentId == owner)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
        return new NotificationListResponse(
            list.Select(ToDto).ToList(),
            list.Count(n => !n.IsRead));
    }

    public async Task<int> MarkReadAsync(string id, string nid)
    {
        var owner = await ResolveOwnerAsync(nid);
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.StudentId == owner)
                ?? throw new NotFoundException("Notification not found.");
        if (!n.IsRead) { n.IsRead = true; await db.SaveChangesAsync(); }
        return 1;
    }

    public async Task<int> MarkAllReadAsync(string nid)
    {
        var owner = await ResolveOwnerAsync(nid);
        return await db.Notifications
            .Where(n => n.StudentId == owner && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }

    public async Task NotifyAsync(string studentId, string type, string title, string message, string? holdId = null)
    {
        db.Notifications.Add(new Notification
        {
            Id = NewId("n_"), StudentId = studentId, Type = type,
            Title = title, Message = message, HoldId = holdId
        });
        await db.SaveChangesAsync();
    }

    /// <summary>Called by the sweeper: warn about holds expiring in the next 2 minutes.</summary>
    public async Task CreateExpiryWarningsAsync()
    {
        var candidates = await db.Holds
            .FromSqlInterpolated($"""
                SELECT * FROM "Holds"
                WHERE "Status" = 'HELD'
                  AND "ExpiresAt" > now()
                  AND "ExpiresAt" <= now() + interval '2 minutes'
                """)
            .Select(h => new { h.Id, h.StudentId })
            .ToListAsync();
        foreach (var h in candidates)
        {
            var already = await db.Notifications.AnyAsync(n =>
                n.HoldId == h.Id && n.Type == NotificationType.ExpiryWarning);
            if (already) continue;
            await NotifyAsync(h.StudentId, NotificationType.ExpiryWarning,
                "Application expiring soon",
                "Your seat hold expires soon. Pay the admission fee to confirm.",
                h.Id);
        }
    }
}
