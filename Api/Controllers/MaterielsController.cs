using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class MaterielDto
{
    [Required] public string Designation { get; set; } = "";
    public TypeMateriel Type { get; set; }
    public string? Immatriculation { get; set; }
    public string Statut { get; set; } = "En service";
}

public class EntretienDto
{
    [Required] public string Type { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public string? Description { get; set; }
}

public class PaiementMaterielDto
{
    [Required] public int MaterielId { get; set; }
    public int? EntretienId { get; set; }
    public int? ChantierId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Montant { get; set; }
    [Required] public string Motif { get; set; } = "";
    public DateTime? Date { get; set; }
}

[ApiController, Route("api"), Authorize(Roles = "Gerant,Materiel")]
public class MaterielsController : ControllerBase
{
    private readonly AppDbContext _db;
    public MaterielsController(AppDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("materiels")]
    public async Task<IActionResult> List(TypeMateriel? type) =>
        Ok(await _db.Materiels.AsNoTracking().Where(m => type == null || m.Type == type)
            .OrderBy(m => m.Designation)
            .Select(m => new
            {
                m.Id, m.Designation, Type = m.Type.ToString(), m.Immatriculation, m.Statut,
                NbEntretiens = m.Entretiens.Count,
                TotalPaye = m.Paiements.Sum(p => (decimal?)p.Montant) ?? 0m
            }).ToListAsync());

    [HttpGet("materiels/{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var m = await _db.Materiels.AsNoTracking().Include(x => x.Entretiens).Include(x => x.Paiements)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (m is null) return NotFound();
        return Ok(new
        {
            m.Id, m.Designation, Type = m.Type.ToString(), m.Immatriculation, m.Statut,
            TotalPaye = m.Paiements.Sum(p => p.Montant),
            Entretiens = m.Entretiens.OrderByDescending(e => e.Date).Select(e => new { e.Id, e.Type, e.Date, e.Description }),
            Paiements = m.Paiements.OrderByDescending(p => p.Date).Select(p => new { p.Id, p.EntretienId, p.Montant, p.Motif, p.Date })
        });
    }

    [HttpPost("materiels")]
    public async Task<IActionResult> Create(MaterielDto dto)
    {
        var m = new Materiel { Designation = dto.Designation, Type = dto.Type, Immatriculation = dto.Immatriculation,
            Statut = dto.Statut, CreatedBy = Uid };
        _db.Materiels.Add(m);
        await _db.SaveChangesAsync();
        return Ok(new { m.Id });
    }

    [HttpPut("materiels/{id:int}")]
    public async Task<IActionResult> Update(int id, MaterielDto dto)
    {
        var m = await _db.Materiels.FindAsync(id);
        if (m is null) return NotFound();
        m.Designation = dto.Designation; m.Type = dto.Type; m.Immatriculation = dto.Immatriculation; m.Statut = dto.Statut;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("materiels/{id:int}/entretiens")]
    public async Task<IActionResult> AddEntretien(int id, EntretienDto dto)
    {
        if (!await _db.Materiels.AnyAsync(m => m.Id == id)) return NotFound();
        var e = new Entretien { MaterielId = id, Type = dto.Type, Date = dto.Date, Description = dto.Description, CreatedBy = Uid };
        _db.Entretiens.Add(e);
        await _db.SaveChangesAsync();
        return Ok(new { e.Id });
    }

    [HttpPost("paiements-materiel")]
    public async Task<IActionResult> Payer(PaiementMaterielDto dto)
    {
        if (!await _db.Materiels.AnyAsync(m => m.Id == dto.MaterielId)) return NotFound(new { message = "Matériel introuvable." });
        if (dto.EntretienId != null && !await _db.Entretiens.AnyAsync(e => e.Id == dto.EntretienId && e.MaterielId == dto.MaterielId))
            return BadRequest(new { message = "Entretien invalide pour ce matériel." });
        var p = new PaiementMateriel { MaterielId = dto.MaterielId, EntretienId = dto.EntretienId, ChantierId = dto.ChantierId, Montant = dto.Montant,
            Motif = dto.Motif, Date = dto.Date ?? DateTime.UtcNow, CreatedBy = Uid };
        _db.PaiementsMateriel.Add(p);
        await _db.SaveChangesAsync();
        return Ok(new { p.Id });
    }
}
