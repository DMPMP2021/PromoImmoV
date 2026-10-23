using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class BudgetCreateDto
{
    public int? ChantierId { get; set; }
    public string? Service { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Montant { get; set; }
    [Required] public string Motif { get; set; } = "";
    public bool Forcer { get; set; }
}

[ApiController, Route("api"), Authorize(Roles = "Gerant")]
public class BudgetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public BudgetsController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    public static readonly string[] Services =
        { "Matériel roulant", "Matériel non roulant", "Entretien", "Administration", "Autre" };

    [HttpGet("budgets/services")]
    public IActionResult GetServices() => Ok(Services);

    [HttpGet("budgets")]
    public async Task<IActionResult> List(int? chantierId, string? service)
    {
        var rows = await _db.Allocations.AsNoTracking()
            .Where(a => (chantierId == null || a.ChantierId == chantierId) && (service == null || a.Service == service))
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.Id, a.ChantierId, Chantier = a.Chantier != null ? a.Chantier.Nom : null,
                a.Service, a.Montant, a.Motif, a.DecidePar, a.CreatedAt }).Take(300).ToListAsync();
        var names = await _users.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.NomComplet);
        return Ok(rows.Select(r => new { r.Id, r.ChantierId, r.Chantier, r.Service, r.Montant, r.Motif,
            DecidePar = names.GetValueOrDefault(r.DecidePar, "?"), r.CreatedAt }));
    }

    [HttpPost("budgets")]
    public async Task<IActionResult> Create(BudgetCreateDto dto)
    {
        if ((dto.ChantierId == null) == string.IsNullOrWhiteSpace(dto.Service))
            return BadRequest(new { message = "Choisissez soit un chantier, soit un service." });
        if (dto.ChantierId != null && !await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId))
            return BadRequest(new { message = "Chantier introuvable." });

        var encaisse = await _db.Encaissements.Where(e => !e.Annule).SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var alloue = await _db.Allocations.SumAsync(a => (decimal?)a.Montant) ?? 0m;
        var remb = await _db.Remboursements.Where(r => r.Statut == StatutRemboursement.Payee).SumAsync(r => (decimal?)r.Montant) ?? 0m;
        var tresorerie = encaisse - remb - alloue;

        if (dto.Montant > tresorerie && !dto.Forcer)
            return Conflict(new { message = $"Le montant dépasse la trésorerie disponible ({tresorerie:N2} DA).", tresorerie });

        var uid = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var a = new AllocationBudget { ChantierId = dto.ChantierId, Service = dto.Service, Montant = dto.Montant,
            Motif = dto.Motif, DecidePar = uid, CreatedBy = uid };
        _db.Allocations.Add(a);
        await _db.SaveChangesAsync();
        return Ok(new { a.Id });
    }

    [HttpGet("chantiers/{id:int}/finances")]
    public async Task<IActionResult> Finances(int id)
    {
        var ch = await _db.Chantiers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (ch is null) return NotFound();
        var encaisse = await _db.Encaissements.Where(e => !e.Annule && e.Contrat.ChantierId == id).SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var alloue = await _db.Allocations.Where(a => a.ChantierId == id).SumAsync(a => (decimal?)a.Montant) ?? 0m;
        return Ok(new { ch.Id, ch.Nom, Encaisse = encaisse, Alloue = alloue, Solde = encaisse - alloue });
    }
}
