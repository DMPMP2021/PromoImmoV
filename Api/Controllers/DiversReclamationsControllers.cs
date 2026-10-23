using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class DepenseDto
{
    [Required] public string Libelle { get; set; } = "";
    [Required] public string Categorie { get; set; } = "";
    [Range(0.01, double.MaxValue)] public decimal Montant { get; set; }
    public DateTime? Date { get; set; }
    public int? ChantierId { get; set; }
    public string? Reference { get; set; }
}

[ApiController, Route("api/depenses"), Authorize(Roles = "Gerant,Achats")]
public class DiversController : ControllerBase
{
    private readonly AppDbContext _db;
    public DiversController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(DateTime? du, DateTime? au, string? categorie) =>
        Ok(await _db.Depenses.AsNoTracking()
            .Where(d => (du == null || d.Date >= du) && (au == null || d.Date <= au) && (categorie == null || d.Categorie == categorie))
            .OrderByDescending(d => d.Date).ThenByDescending(d => d.Id)
            .Select(d => new { d.Id, d.Libelle, d.Categorie, d.Montant, d.Date, d.ChantierId,
                ChantierNom = d.Chantier != null ? d.Chantier.Nom : null, d.Reference }).Take(500).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(DepenseDto dto)
    {
        var d = new DepenseDiverse { Libelle = dto.Libelle, Categorie = dto.Categorie, Montant = dto.Montant,
            Date = (dto.Date ?? DateTime.Today).Date, ChantierId = dto.ChantierId, Reference = dto.Reference,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) };
        _db.Depenses.Add(d);
        await _db.SaveChangesAsync();
        return Ok(new { d.Id });
    }
}

public class ReclamationPublicDto
{
    [Required, StringLength(100)] public string Nom { get; set; } = "";
    [StringLength(30)] public string? Telephone { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(100)] public string? ContratRef { get; set; }
    [Required, StringLength(150)] public string Objet { get; set; } = "";
    [Required, StringLength(3000)] public string Message { get; set; } = "";
    public string? Website { get; set; } // champ piège anti-robots : doit rester vide
}
public class TraiterReclamationDto { public StatutReclamation Statut { get; set; } public string? Reponse { get; set; } }

[ApiController, Route("api/reclamations")]
public class ReclamationsController : ControllerBase
{
    private static readonly ConcurrentDictionary<string, List<DateTime>> _hits = new();
    private readonly AppDbContext _db;
    private readonly Notifier _notif;
    public ReclamationsController(AppDbContext db, Notifier notif) { _db = db; _notif = notif; }

    /// <summary>Formulaire public « Contactez-nous » (sans connexion).</summary>
    [HttpPost("public"), AllowAnonymous]
    public async Task<IActionResult> Public(ReclamationPublicDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.Website)) return Ok(new { numero = "RCL-00000" }); // robot : on ignore silencieusement

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?";
        var list = _hits.GetOrAdd(ip, _ => new List<DateTime>());
        lock (list)
        {
            list.RemoveAll(t => t < DateTime.UtcNow.AddHours(-1));
            if (list.Count >= 5) return StatusCode(429, new { message = "Trop de messages envoyés. Réessayez plus tard." });
            list.Add(DateTime.UtcNow);
        }

        var r = new Reclamation { Nom = dto.Nom.Trim(), Telephone = dto.Telephone, Email = dto.Email, ContratRef = dto.ContratRef,
            Objet = dto.Objet.Trim(), Message = dto.Message.Trim() };
        _db.Reclamations.Add(r);
        await _db.SaveChangesAsync();
        await _notif.GerantsAsync("RECLAMATION", $"Nouvelle réclamation de {r.Nom} : {r.Objet}", r.Id);
        return Ok(new { numero = $"RCL-{r.Id:D5}" });
    }

    [HttpGet, Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> List(StatutReclamation? statut) =>
        Ok(await _db.Reclamations.AsNoTracking().Where(r => statut == null || r.Statut == statut)
            .OrderBy(r => r.Statut == StatutReclamation.Traitee || r.Statut == StatutReclamation.Rejetee).ThenByDescending(r => r.DateCreation)
            .Select(r => new { r.Id, r.Nom, r.Telephone, r.Email, r.ContratRef, r.Objet, r.Message, r.Statut,
                r.Reponse, r.DateCreation, r.DateTraitement, r.TraiteePar }).Take(500).ToListAsync());

    [HttpPut("{id:int}/traiter"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Traiter(int id, TraiterReclamationDto dto)
    {
        var r = await _db.Reclamations.FindAsync(id);
        if (r is null) return NotFound();
        r.Statut = dto.Statut; r.Reponse = dto.Reponse; r.DateTraitement = DateTime.UtcNow; r.TraiteePar = User.Identity?.Name;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
