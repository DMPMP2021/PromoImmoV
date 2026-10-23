using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Dtos;
using PromoImmo.Api.Hubs;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class EncaissementsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IHubContext<NotificationHub> _hub;

    public EncaissementsController(AppDbContext db, UserManager<AppUser> users,
        IHubContext<NotificationHub> hub)
    { _db = db; _users = users; _hub = hub; }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Encaisser une tranche ----------
    [HttpPost("encaissements")]
    [Authorize(Roles = "Recette")]
    public async Task<IActionResult> Encaisser(EncaissementCreateDto dto)
    {
        if (dto.Mode == ModePaiement.Bancaire && string.IsNullOrWhiteSpace(dto.RefBancaire))
            return BadRequest("La référence bancaire est obligatoire pour un paiement bancaire.");

        var contrat = await _db.Contrats
            .Include(c => c.Client).Include(c => c.Chantier)
            .FirstOrDefaultAsync(c => c.Id == dto.ContratId);
        if (contrat is null) return NotFound("Contrat introuvable.");

        await using var tx = await _db.Database.BeginTransactionAsync();

        var dejaPaye = await _db.Encaissements
            .Where(e => e.ContratId == contrat.Id && !e.Annule)
            .SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var reste = contrat.PrixTotal - dejaPaye;

        if (dto.Montant > reste)
            return BadRequest($"Le montant dépasse le solde restant ({reste:N2} DA).");

        var enc = new Encaissement
        {
            ContratId = contrat.Id,
            EcheanceId = dto.EcheanceId,
            Montant = dto.Montant,
            Mode = dto.Mode,
            RefBancaire = dto.RefBancaire,
            AgentId = UserId,
            CreatedBy = UserId,
            NumeroRecu = Guid.NewGuid().ToString("N") // provisoire, remplacé ci-dessous
        };
        _db.Encaissements.Add(enc);
        await _db.SaveChangesAsync();

        enc.NumeroRecu = $"REC-{DateTime.UtcNow:yyyy}-{enc.Id:D6}";

        var agent = await _users.FindByIdAsync(UserId);
        var message = $"{dto.Montant:N2} DA encaissé ({(dto.Mode == ModePaiement.Especes ? "espèces" : "bancaire")}) " +
                      $"- {contrat.Client.Nom} - {contrat.Chantier.Nom} - par {agent?.NomComplet}";

        var gerants = await _users.GetUsersInRoleAsync("Gerant");
        var notifs = gerants.Select(g => new Notification
        {
            DestinataireId = g.Id,
            Type = "ENCAISSEMENT",
            Message = message,
            ReferenceId = enc.Id
        }).ToList();
        _db.Notifications.AddRange(notifs);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        // Push temps réel après commit : on ne notifie jamais un encaissement annulé par un rollback
        foreach (var n in notifs)
            await _hub.Clients.User(n.DestinataireId).SendAsync("nouvelleNotification", new
            { n.Id, n.Type, n.Message, n.ReferenceId, n.Date, n.Lu });

        return CreatedAtAction(nameof(Historique), new { clientId = contrat.ClientId },
            new { enc.Id, enc.NumeroRecu, Reste = reste - dto.Montant });
    }

    // ---------- Annulation par écriture inverse ----------
    [HttpPost("encaissements/{id:int}/annuler")]
    [Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Annuler(int id, [FromBody] AnnulationDto dto)
    {
        var enc = await _db.Encaissements.FindAsync(id);
        if (enc is null) return NotFound();
        if (enc.Annule) return BadRequest("Déjà annulé.");

        enc.Annule = true;
        _db.Encaissements.Add(new Encaissement
        {
            ContratId = enc.ContratId,
            Montant = -enc.Montant,
            Mode = enc.Mode,
            RefBancaire = $"Annulation {enc.NumeroRecu} : {dto.Motif}",
            NumeroRecu = $"ANN-{enc.NumeroRecu}",
            AgentId = UserId,
            CreatedBy = UserId,
            Annule = true,
            EncaissementOrigineId = enc.Id
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Historique d'un client (gérant) ----------
    [HttpGet("clients/{clientId:int}/historique")]
    [Authorize(Roles = "Gerant")]
    public async Task<ActionResult<HistoriqueClientDto>> Historique(
        int clientId, DateTime? du = null, DateTime? au = null)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null) return NotFound();

        var contrats = await _db.Contrats.AsNoTracking()
            .Where(c => c.ClientId == clientId)
            .Select(c => new
            {
                c.Id,
                Chantier = c.Chantier.Nom,
                c.Lot,
                c.PrixTotal,
                Encs = c.Encaissements
                    .Where(e => (du == null || e.DateEncaissement >= du) &&
                                (au == null || e.DateEncaissement <= au))
                    .OrderByDescending(e => e.DateEncaissement)
                    .Select(e => new { e.Id, e.NumeroRecu, e.Montant, e.Mode, e.RefBancaire,
                                       e.DateEncaissement, e.Annule, e.AgentId })
                    .ToList(),
                Paye = c.Encaissements.Where(e => !e.Annule).Sum(e => (decimal?)e.Montant) ?? 0m
            }).ToListAsync();

        var agents = await _users.Users.AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.NomComplet);

        return new HistoriqueClientDto(client.Id, client.Nom,
            contrats.Select(c => new ContratHistoriqueDto(c.Id, c.Chantier, c.Lot, c.PrixTotal,
                c.Paye, c.PrixTotal - c.Paye,
                c.Encs.Select(e => new EncaissementDto(e.Id, e.NumeroRecu, e.Montant,
                    e.Mode.ToString(), e.RefBancaire, e.DateEncaissement, e.Annule,
                    agents.GetValueOrDefault(e.AgentId, "?"))).ToList())).ToList());
    }

    // ---------- Notifications ----------
    [HttpGet("notifications")]
    public async Task<IActionResult> MesNotifications(bool nonLuesSeulement = false) =>
        Ok(await _db.Notifications.AsNoTracking()
            .Where(n => n.DestinataireId == UserId && (!nonLuesSeulement || !n.Lu))
            .OrderByDescending(n => n.Date).Take(100).ToListAsync());

    [HttpPut("notifications/{id:int}/lu")]
    public async Task<IActionResult> MarquerLu(int id)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.DestinataireId == UserId);
        if (n is null) return NotFound();
        n.Lu = true;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
