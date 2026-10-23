using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class TacheDto
{
    public ServiceCode Service { get; set; }
    [Required] public string Titre { get; set; } = "";
    public string? Description { get; set; }
    public int? ChantierId { get; set; }
    public string? AssigneA { get; set; }
    public NiveauPriorite Priorite { get; set; } = NiveauPriorite.Normale;
    public StatutTache Statut { get; set; } = StatutTache.APrevoir;
    public DateTime? DateEcheance { get; set; }
}
public class StatutTacheDto { public StatutTache Statut { get; set; } }

[ApiController, Route("api/taches"), Authorize]
public class TachesController : ControllerBase
{
    private readonly AppDbContext _db;
    public TachesController(AppDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> List(ServiceCode? service, StatutTache? statut, int? chantierId)
    {
        if (service == null && !User.IsInRole("Gerant")) return Forbid();
        if (service != null && !Acces.Peut(User, service.Value)) return Forbid();
        var today = DateTime.Today;
        return Ok(await _db.Taches.AsNoTracking()
            .Where(t => (service == null || t.Service == service) && (statut == null || t.Statut == statut) &&
                        (chantierId == null || t.ChantierId == chantierId))
            .OrderBy(t => t.Statut == StatutTache.Termine).ThenBy(t => t.DateEcheance)
            .Select(t => new
            {
                t.Id, t.Service, t.Titre, t.Description, t.ChantierId,
                ChantierNom = t.Chantier != null ? t.Chantier.Nom : null,
                t.AssigneA, t.Priorite, t.Statut, t.DateEcheance, t.DateFin,
                EnRetard = t.Statut != StatutTache.Termine && t.DateEcheance != null && t.DateEcheance < today
            }).Take(500).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(TacheDto dto)
    {
        if (!Acces.Peut(User, dto.Service)) return Forbid();
        var t = new Tache
        {
            Service = dto.Service, Titre = dto.Titre, Description = dto.Description, ChantierId = dto.ChantierId,
            AssigneA = dto.AssigneA, Priorite = dto.Priorite, Statut = dto.Statut, DateEcheance = dto.DateEcheance, CreatedBy = Uid,
            DateFin = dto.Statut == StatutTache.Termine ? DateTime.Today : null
        };
        _db.Taches.Add(t);
        await _db.SaveChangesAsync();
        return Ok(new { t.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, TacheDto dto)
    {
        var t = await _db.Taches.FindAsync(id);
        if (t is null) return NotFound();
        if (!Acces.Peut(User, t.Service) || !Acces.Peut(User, dto.Service)) return Forbid();
        t.Service = dto.Service; t.Titre = dto.Titre; t.Description = dto.Description; t.ChantierId = dto.ChantierId;
        t.AssigneA = dto.AssigneA; t.Priorite = dto.Priorite; t.DateEcheance = dto.DateEcheance;
        t.Statut = dto.Statut; t.DateFin = dto.Statut == StatutTache.Termine ? (t.DateFin ?? DateTime.Today) : null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/statut")]
    public async Task<IActionResult> SetStatut(int id, StatutTacheDto dto)
    {
        var t = await _db.Taches.FindAsync(id);
        if (t is null) return NotFound();
        if (!Acces.Peut(User, t.Service)) return Forbid();
        t.Statut = dto.Statut;
        t.DateFin = dto.Statut == StatutTache.Termine ? DateTime.Today : null;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
