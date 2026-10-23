using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class RapportDto
{
    [Required] public int ChantierId { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    [Required] public string Travaux { get; set; } = "";
    public string? Incidents { get; set; }
    public string? Meteo { get; set; }
}
public class MissionDto
{
    [Required] public int ChantierId { get; set; }
    [Required] public string Titre { get; set; } = "";
    public string? Description { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public StatutMission Statut { get; set; } = StatutMission.Planifiee;
    public List<int> EmployeIds { get; set; } = new();
}
public class StatutMissionDto { public StatutMission Statut { get; set; } }

[ApiController, Route("api"), Authorize(Roles = "Gerant,Suivi")]
public class SuiviController : ControllerBase
{
    private readonly AppDbContext _db;
    public SuiviController(AppDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ---------- Rapports journaliers ----------
    [HttpGet("rapports")]
    public async Task<IActionResult> Rapports(int? chantierId, DateTime? du, DateTime? au) =>
        Ok(await _db.Rapports.AsNoTracking()
            .Where(r => (chantierId == null || r.ChantierId == chantierId) && (du == null || r.Date >= du) && (au == null || r.Date <= au))
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)
            .Select(r => new { r.Id, r.ChantierId, ChantierNom = r.Chantier.Nom, r.Date, r.Travaux, r.Incidents, r.Meteo })
            .Take(300).ToListAsync());

    [HttpPost("rapports")]
    public async Task<IActionResult> AddRapport(RapportDto dto)
    {
        var d = dto.Date.Date;
        if (!await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });
        var r = await _db.Rapports.FirstOrDefaultAsync(x => x.ChantierId == dto.ChantierId && x.Date == d);
        if (r is null)
        {
            r = new RapportJournalier { ChantierId = dto.ChantierId, Date = d, CreatedBy = Uid };
            _db.Rapports.Add(r);
        }
        r.Travaux = dto.Travaux; r.Incidents = dto.Incidents; r.Meteo = dto.Meteo;
        await _db.SaveChangesAsync();
        return Ok(new { r.Id });
    }

    // ---------- Vue de la journée par chantier ----------
    [HttpGet("suivi/journee")]
    public async Task<IActionResult> Journee(DateTime date)
    {
        var d = date.Date;
        var chantiers = await _db.Chantiers.AsNoTracking().Where(c => c.Statut == StatutChantier.EnCours).OrderBy(c => c.Nom).ToListAsync();
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.Date == d).Select(p => new { p.ChantierId, p.Statut }).ToListAsync();
        var rapports = await _db.Rapports.AsNoTracking().Where(r => r.Date == d).ToListAsync();
        var missions = await _db.Missions.AsNoTracking()
            .Where(m => m.DateDebut <= d && m.DateFin >= d && m.Statut != StatutMission.Annulee).Select(m => new { m.ChantierId, m.Titre, m.Statut }).ToListAsync();

        return Ok(chantiers.Select(c => new
        {
            ChantierId = c.Id, c.Nom,
            Presents = pts.Count(p => p.ChantierId == c.Id && (p.Statut == StatutPointage.Present || p.Statut == StatutPointage.Retard)),
            Absents = pts.Count(p => p.ChantierId == c.Id && p.Statut != StatutPointage.Present && p.Statut != StatutPointage.Retard),
            Rapport = rapports.FirstOrDefault(r => r.ChantierId == c.Id)?.Travaux,
            Incidents = rapports.FirstOrDefault(r => r.ChantierId == c.Id)?.Incidents,
            Missions = missions.Where(m => m.ChantierId == c.Id).Select(m => m.Titre)
        }));
    }

    // ---------- Programmation des missions ----------
    [HttpGet("missions")]
    public async Task<IActionResult> Missions(int? chantierId, StatutMission? statut, DateTime? du, DateTime? au) =>
        Ok(await _db.Missions.AsNoTracking()
            .Where(m => (chantierId == null || m.ChantierId == chantierId) && (statut == null || m.Statut == statut) &&
                        (du == null || m.DateFin >= du) && (au == null || m.DateDebut <= au))
            .OrderBy(m => m.DateDebut)
            .Select(m => new
            {
                m.Id, m.ChantierId, ChantierNom = m.Chantier.Nom, m.Titre, m.Description, m.DateDebut, m.DateFin, m.Statut,
                EmployeIds = m.Affectations.Select(a => a.EmployeId).ToList(),
                Employes = m.Affectations.Select(a => a.Employe.Nom).ToList()
            }).Take(500).ToListAsync());

    [HttpPost("missions")]
    public async Task<IActionResult> AddMission(MissionDto dto)
    {
        if (dto.DateFin < dto.DateDebut) return BadRequest(new { message = "La date de fin précède la date de début." });
        if (!await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });
        var m = new Mission { ChantierId = dto.ChantierId, Titre = dto.Titre, Description = dto.Description,
            DateDebut = dto.DateDebut.Date, DateFin = dto.DateFin.Date, Statut = dto.Statut, CreatedBy = Uid };
        foreach (var id in dto.EmployeIds.Distinct()) m.Affectations.Add(new MissionAffectation { EmployeId = id });
        _db.Missions.Add(m);
        await _db.SaveChangesAsync();
        return Ok(new { m.Id });
    }

    [HttpPut("missions/{id:int}")]
    public async Task<IActionResult> UpdMission(int id, MissionDto dto)
    {
        var m = await _db.Missions.Include(x => x.Affectations).FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        if (dto.DateFin < dto.DateDebut) return BadRequest(new { message = "La date de fin précède la date de début." });
        m.ChantierId = dto.ChantierId; m.Titre = dto.Titre; m.Description = dto.Description;
        m.DateDebut = dto.DateDebut.Date; m.DateFin = dto.DateFin.Date; m.Statut = dto.Statut;
        _db.Affectations.RemoveRange(m.Affectations);
        await _db.SaveChangesAsync();
        foreach (var eid in dto.EmployeIds.Distinct()) _db.Affectations.Add(new MissionAffectation { MissionId = id, EmployeId = eid });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("missions/{id:int}/statut")]
    public async Task<IActionResult> SetStatut(int id, StatutMissionDto dto)
    {
        var m = await _db.Missions.FindAsync(id);
        if (m is null) return NotFound();
        m.Statut = dto.Statut;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
