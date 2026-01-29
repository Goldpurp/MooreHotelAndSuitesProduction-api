using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace MooreHotels.Infrastructure.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Staff and Admins join a specific group for broadcast alerts
        if (Context.User?.IsInRole("Admin") == true || Context.User?.IsInRole("Manager") == true || Context.User?.IsInRole("Staff") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "StaffGroup");
        }
        await base.OnConnectedAsync();
    }
}