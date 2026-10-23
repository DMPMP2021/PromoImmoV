using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class AvancementDto
{
    [Required] public int ChantierId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    [Required] public string Phase { get; set; } = "";
    [Range(0, 100)] public int Pourcentage { get; set; }
    public string? Observations { get; set; }
}

[ApiController, Route("api"), Authorize(Roles = "Gerant,Technique")]
public class TechniqueController : ControllerBase
{
    private readonly AppDbContext _db;
    public TechniqueController(AppDbContext db) => _db = db;

    [HttpGet("avancements")]
    public async Task<IActionResult> List(int? chantierId) =>
        Ok(await _db.Avancements.AsNoTracking().Where(a => chantierId == null || a.ChantierId == chantierId)
            .OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
            .Select(a => new { a.Id, a.ChantierId, ChantierNom = a.Chantier.Nom, a.Date, a.Phase, a.Pourcentage, a.Observations })
            .Take(300).ToListAsync());

    [HttpPost("avancements")]
    public async Task<IActionResult> Create(AvancementDto dto)
    {
        if (!await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });
        var a = new AvancementChantier { ChantierId = dto.ChantierId, Date = dto.Date.Date, Phase = dto.Phase,
            Pourcentage = dto.Pourcentage, Observations = dto.Observations, CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) };
        _db.Avancements.Add(a);
        await _db.SaveChangesAsync();
        return Ok(new { a.Id });
    }

    [HttpGet("technique/synthese")]
    public async Task<IActionResult> Synthese()
    {
        var today = DateTime.Today;
        var chantiers = await _db.Chantiers.AsNoTracking().OrderBy(c => c.Nom).ToListAsync();
        var av = await _db.Avancements.AsNoTracking().OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).ToListAsync();
        var last = av.GroupBy(a => a.ChantierId).ToDictionary(g => g.Key, g => g.First());
        var taches = await _db.Taches.AsNoTracking().Where(t => t.Service == ServiceCode.Technique && t.ChantierId != null)
            .Select(t => new { t.ChantierId, t.Statut, t.DateEcheance }).ToListAsync();

        return Ok(chantiers.Select(c =>
        {
            last.TryGetValue(c.Id, out var l);
            var ts = taches.Where(t => t.ChantierId == c.Id).ToList();
            return new
            {
                c.Id, c.Nom, c.Statut,
                Pourcentage = l?.Pourcentage ?? 0, Phase = l?.Phase, DerniereMaj = l?.Date,
                TachesOuvertes = ts.Count(t => t.Statut != StatutTache.Termine),
                TachesEnRetard = ts.Count(t => t.Statut != StatutTache.Termine && t.DateEcheance != null && t.DateEcheance < today)
            };
        }));
    }
}
