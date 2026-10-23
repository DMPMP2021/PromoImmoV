using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class EmployeDto
{
    [Required] public string Nom { get; set; } = "";
    [Required] public string Poste { get; set; } = "";
    public string? Telephone { get; set; }
    [Range(0, double.MaxValue)] public decimal SalaireJournalier { get; set; }
    public int? ChantierId { get; set; }
    public bool Actif { get; set; } = true;
}
public class LignePointageDto
{
    public int EmployeId { get; set; }
    public StatutPointage Statut { get; set; } = StatutPointage.Present;
    public decimal Heures { get; set; } = 8;
    public string? Remarque { get; set; }
}
public class FeuillePointageDto
{
    public DateTime Date { get; set; }
    public int? ChantierId { get; set; }
    public List<LignePointageDto> Lignes { get; set; } = new();
}
public class PaiementPersonnelDto
{
    [Required] public int EmployeId { get; set; }
    public TypePaiementPersonnel Type { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Montant { get; set; }
    public string? Periode { get; set; }
    public string? Motif { get; set; }
    public DateTime? Date { get; set; }
}

[ApiController, Route("api"), Authorize(Roles = "Gerant,Personnel,Suivi")]
public class PersonnelController : ControllerBase
{
    private readonly AppDbContext _db;
    public PersonnelController(AppDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Employés ----------
    [HttpGet("employes")]
    public async Task<IActionResult> Employes(bool? actif, int? chantierId) =>
        Ok(await _db.Employes.AsNoTracking()
            .Where(e => (actif == null || e.Actif == actif) && (chantierId == null || e.ChantierId == chantierId))
            .OrderBy(e => e.Nom)
            .Select(e => new { e.Id, e.Nom, e.Poste, e.Telephone, e.SalaireJournalier, e.ChantierId,
                ChantierNom = e.Chantier != null ? e.Chantier.Nom : null, e.Actif }).ToListAsync());

    [HttpPost("employes"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> AddEmploye(EmployeDto dto)
    {
        var e = new Employe { Nom = dto.Nom, Poste = dto.Poste, Telephone = dto.Telephone, SalaireJournalier = dto.SalaireJournalier,
            ChantierId = dto.ChantierId, Actif = dto.Actif, CreatedBy = Uid };
        _db.Employes.Add(e);
        await _db.SaveChangesAsync();
        return Ok(new { e.Id });
    }

    [HttpPut("employes/{id:int}"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> UpdEmploye(int id, EmployeDto dto)
    {
        var e = await _db.Employes.FindAsync(id);
        if (e is null) return NotFound();
        e.Nom = dto.Nom; e.Poste = dto.Poste; e.Telephone = dto.Telephone; e.SalaireJournalier = dto.SalaireJournalier;
        e.ChantierId = dto.ChantierId; e.Actif = dto.Actif;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ---------- Pointage journalier ----------
    [HttpGet("pointages/feuille")]
    public async Task<IActionResult> Feuille(DateTime date, int? chantierId)
    {
        var d = date.Date;
        var emps = await _db.Employes.AsNoTracking()
            .Where(e => e.Actif && (chantierId == null || e.ChantierId == chantierId)).OrderBy(e => e.Nom).ToListAsync();
        var ids = emps.Select(e => e.Id).ToList();
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.Date == d && ids.Contains(p.EmployeId)).ToListAsync();
        return Ok(emps.Select(e =>
        {
            var p = pts.FirstOrDefault(x => x.EmployeId == e.Id);
            return new { EmployeId = e.Id, e.Nom, e.Poste, e.ChantierId, Statut = p?.Statut, Heures = p?.Heures ?? 8m, Remarque = p?.Remarque, Pointe = p != null };
        }));
    }

    [HttpPost("pointages/feuille")]
    public async Task<IActionResult> SaveFeuille(FeuillePointageDto dto)
    {
        var d = dto.Date.Date;
        var ids = dto.Lignes.Select(l => l.EmployeId).ToList();
        var existants = await _db.Pointages.Where(p => p.Date == d && ids.Contains(p.EmployeId)).ToListAsync();
        var emps = await _db.Employes.AsNoTracking().Where(e => ids.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.ChantierId);

        foreach (var l in dto.Lignes)
        {
            var p = existants.FirstOrDefault(x => x.EmployeId == l.EmployeId);
            if (p is null)
            {
                p = new Pointage { EmployeId = l.EmployeId, Date = d, CreatedBy = Uid };
                _db.Pointages.Add(p);
            }
            p.Statut = l.Statut;
            p.Heures = (l.Statut == StatutPointage.Present || l.Statut == StatutPointage.Retard) ? l.Heures : 0;
            p.Remarque = l.Remarque;
            p.ChantierId = dto.ChantierId ?? emps.GetValueOrDefault(l.EmployeId);
        }
        await _db.SaveChangesAsync();
        return Ok(new { enregistres = dto.Lignes.Count });
    }

    // ---------- Paiements du personnel ----------
    [HttpGet("paiements-personnel"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> Paiements(int? employeId, TypePaiementPersonnel? type, DateTime? du, DateTime? au) =>
        Ok(await _db.PaiementsPersonnel.AsNoTracking()
            .Where(p => (employeId == null || p.EmployeId == employeId) && (type == null || p.Type == type) &&
                        (du == null || p.Date >= du) && (au == null || p.Date <= au))
            .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
            .Select(p => new { p.Id, p.EmployeId, EmployeNom = p.Employe.Nom, p.Type, p.Montant, p.Periode, p.Motif, p.Date })
            .Take(500).ToListAsync());

    [HttpPost("paiements-personnel"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> Payer(PaiementPersonnelDto dto)
    {
        if (!await _db.Employes.AnyAsync(e => e.Id == dto.EmployeId)) return BadRequest(new { message = "Employé introuvable." });
        var p = new PaiementPersonnel { EmployeId = dto.EmployeId, Type = dto.Type, Montant = dto.Montant, Periode = dto.Periode,
            Motif = dto.Motif, Date = (dto.Date ?? DateTime.Today).Date, CreatedBy = Uid };
        _db.PaiementsPersonnel.Add(p);
        await _db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    // ---------- Récapitulatif mensuel ----------
    [HttpGet("personnel/recap"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> Recap(string? mois)
    {
        if (!DateTime.TryParseExact((mois ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var debut))
            debut = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var fin = debut.AddMonths(1);

        var emps = await _db.Employes.AsNoTracking().Where(e => e.Actif).OrderBy(e => e.Nom).ToListAsync();
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.Date >= debut && p.Date < fin).ToListAsync();
        var pay = await _db.PaiementsPersonnel.AsNoTracking().Where(p => p.Date >= debut && p.Date < fin).ToListAsync();

        return Ok(emps.Select(e =>
        {
            var mp = pts.Where(p => p.EmployeId == e.Id).ToList();
            var jours = mp.Count(p => (p.Statut == StatutPointage.Present || p.Statut == StatutPointage.Retard));
            var du = jours * e.SalaireJournalier;
            var paye = pay.Where(p => p.EmployeId == e.Id).Sum(p => p.Montant);
            return new
            {
                e.Id, e.Nom, e.Poste, e.SalaireJournalier,
                JoursPresents = jours, Absences = mp.Count(p => p.Statut == StatutPointage.Absent),
                Heures = mp.Sum(p => p.Heures), SalaireEstime = du, TotalPaye = paye, Reste = du - paye
            };
        }));
    }
}
