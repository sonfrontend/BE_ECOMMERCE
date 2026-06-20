using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BE_ECOMMERCE.Hubs;

public class ChatHub : Hub
{
    public async Task JoinUserGroup(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }

    public async Task LeaveUserGroup(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
    }
}
