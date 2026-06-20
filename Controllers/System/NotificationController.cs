using BE_ECOMMERCE.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using BE_ECOMMERCE.Services.Notification;

namespace BE_ECOMMERCE.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;

    public NotificationController(ApplicationDbContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    [HttpPost("notify-admin")]
    public async Task<IActionResult> NotifyAdmin([FromBody] NotifyAdminRequest request)
    {
        var userNameOrId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Khách";
        
        // Find admins
        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Admin");
        if (adminRole == null) return Ok(); // No admin role found
        
        var adminIds = await _context.UserRoles
            .Where(ur => ur.RoleId == adminRole.RoleId)
            .Select(ur => ur.UserId)
            .ToListAsync();
            
        string title = BE_ECOMMERCE.Constants.AdminNotificationMessages.GetTitle(request.ActionCode);
        string message = BE_ECOMMERCE.Constants.AdminNotificationMessages.GetMessage(request.ActionCode, request.Details);
        
        foreach(var adminId in adminIds)
        {
            await _notificationService.SendNotificationAsync(adminId, title, message, "System");
        }
        
        return Ok(new { success = true });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        return Ok(notifications);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification == null) return NotFound();

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Marked as read" });
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            return Unauthorized("User not found");

        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach(var notif in notifications)
        {
            notif.IsRead = true;
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "All marked as read" });
    }
}

public class NotifyAdminRequest
{
    public string ActionCode { get; set; }
    public string Details { get; set; }
}
