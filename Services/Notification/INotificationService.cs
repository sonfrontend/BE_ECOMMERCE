using BE_ECOMMERCE.Entities.System;
using System.Threading.Tasks;

namespace BE_ECOMMERCE.Services.Notification;

public interface INotificationService
{
    Task SendNotificationAsync(Guid userId, string title, string message, string type, string? relatedId = null);
}
