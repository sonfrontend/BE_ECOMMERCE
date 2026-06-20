using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BE_ECOMMERCE.Hubs;

public class NotificationHub : Hub
{
    // Cấu hình Join User Group để khi gửi thông báo, chỉ gửi đến những kết nối thuộc UserId đó
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
    }
}
