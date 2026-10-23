using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class ClientDto
{
    [Required] public string Nom { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Cin { get; set; }
    public string? Adresse { get; set; }
    public string? Email { get; set; }
}

public class ChantierDto
{
    [Required] public string Nom { get; set; } = "";
    public string? Localisation { get; set; }
    public StatutChantier Statut { get; set; } = StatutChantier.EnCours;
    [Range(0, double.MaxValue)] public decimal BudgetPrevu { get; set; }
}

public class ContratCreateDto
{
    [Required] public int ClientId { get; set; }
    [Required] public int ChantierId { get; set; }
    [Required] public string Lot { get; set; } = "";
    [Range(1, double.MaxValue)] public decimal PrixTotal { get; set; }
    [Range(1, 240)] public int NombreTranches { get; set; } = 1;
    public DateTime DatePremiere { get; set; } = DateTime.Today;
    [Range(1, 12)] public int IntervalleMois { get; set; } = 1;
}

[ApiController, Route("api/clients"), Authorize(Roles = "Gerant,Recette,Remboursement")]
public class ClientsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ClientsController(AppDbContext db) => _db = db;
    private string? Uid => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpGet]
    public async Task<IActionResult> List(string? q)
    {
        var query = _db.Clients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => EF.Functions.ILike(c.Nom, $"%{q}%") || (c.Telephone != null && EF.Functions.ILike(c.Telephone, $"%{q}%")) || (c.Cin != null && EF.Functions.ILike(c.Cin, $"%{q}%")));
        return Ok(await query.OrderBy(c => c.Nom).Take(50)
            .Select(c => new { c.Id, c.Nom, c.Telephone, c.Cin, c.Adresse, c.Email }).ToListAsync());
    }

    [HttpPost, Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Create(ClientDto dto)
    {
        var c = new Client { Nom = dto.Nom, Telephone = dto.Telephone, Cin = dto.Cin, Adresse = dto.Adresse, Email = dto.Email, CreatedBy = Uid };
        _db.Clients.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id, c.Nom, c.Telephone, c.Cin, c.Adresse });
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Update(int id, ClientDto dto)
    {
        var c = await _db.Clients.FindAsync(id);
        if (c is null) return NotFound();
        c.Nom = dto.Nom; c.Telephone = dto.Telephone; c.Cin = dto.Cin; c.Adresse = dto.Adresse; c.Email = dto.Email;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController, Route("api/chantiers"), Authorize]
public class ChantiersController : ControllerBase
{
    private readonly AppDbContext _db;
    public ChantiersController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List() =>
        Ok(await _db.Chantiers.AsNoTracking().OrderBy(c => c.Nom)
            .Select(c => new { c.Id, c.Nom, c.Localisation, Statut = c.Statut.ToString(), c.BudgetPrevu }).ToListAsync());

    [HttpPost, Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Create(ChantierDto dto)
    {
        var c = new Chantier { Nom = dto.Nom, Localisation = dto.Localisation, Statut = dto.Statut, BudgetPrevu = dto.BudgetPrevu,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) };
        _db.Chantiers.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Update(int id, ChantierDto dto)
    {
        var c = await _db.Chantiers.FindAsync(id);
        if (c is null) return NotFound();
        c.Nom = dto.Nom; c.Localisation = dto.Localisation; c.Statut = dto.Statut; c.BudgetPrevu = dto.BudgetPrevu;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController, Route("api/contrats"), Authorize(Roles = "Gerant,Recette,Remboursement")]
public class ContratsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContratsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(int? clientId, int? chantierId) =>
        Ok(await _db.Contrats.AsNoTracking()
            .Where(c => (clientId == null || c.ClientId == clientId) && (chantierId == null || c.ChantierId == chantierId))
            .OrderByDescending(c => c.Id)
            .Select(c => new
            {
                c.Id, c.ClientId, ClientNom = c.Client.Nom, c.ChantierId, ChantierNom = c.Chantier.Nom, c.Lot, c.PrixTotal,
                TotalPaye = c.Encaissements.Where(e => !e.Annule).Sum(e => (decimal?)e.Montant) ?? 0m
            }).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id)
    {
        var c = await _db.Contrats.AsNoTracking().Include(x => x.Client).Include(x => x.Chantier)
            .Include(x => x.Echeances).FirstOrDefaultAsync(x => x.Id == id);
        if (c is null) return NotFound();

        var paid = await _db.Encaissements.Where(e => e.ContratId == id && !e.Annule)
            .Select(e => new { e.EcheanceId, e.Montant }).ToListAsync();
        var totalPaye = paid.Sum(p => p.Montant);

        return Ok(new
        {
            c.Id, c.ClientId, ClientNom = c.Client.Nom, c.ChantierId, ChantierNom = c.Chantier.Nom, c.Lot, c.PrixTotal,
            TotalPaye = totalPaye, Reste = c.PrixTotal - totalPaye,
            Echeances = c.Echeances.OrderBy(e => e.Numero).Select(e =>
            {
                var p = paid.Where(x => x.EcheanceId == e.Id).Sum(x => x.Montant);
                return new { e.Id, e.Numero, e.MontantPrevu, e.DatePrevue, Paye = p, Reste = Math.Max(0, e.MontantPrevu - p) };
            })
        });
    }

    [HttpPost, Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Create(ContratCreateDto dto)
    {
        if (!await _db.Clients.AnyAsync(c => c.Id == dto.ClientId)) return BadRequest(new { message = "Client introuvable." });
        if (!await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });

        var ct = new Contrat { ClientId = dto.ClientId, ChantierId = dto.ChantierId, Lot = dto.Lot, PrixTotal = dto.PrixTotal,
            CreatedBy = User.FindFirstValue(ClaimTypes.NameIdentifier) };
        var part = Math.Round(dto.PrixTotal / dto.NombreTranches, 2);
        for (int i = 1; i <= dto.NombreTranches; i++)
            ct.Echeances.Add(new Echeance
            {
                Numero = i,
                MontantPrevu = i == dto.NombreTranches ? dto.PrixTotal - part * (dto.NombreTranches - 1) : part,
                DatePrevue = dto.DatePremiere.AddMonths((i - 1) * dto.IntervalleMois)
            });
        _db.Contrats.Add(ct);
        await _db.SaveChangesAsync();
        return Ok(new { ct.Id });
    }
}
