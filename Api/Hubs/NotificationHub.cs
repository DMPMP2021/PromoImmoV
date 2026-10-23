using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PromoImmo.Api.Hubs;

[Authorize]
public class NotificationHub : Hub { }
