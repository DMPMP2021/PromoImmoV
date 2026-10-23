using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class ObligationDto
{
    public TypeObligation Type { get; set; }
    [Required] public string Libelle { get; set; } = "";
    [Required] public string Periode { get; set; } = "";
    [Range(0, double.MaxValue)] public decimal Montant { get; set; }
    public DateTime DateEcheance { get; set; }
}
public class PayerObligationDto { public DateTime? DatePaiement { get; set; } public string? Reference { get; set; } }

[ApiController, Route("api/obligations"), Authorize(Roles = "Gerant,Fiscal")]
public class FiscalController : ControllerBase
{
    private readonly AppDbContext _db;
    public FiscalController(AppDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> List(TypeObligation? type, StatutObligation? statut)
    {
        var today = DateTime.Today;
        return Ok(await _db.Obligations.AsNoTracking()
            .Where(o => (type == null || o.Type == type) && (statut == null || o.Statut == statut))
            .OrderBy(o => o.Statut == StatutObligation.Payee).ThenBy(o => o.DateEcheance)
            .Select(o => new
            {
                o.Id, o.Type, o.Libelle, o.Periode, o.Montant, o.DateEcheance, o.Statut, o.DatePaiement, o.Reference,
                EnRetard = o.Statut == StatutObligation.APayer && o.DateEcheance < today
            }).Take(500).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Create(ObligationDto dto)
    {
        var o = new ObligationFiscale { Type = dto.Type, Libelle = dto.Libelle, Periode = dto.Periode, Montant = dto.Montant,
            DateEcheance = dto.DateEcheance.Date, CreatedBy = Uid };
        _db.Obligations.Add(o);
        await _db.SaveChangesAsync();
        return Ok(new { o.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ObligationDto dto)
    {
        var o = await _db.Obligations.FindAsync(id);
        if (o is null) return NotFound();
        if (o.Statut == StatutObligation.Payee) return BadRequest(new { message = "Une obligation payée ne peut plus être modifiée." });
        o.Type = dto.Type; o.Libelle = dto.Libelle; o.Periode = dto.Periode; o.Montant = dto.Montant; o.DateEcheance = dto.DateEcheance.Date;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/payer")]
    public async Task<IActionResult> Payer(int id, PayerObligationDto dto)
    {
        var o = await _db.Obligations.FindAsync(id);
        if (o is null) return NotFound();
        o.Statut = StatutObligation.Payee; o.DatePaiement = (dto.DatePaiement ?? DateTime.Today).Date; o.Reference = dto.Reference;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
