using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class FournisseurDto { [Required] public string Nom { get; set; } = ""; public string? Telephone { get; set; } public string? Adresse { get; set; } }
public class CommandeDto
{
    [Required] public int FournisseurId { get; set; }
    public int? ChantierId { get; set; }
    [Required] public string Objet { get; set; } = "";
    [Range(0, double.MaxValue)] public decimal Quantite { get; set; } = 1;
    public string Unite { get; set; } = "u";
    [Range(0, double.MaxValue)] public decimal Montant { get; set; }
    public DateTime? DateLivraisonPrevue { get; set; }
}
public class StatutCommandeDto { public StatutCommande Statut { get; set; } }
public class DecisionCommandeDto { public bool Accepter { get; set; } }

[ApiController, Route("api"), Authorize(Roles = "Gerant,Achats")]
public class AchatsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly Notifier _notif;
    public AchatsController(AppDbContext db, IConfiguration cfg, Notifier notif) { _db = db; _cfg = cfg; _notif = notif; }
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("fournisseurs")]
    public async Task<IActionResult> Fournisseurs() =>
        Ok(await _db.Fournisseurs.AsNoTracking().OrderBy(f => f.Nom).Select(f => new { f.Id, f.Nom, f.Telephone, f.Adresse }).ToListAsync());

    [HttpPost("fournisseurs")]
    public async Task<IActionResult> AddFournisseur(FournisseurDto dto)
    {
        var f = new Fournisseur { Nom = dto.Nom, Telephone = dto.Telephone, Adresse = dto.Adresse, CreatedBy = Uid };
        _db.Fournisseurs.Add(f);
        await _db.SaveChangesAsync();
        return Ok(new { f.Id });
    }

    [HttpPut("fournisseurs/{id:int}")]
    public async Task<IActionResult> UpdFournisseur(int id, FournisseurDto dto)
    {
        var f = await _db.Fournisseurs.FindAsync(id);
        if (f is null) return NotFound();
        f.Nom = dto.Nom; f.Telephone = dto.Telephone; f.Adresse = dto.Adresse;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("commandes")]
    public async Task<IActionResult> Commandes(StatutCommande? statut, int? chantierId, int? fournisseurId) =>
        Ok(await _db.Commandes.AsNoTracking()
            .Where(c => (statut == null || c.Statut == statut) && (chantierId == null || c.ChantierId == chantierId) &&
                        (fournisseurId == null || c.FournisseurId == fournisseurId))
            .OrderByDescending(c => c.DateCommande).ThenByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id, c.FournisseurId, FournisseurNom = c.Fournisseur.Nom, c.ChantierId,
                ChantierNom = c.Chantier != null ? c.Chantier.Nom : null,
                c.Objet, c.Quantite, c.Unite, c.Montant, c.Statut, c.DateCommande, c.DateLivraisonPrevue, c.DateLivraison
            }).Take(500).ToListAsync());

    /// <summary>Au-dessus du seuil (Achats:SeuilValidation), la commande attend l'accord du gérant.</summary>
    [HttpPost("commandes")]
    public async Task<IActionResult> AddCommande(CommandeDto dto)
    {
        if (!await _db.Fournisseurs.AnyAsync(f => f.Id == dto.FournisseurId)) return BadRequest(new { message = "Fournisseur introuvable." });
        var seuil = _cfg.GetValue("Achats:SeuilValidation", 500000m);
        var c = new CommandeAchat { FournisseurId = dto.FournisseurId, ChantierId = dto.ChantierId, Objet = dto.Objet,
            Quantite = dto.Quantite, Unite = dto.Unite, Montant = dto.Montant, DateLivraisonPrevue = dto.DateLivraisonPrevue, CreatedBy = Uid };
        var aValider = dto.Montant > seuil && !User.IsInRole("Gerant");
        if (aValider) c.Statut = StatutCommande.AValider;
        _db.Commandes.Add(c);
        await _db.SaveChangesAsync();
        if (aValider)
            await _notif.GerantsAsync("ACHAT", $"Commande à valider : {dto.Objet} — {dto.Montant:N2} DA (seuil {seuil:N0} DA)", c.Id);
        return Ok(new { c.Id, aValider });
    }

    [HttpPut("commandes/{id:int}/statut")]
    public async Task<IActionResult> SetStatut(int id, StatutCommandeDto dto)
    {
        var c = await _db.Commandes.FindAsync(id);
        if (c is null) return NotFound();
        if (!User.IsInRole("Gerant") && (c.Statut == StatutCommande.AValider || dto.Statut == StatutCommande.AValider))
            return BadRequest(new { message = "Cette commande doit d'abord être validée par le gérant." });
        c.Statut = dto.Statut;
        if (dto.Statut == StatutCommande.Livree && c.DateLivraison == null) c.DateLivraison = DateTime.Today;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("commandes/{id:int}/decision"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Decider(int id, DecisionCommandeDto dto)
    {
        var c = await _db.Commandes.FindAsync(id);
        if (c is null) return NotFound();
        if (c.Statut != StatutCommande.AValider) return BadRequest(new { message = "Cette commande n'est pas en attente de validation." });
        c.Statut = dto.Accepter ? StatutCommande.Demandee : StatutCommande.Annulee;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
