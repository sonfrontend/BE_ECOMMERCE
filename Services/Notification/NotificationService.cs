using BE_ECOMMERCE.Data;
using BE_ECOMMERCE.Entities.System;
using BE_ECOMMERCE.Hubs;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace BE_ECOMMERCE.Services.Notification;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ApplicationDbContext context, IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(Guid userId, string title, string message, string type, string? relatedId = null)
    {
        var notification = new BE_ECOMMERCE.Entities.System.Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            RelatedId = relatedId,
            IsRead = false,
            CreatedAt = DateTime.Now,
            IsActived = true
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Gửi qua SignalR tới những client đang kết nối thuộc UserId này
        await _hubContext.Clients.Group(userId.ToString()).SendAsync("ReceiveNotification", new
        {
            id = notification.Id,
            title = notification.Title,
            message = notification.Message,
            type = notification.Type,
            relatedId = notification.RelatedId,
            isRead = notification.IsRead,
            createdAt = notification.CreatedAt
        });
    }
}
