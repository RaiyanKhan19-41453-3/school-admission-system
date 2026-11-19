using Admissions.Models;
using Admissions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admissions.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize(Roles = AppRoles.Parent)]
public sealed class NotificationsController(INotificationService notifications) : ControllerBase
{
    string Nid => User.FindFirst("nid")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await notifications.GetMyAsync(Nid));

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkRead(string id)
    {
        await notifications.MarkReadAsync(id, Nid);
        return Ok(new { message = "Marked as read." });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAll()
    {
        var n = await notifications.MarkAllReadAsync(Nid);
        return Ok(new { message = $"{n} notifications marked as read." });
    }
}
