using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Hubs;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.Security.Claims;

namespace PromoImmo.Api.Services;

/// <summary>Enregistre une notification pour chaque gérant et la pousse en temps réel.</summary>
public class Notifier
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IHubContext<NotificationHub> _hub;
    public Notifier(AppDbContext db, UserManager<AppUser> users, IHubContext<NotificationHub> hub)
    { _db = db; _users = users; _hub = hub; }

    public async Task GerantsAsync(string type, string message, int? refId = null)
    {
        var gerants = await _users.GetUsersInRoleAsync("Gerant");
        var notifs = gerants.Select(g => new Notification { DestinataireId = g.Id, Type = type, Message = message, ReferenceId = refId }).ToList();
        _db.Notifications.AddRange(notifs);
        await _db.SaveChangesAsync();
        foreach (var n in notifs)
            await _hub.Clients.User(n.DestinataireId).SendAsync("nouvelleNotification",
                new { n.Id, n.Type, n.Message, n.ReferenceId, n.Date, n.Lu });
    }

    /// <summary>Notifie les gérants sauf si le même message a déjà été envoyé dans la fenêtre donnée.</summary>
    public async Task<bool> GerantsUniqueAsync(string type, string message, TimeSpan fenetre)
    {
        var depuis = DateTime.UtcNow - fenetre;
        if (await _db.Notifications.AnyAsync(n => n.Type == type && n.Message == message && n.Date > depuis)) return false;
        await GerantsAsync(type, message);
        return true;
    }
}

public static class Acces
{
    /// <summary>Rôle propriétaire de chaque service (Divers → Achats).</summary>
    public static string Role(ServiceCode s) => s == ServiceCode.Divers ? "Achats" : s.ToString();
    public static bool Peut(ClaimsPrincipal u, ServiceCode s) => u.IsInRole("Gerant") || u.IsInRole(Role(s));
}
