using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BarTascaBackend.Hubs;

[Authorize]
public class QueueHub : Hub
{
    //TODO: Validar que el usuario tenga permisos para unirse a un grupo de ticket
    public Task JoinTicketGroup(int ticketId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");

    public Task LeaveTicketGroup(int ticketId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");

    [Authorize(Roles = "Admin,Worker")] // TODO: unificar en una policy "Staff"
    public Task JoinStaff() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "staff");

    public Task LeaveStaff() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff");
}
