using Microsoft.AspNetCore.SignalR;

namespace BarTascaBackend.Hubs;

public class QueueHub : Hub
{
    public Task JoinTicketGroup(int ticketId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");

    public Task LeaveTicketGroup(int ticketId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");

    public Task JoinStaff() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "staff");

    public Task LeaveStaff() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff");
}
