using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class RemboursementDto
{
    [Required] public int ContratId { get; set; }
    public TypeRemboursement Type { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Montant { get; set; }
    [Required] public string Motif { get; set; } = "";
}
public class DecisionDto { public bool Valider { get; set; } public string? Commentaire { get; set; } }
public class PaiementRembDto { public ModePaiement Mode { get; set; } = ModePaiement.Especes; public string? Reference { get; set; } }

[ApiController, Route("api/remboursements"), Authorize(Roles = "Gerant,Remboursement")]
public class RemboursementsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Notifier _notif;
    public RemboursementsController(AppDbContext db, Notifier notif) { _db = db; _notif = notif; }
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> List(StatutRemboursement? statut) =>
        Ok(await _db.Remboursements.AsNoTracking().Where(r => statut == null || r.Statut == statut)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.ContratId, ClientNom = r.Contrat.Client.Nom, ChantierNom = r.Contrat.Chantier.Nom, r.Contrat.Lot,
                r.Type, r.Montant, r.Motif, r.Statut, r.CreatedAt, r.DateDecision, r.Commentaire, r.DatePaiement, r.Mode, r.RefPaiement
            }).Take(500).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(RemboursementDto dto)
    {
        var c = await _db.Contrats.Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == dto.ContratId);
        if (c is null) return NotFound(new { message = "Contrat introuvable." });

        var paye = await _db.Encaissements.Where(e => e.ContratId == c.Id && !e.Annule).SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var deja = await _db.Remboursements.Where(r => r.ContratId == c.Id && r.Statut != StatutRemboursement.Rejetee)
            .SumAsync(r => (decimal?)r.Montant) ?? 0m;
        var plafond = paye - deja;
        if (dto.Montant > plafond)
            return BadRequest(new { message = $"Montant supérieur au maximum remboursable ({plafond:N2} DA = payé {paye:N2} − déjà demandé {deja:N2})." });

        var r = new DemandeRemboursement { ContratId = c.Id, Type = dto.Type, Montant = dto.Montant, Motif = dto.Motif, CreatedBy = Uid };
        _db.Remboursements.Add(r);
        await _db.SaveChangesAsync();
        await _notif.GerantsAsync("REMBOURSEMENT",
            $"Demande de remboursement ({(dto.Type == TypeRemboursement.Desistement ? "désistement" : "trop-perçu")}) : {dto.Montant:N2} DA — {c.Client.Nom} — à valider", r.Id);
        return Ok(new { r.Id });
    }

    [HttpPost("{id:int}/decision"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Decider(int id, DecisionDto dto)
    {
        var r = await _db.Remboursements.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Statut != StatutRemboursement.Demandee) return BadRequest(new { message = "Cette demande a déjà été traitée." });
        r.Statut = dto.Valider ? StatutRemboursement.Validee : StatutRemboursement.Rejetee;
        r.DateDecision = DateTime.Now; r.DecidePar = Uid; r.Commentaire = dto.Commentaire;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/payer")]
    public async Task<IActionResult> Payer(int id, PaiementRembDto dto)
    {
        var r = await _db.Remboursements.FindAsync(id);
        if (r is null) return NotFound();
        if (r.Statut != StatutRemboursement.Validee) return BadRequest(new { message = "Seules les demandes validées par le gérant peuvent être payées." });
        if (dto.Mode == ModePaiement.Bancaire && string.IsNullOrWhiteSpace(dto.Reference))
            return BadRequest(new { message = "Référence obligatoire pour un paiement bancaire." });
        r.Statut = StatutRemboursement.Payee; r.DatePaiement = DateTime.Now; r.Mode = dto.Mode; r.RefPaiement = dto.Reference;
        await _db.SaveChangesAsync();
        await _notif.GerantsAsync("REMBOURSEMENT", $"Remboursement payé : {r.Montant:N2} DA (dossier #{r.Id})", r.Id);
        return NoContent();
    }
}
