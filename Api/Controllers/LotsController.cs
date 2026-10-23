using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class LotDto
{
    [Required] public int ChantierId { get; set; }
    [Required] public string Reference { get; set; } = "";
    public string? TypeBien { get; set; }
    public string? Etage { get; set; }
    [Range(0, double.MaxValue)] public decimal Surface { get; set; }
    [Range(0, double.MaxValue)] public decimal Prix { get; set; }
    public string? Notes { get; set; }
}
public class LotSerieDto
{
    [Required] public int ChantierId { get; set; }
    [Required] public string Prefixe { get; set; } = "";
    public int De { get; set; } = 1;
    public int A { get; set; } = 1;
    public string? TypeBien { get; set; }
    public decimal Surface { get; set; }
    public decimal Prix { get; set; }
}
public class ReserverDto { [Required] public int ClientId { get; set; } [Range(1, 365)] public int Jours { get; set; } = 15; }
public class LotContratDto
{
    public int? ClientId { get; set; }
    public decimal? Prix { get; set; }
    [Range(1, 240)] public int NombreTranches { get; set; } = 4;
    public DateTime DatePremiere { get; set; } = DateTime.Today;
    [Range(1, 12)] public int IntervalleMois { get; set; } = 1;
}

[ApiController, Route("api/lots"), Authorize(Roles = "Gerant,Recette")]
public class LotsController : ControllerBase
{
    private readonly AppDbContext _db;
    public LotsController(AppDbContext db) => _db = db;
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> List(int? chantierId, StatutLot? statut)
    {
        var today = DateTime.Today;
        return Ok(await _db.Lots.AsNoTracking()
            .Where(l => (chantierId == null || l.ChantierId == chantierId) && (statut == null || l.Statut == statut))
            .OrderBy(l => l.Chantier.Nom).ThenBy(l => l.Reference)
            .Select(l => new
            {
                l.Id, l.ChantierId, ChantierNom = l.Chantier.Nom, l.Reference, l.TypeBien, l.Etage, l.Surface, l.Prix, l.Statut,
                l.ClientId, ClientNom = l.Client != null ? l.Client.Nom : null, l.ContratId, l.DateReservation, l.DateFinReservation, l.Notes,
                Expiree = l.Statut == StatutLot.Reserve && l.DateFinReservation != null && l.DateFinReservation < today
            }).Take(1000).ToListAsync());
    }

    [HttpGet("synthese")]
    public async Task<IActionResult> Synthese()
    {
        var chantiers = await _db.Chantiers.AsNoTracking().OrderBy(c => c.Nom).ToListAsync();
        var lots = await _db.Lots.AsNoTracking().Select(l => new { l.ChantierId, l.Statut, l.Prix }).ToListAsync();
        return Ok(chantiers.Select(c =>
        {
            var m = lots.Where(l => l.ChantierId == c.Id).ToList();
            var total = m.Count;
            var vendus = m.Count(l => l.Statut == StatutLot.Vendu);
            var reserves = m.Count(l => l.Statut == StatutLot.Reserve);
            return new
            {
                c.Id, c.Nom, Total = total, Disponibles = m.Count(l => l.Statut == StatutLot.Disponible), Reserves = reserves, Vendus = vendus,
                TauxVente = total == 0 ? 0 : Math.Round(vendus * 100.0 / total, 1),
                TauxReserve = total == 0 ? 0 : Math.Round(reserves * 100.0 / total, 1),
                TauxCommercialisation = total == 0 ? 0 : Math.Round((vendus + reserves) * 100.0 / total, 1),
                ValeurStock = m.Where(l => l.Statut == StatutLot.Disponible).Sum(l => l.Prix),
                ChiffreVendu = m.Where(l => l.Statut == StatutLot.Vendu).Sum(l => l.Prix)
            };
        }));
    }

    [HttpPost, Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Create(LotDto dto)
    {
        if (!await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });
        if (await _db.Lots.AnyAsync(l => l.ChantierId == dto.ChantierId && l.Reference == dto.Reference))
            return BadRequest(new { message = "Cette référence existe déjà sur ce chantier." });
        var l = new Lot { ChantierId = dto.ChantierId, Reference = dto.Reference.Trim(), TypeBien = dto.TypeBien, Etage = dto.Etage,
            Surface = dto.Surface, Prix = dto.Prix, Notes = dto.Notes, CreatedBy = Uid };
        _db.Lots.Add(l);
        await _db.SaveChangesAsync();
        return Ok(new { l.Id });
    }

    [HttpPost("serie"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Serie(LotSerieDto dto)
    {
        if (dto.A < dto.De || dto.A - dto.De > 500) return BadRequest(new { message = "Plage invalide (500 lots maximum)." });
        if (!await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });
        var existantes = (await _db.Lots.Where(l => l.ChantierId == dto.ChantierId).Select(l => l.Reference).ToListAsync()).ToHashSet();
        var crees = 0;
        for (var n = dto.De; n <= dto.A; n++)
        {
            var reference = $"{dto.Prefixe}{n}";
            if (existantes.Contains(reference)) continue;
            _db.Lots.Add(new Lot { ChantierId = dto.ChantierId, Reference = reference, TypeBien = dto.TypeBien, Surface = dto.Surface, Prix = dto.Prix, CreatedBy = Uid });
            crees++;
        }
        await _db.SaveChangesAsync();
        return Ok(new { crees });
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Update(int id, LotDto dto)
    {
        var l = await _db.Lots.FindAsync(id);
        if (l is null) return NotFound();
        if (l.Statut == StatutLot.Vendu && dto.Prix != l.Prix) return BadRequest(new { message = "Le prix d'un lot vendu ne peut plus être modifié." });
        if (await _db.Lots.AnyAsync(x => x.Id != id && x.ChantierId == l.ChantierId && x.Reference == dto.Reference))
            return BadRequest(new { message = "Cette référence existe déjà sur ce chantier." });
        l.Reference = dto.Reference.Trim(); l.TypeBien = dto.TypeBien; l.Etage = dto.Etage; l.Surface = dto.Surface; l.Prix = dto.Prix; l.Notes = dto.Notes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Delete(int id)
    {
        var l = await _db.Lots.FindAsync(id);
        if (l is null) return NotFound();
        if (l.Statut != StatutLot.Disponible) return BadRequest(new { message = "Seul un lot disponible peut être supprimé." });
        _db.Lots.Remove(l);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/reserver")]
    public async Task<IActionResult> Reserver(int id, ReserverDto dto)
    {
        var l = await _db.Lots.FindAsync(id);
        if (l is null) return NotFound();
        if (l.Statut != StatutLot.Disponible) return BadRequest(new { message = "Ce lot n'est pas disponible." });
        if (!await _db.Clients.AnyAsync(c => c.Id == dto.ClientId)) return BadRequest(new { message = "Client introuvable." });
        l.Statut = StatutLot.Reserve; l.ClientId = dto.ClientId;
        l.DateReservation = DateTime.Today; l.DateFinReservation = DateTime.Today.AddDays(dto.Jours);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id:int}/liberer")]
    public async Task<IActionResult> Liberer(int id)
    {
        var l = await _db.Lots.FindAsync(id);
        if (l is null) return NotFound();
        if (l.Statut != StatutLot.Reserve) return BadRequest(new { message = "Seul un lot réservé peut être libéré." });
        l.Statut = StatutLot.Disponible; l.ClientId = null; l.DateReservation = null; l.DateFinReservation = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Crée le contrat (avec échéancier) directement depuis le lot et passe le lot à « Vendu ».</summary>
    [HttpPost("{id:int}/contrat")]
    public async Task<IActionResult> CreerContrat(int id, LotContratDto dto)
    {
        var lot = await _db.Lots.FirstOrDefaultAsync(x => x.Id == id);
        if (lot is null) return NotFound();
        if (lot.Statut == StatutLot.Vendu) return BadRequest(new { message = "Ce lot est déjà vendu." });
        var clientId = dto.ClientId ?? lot.ClientId;
        if (clientId == null) return BadRequest(new { message = "Choisissez un client." });
        if (lot.Statut == StatutLot.Reserve && lot.ClientId != null && lot.ClientId != clientId)
            return BadRequest(new { message = "Ce lot est réservé pour un autre client. Libérez-le d'abord." });
        if (!await _db.Clients.AnyAsync(c => c.Id == clientId)) return BadRequest(new { message = "Client introuvable." });
        var prix = dto.Prix ?? lot.Prix;
        if (prix <= 0) return BadRequest(new { message = "Le prix du lot doit être supérieur à 0." });

        await using var tx = await _db.Database.BeginTransactionAsync();
        var ct = new Contrat { ClientId = clientId.Value, ChantierId = lot.ChantierId, Lot = lot.Reference, PrixTotal = prix, CreatedBy = Uid };
        var part = Math.Round(prix / dto.NombreTranches, 2);
        for (var i = 1; i <= dto.NombreTranches; i++)
            ct.Echeances.Add(new Echeance
            {
                Numero = i,
                MontantPrevu = i == dto.NombreTranches ? prix - part * (dto.NombreTranches - 1) : part,
                DatePrevue = dto.DatePremiere.Date.AddMonths((i - 1) * dto.IntervalleMois)
            });
        _db.Contrats.Add(ct);
        await _db.SaveChangesAsync();

        lot.Statut = StatutLot.Vendu; lot.ClientId = clientId; lot.ContratId = ct.Id; lot.DateFinReservation = null;
        await _db.SaveChangesAsync();
        await tx.CommitAsync();
        return Ok(new { ct.Id });
    }
}
