using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BarTascaBackend.Hubs
{
    [AllowAnonymous]
    public class PublicQueueHub : Hub
    {
        public const string PublicQueueGroup = "public-queue";

        public override async Task OnConnectedAsync()
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, PublicQueueGroup);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, PublicQueueGroup);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
