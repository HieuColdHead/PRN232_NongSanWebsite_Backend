using Microsoft.AspNetCore.SignalR;
using DAL.Entity;
using System.Security.Claims;

namespace BLL.Hubs;

public class AppHub : Hub
{
    private static readonly Dictionary<Guid, string> UserConnections = new();

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            UserConnections[userId] = Context.ConnectionId;
            
            // Join a group for their role for broadcasts
            var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.IsNullOrEmpty(role))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, role);
            }
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId != Guid.Empty)
        {
            UserConnections.Remove(userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    private Guid GetUserId()
    {
        var userIdClaim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            // Some JWTs use "sub" instead of NameIdentifier
            userIdClaim = Context.User?.FindFirst("sub");
        }
        
        return userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var guid) ? guid : Guid.Empty;
    }

    public static string? GetConnectionId(Guid userId)
    {
        return UserConnections.TryGetValue(userId, out var connectionId) ? connectionId : null;
    }
}
