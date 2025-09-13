using BarTasca.Data.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BarTascaBackend.Hubs;

[Authorize]
public class QueueHub : Hub
{
    private readonly ITicketRepository _tickets;
    private readonly ILogger<QueueHub> _logger;

    public QueueHub(ITicketRepository tickets, ILogger<QueueHub> logger)
    {
        _tickets = tickets;
        _logger = logger;
    }

    public async Task JoinTicketGroup(string publicId)
    {
        var ct = Context.ConnectionAborted;

        var user = Context.User;
        if (user is null) throw new Exception("Unauthorized");

        _logger.LogInformation("JoinTicketGroup: pid={Pid}, claim={Claim}",
             publicId,
             user.FindFirst("ticket_public_id")?.Value);

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
