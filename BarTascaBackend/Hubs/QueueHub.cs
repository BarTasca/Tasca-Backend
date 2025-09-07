using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using BarTasca.Data.Interfaces;

namespace BarTascaBackend.Hubs;

[Authorize]
public class QueueHub : Hub
{
    private readonly ITicketRepository _tickets;

    public QueueHub(ITicketRepository tickets)
    {
        _tickets = tickets;
    }

    public async Task JoinTicketGroup(string publicId, CancellationToken ct = default)
    {
        var user = Context.User;
        if (user is null) throw new Exception("Unauthorized");
        var ticket = await _tickets.GetByPublicIdAsync(publicId, ct);
        if (ticket is null) throw new HubException("NotFound");


        //staff
        var isStaff = user.IsInRole("Admin") || user.IsInRole("Worker");
        if (isStaff)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticket.Id}");
            return;
        }

        //Customer
        var claim = user.FindFirst("ticket_public_id");
        if (claim is null || claim.Value != publicId) throw new HubException("Forbidden");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket:{ticket.Id}");

    }

    public Task LeaveTicketGroup(int ticketId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket:{ticketId}");

    [Authorize(Policy = "Staff")]
    public Task JoinStaff() =>
        Groups.AddToGroupAsync(Context.ConnectionId, "staff");

    public Task LeaveStaff() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff");
}
