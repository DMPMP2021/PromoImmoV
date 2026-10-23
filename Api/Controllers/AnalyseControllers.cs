using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

[ApiController, Route("api/rentabilite"), Authorize(Roles = "Gerant")]
public class RentabiliteController : ControllerBase
{
    private readonly RentabiliteService _svc;
    public RentabiliteController(RentabiliteService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var l = await _svc.CalculerAsync();
        return Ok(new
        {
            Chantiers = l,
            Totaux = new
            {
                ChiffreAffaires = l.Sum(x => x.ChiffreAffaires), Encaisse = l.Sum(x => x.Encaisse), Rembourse = l.Sum(x => x.Rembourse),
                Depense = l.Sum(x => x.Depense), MargeReelle = l.Sum(x => x.MargeReelle),
                EnDepassement = l.Count(x => x.Alerte == "Depassement")
            }
        });
    }
}

public class RelanceDto { [Required] public string Canal { get; set; } = ""; public string? Message { get; set; } }

[ApiController, Route("api/impayes"), Authorize(Roles = "Gerant,Recette")]
public class ImpayesController : ControllerBase
{
    private readonly ImpayesService _svc;
    private readonly AppDbContext _db;
    public ImpayesController(ImpayesService svc, AppDbContext db) { _svc = svc; _db = db; }

    [HttpGet]
    public async Task<IActionResult> List(int joursMin = 0) => Ok(await _svc.ListerAsync(joursMin));

    [HttpPost("{contratId:int}/relance")]
    public async Task<IActionResult> Relancer(int contratId, RelanceDto dto)
    {
        if (!await _db.Contrats.AnyAsync(c => c.Id == contratId)) return NotFound();
        _db.Relances.Add(new Relance { ContratId = contratId, Canal = dto.Canal, Message = dto.Message ?? "", Par = User.Identity?.Name });
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController, Route("api/alertes"), Authorize(Roles = "Gerant")]
public class AlertesController : ControllerBase
{
    private readonly RappelsService _svc;
    public AlertesController(RappelsService svc) => _svc = svc;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await _svc.CalculerAsync());

    /// <summary>Force la génération immédiate des notifications d'alerte.</summary>
    [HttpPost("generer")]
    public async Task<IActionResult> Generer() => Ok(new { crees = await _svc.GenererAsync() });
}

[ApiController, Route("api/stats"), Authorize(Roles = "Gerant")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StatsController(AppDbContext db) => _db = db;

    [HttpGet("graphiques")]
    public async Task<IActionResult> Graphiques()
    {
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);
        var rows = await _db.Encaissements.Where(e => !e.Annule && e.DateEncaissement >= start)
            .GroupBy(e => new { e.DateEncaissement.Year, e.DateEncaissement.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, T = g.Sum(x => x.Montant) }).ToListAsync();
        var mois = Enumerable.Range(0, 12).Select(i => start.AddMonths(i)).Select(d => new
        {
            Mois = d.ToString("yyyy-MM"),
            Total = rows.Where(r => r.Year == d.Year && r.Month == d.Month).Sum(r => r.T)
        }).ToList();

        var achats = await _db.Commandes.Where(c => c.Statut == StatutCommande.Commandee || c.Statut == StatutCommande.Livree || c.Statut == StatutCommande.Payee)
            .SumAsync(c => (decimal?)c.Montant) ?? 0m;
        var personnel = await _db.PaiementsPersonnel.SumAsync(p => (decimal?)p.Montant) ?? 0m;
        var materiel = await _db.PaiementsMateriel.SumAsync(p => (decimal?)p.Montant) ?? 0m;
        var fiscal = await _db.Obligations.Where(o => o.Statut == StatutObligation.Payee).SumAsync(o => (decimal?)o.Montant) ?? 0m;
        var remb = await _db.Remboursements.Where(r => r.Statut == StatutRemboursement.Payee).SumAsync(r => (decimal?)r.Montant) ?? 0m;
        var divers = await _db.Depenses.SumAsync(d => (decimal?)d.Montant) ?? 0m;

        return Ok(new
        {
            EncaissementsParMois = mois,
            Depenses = new[]
            {
                new { Categorie = "Achats", Montant = achats }, new { Categorie = "Personnel", Montant = personnel },
                new { Categorie = "Matériel", Montant = materiel }, new { Categorie = "Fiscal / parafiscal", Montant = fiscal },
                new { Categorie = "Remboursements", Montant = remb }, new { Categorie = "Divers", Montant = divers }
            }
        });
    }
}
