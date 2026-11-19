using Microsoft.AspNetCore.SignalR;

namespace PartsOrderTracker.Api.Hubs;

// Hosts the "orders" hub used by Azure SignalR (negotiate + routing).
// The UI calls Subscribe with its user id so it joins the group the worker publishes to.
public sealed class OrdersHub : Hub
{
    public Task Subscribe(string userId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, userId);
}
