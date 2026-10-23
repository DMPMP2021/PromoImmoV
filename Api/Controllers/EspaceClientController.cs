using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;

namespace PromoImmo.Api.Controllers;

public class ReclamationClientDto
{
    [Required, StringLength(150)] public string Objet { get; set; } = "";
    [Required, StringLength(3000)] public string Message { get; set; } = "";
    [StringLength(100)] public string? ContratRef { get; set; }
}

/// <summary>Espace réservé aux clients : ils ne voient QUE leurs propres données.</summary>
[ApiController, Route("api/espace-client"), Authorize(Roles = "Client")]
public class EspaceClientController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Notifier _notif;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;
    public EspaceClientController(AppDbContext db, Notifier notif, IConfiguration cfg, IWebHostEnvironment env)
    { _db = db; _notif = notif; _cfg = cfg; _env = env; }

    private int ClientId => int.TryParse(User.FindFirstValue("clientId"), out var id) ? id : 0;

    [HttpGet("moi")]
    public async Task<IActionResult> Moi()
    {
        var cid = ClientId;
        if (cid == 0) return Forbid();
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cid);
        if (client is null) return NotFound();
        var today = DateTime.Today;

        var contrats = await _db.Contrats.AsNoTracking().Include(c => c.Chantier).Include(c => c.Echeances)
            .Where(c => c.ClientId == cid).OrderBy(c => c.Id).ToListAsync();
        var encs = await _db.Encaissements.AsNoTracking()
            .Where(e => e.Contrat.ClientId == cid && e.Montant > 0 && !e.Annule).OrderBy(e => e.DateEncaissement)
            .Select(e => new { e.Id, e.ContratId, e.DateEncaissement, e.NumeroRecu, e.Mode, e.Montant }).ToListAsync();

        var liste = contrats.Select(c =>
        {
            var paye = encs.Where(x => x.ContratId == c.Id).Sum(x => x.Montant);
            var restant = paye;
            var echeances = c.Echeances.OrderBy(x => x.Numero).Select(x =>
            {
                var alloue = Math.Min(restant, x.MontantPrevu);
                restant -= alloue;
                var reste = x.MontantPrevu - alloue;
                var retard = x.DatePrevue.Date < today;
                return new
                {
                    x.Numero, x.DatePrevue, x.MontantPrevu, Paye = alloue, Reste = reste,
                    Situation = reste <= 0.009m ? "Soldée" : alloue > 0 ? (retard ? "Partielle — en retard" : "Partielle") : (retard ? "En retard" : "À venir")
                };
            }).ToList();
            return new
            {
                c.Id, Chantier = c.Chantier.Nom, c.Lot, c.PrixTotal, TotalPaye = paye, Reste = c.PrixTotal - paye, Echeances = echeances,
                Paiements = encs.Where(x => x.ContratId == c.Id)
                    .Select(x => new { x.Id, x.DateEncaissement, x.NumeroRecu, Mode = x.Mode == ModePaiement.Especes ? "Espèces" : "Bancaire", x.Montant }).ToList()
            };
        }).ToList();

        return Ok(new { client.Nom, Contrats = liste });
    }

    [HttpGet("avancement")]
    public async Task<IActionResult> Avancement()
    {
        var cid = ClientId;
        var chantierIds = await _db.Contrats.Where(c => c.ClientId == cid).Select(c => c.ChantierId).Distinct().ToListAsync();
        var chantiers = await _db.Chantiers.AsNoTracking().Where(c => chantierIds.Contains(c.Id)).ToListAsync();
        var av = await _db.Avancements.AsNoTracking().Where(a => chantierIds.Contains(a.ChantierId))
            .OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).ToListAsync();
        return Ok(chantiers.Select(c =>
        {
            var l = av.Where(a => a.ChantierId == c.Id).ToList();
            var d = l.FirstOrDefault();
            return new
            {
                c.Nom, c.Localisation, Pourcentage = d != null ? d.Pourcentage : 0, Phase = d?.Phase, DerniereMaj = d?.Date,
                Historique = l.Take(8).Select(a => new { a.Date, a.Phase, a.Pourcentage }).ToList()
            };
        }));
    }

    [HttpGet("documents")]
    public async Task<IActionResult> Documents()
    {
        var cid = ClientId;
        var contratIds = await _db.Contrats.Where(c => c.ClientId == cid).Select(c => c.Id).ToListAsync();
        return Ok(await _db.Documents.AsNoTracking()
            .Where(d => d.VisibleClient && ((d.Cible == "Client" && d.CibleId == cid) || (d.Cible == "Contrat" && contratIds.Contains(d.CibleId))))
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.NomFichier, d.Categorie, d.Description, d.Taille, d.CreatedAt }).ToListAsync());
    }

    [HttpGet("documents/{id:int}/fichier")]
    public async Task<IActionResult> Telecharger(int id)
    {
        var cid = ClientId;
        var contratIds = await _db.Contrats.Where(c => c.ClientId == cid).Select(c => c.Id).ToListAsync();
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.VisibleClient &&
            ((x.Cible == "Client" && x.CibleId == cid) || (x.Cible == "Contrat" && contratIds.Contains(x.CibleId))));
        if (d is null) return NotFound();
        var chemin = Path.Combine(DocumentsController.DossierFichiers(_cfg, _env), d.Chemin);
        if (!System.IO.File.Exists(chemin)) return NotFound();
        return PhysicalFile(chemin, d.TypeMime, d.NomFichier);
    }

    [HttpGet("reclamations")]
    public async Task<IActionResult> Reclamations() =>
        Ok(await _db.Reclamations.AsNoTracking().Where(r => r.ClientId == ClientId).OrderByDescending(r => r.DateCreation)
            .Select(r => new { r.Id, r.Objet, r.Message, r.Statut, r.Reponse, r.DateCreation, r.DateTraitement }).ToListAsync());

    [HttpPost("reclamations")]
    public async Task<IActionResult> NouvelleReclamation(ReclamationClientDto dto)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ClientId);
        if (client is null) return Forbid();
        var r = new Reclamation { ClientId = client.Id, Nom = client.Nom, Telephone = client.Telephone, Email = client.Email,
            ContratRef = dto.ContratRef, Objet = dto.Objet.Trim(), Message = dto.Message.Trim() };
        _db.Reclamations.Add(r);
        await _db.SaveChangesAsync();
        await _notif.GerantsAsync("RECLAMATION", $"Nouvelle réclamation (espace client) de {r.Nom} : {r.Objet}", r.Id);
        return Ok(new { numero = $"RCL-{r.Id:D5}" });
    }

    [HttpGet("recu/{id:int}")]
    public async Task<IActionResult> Recu(int id)
    {
        var e = await _db.Encaissements.AsNoTracking().Include(x => x.Contrat).ThenInclude(c => c.Client)
            .Include(x => x.Contrat).ThenInclude(c => c.Chantier)
            .FirstOrDefaultAsync(x => x.Id == id && x.Contrat.ClientId == ClientId && x.Montant > 0 && !x.Annule);
        if (e is null) return NotFound();
        var paye = await _db.Encaissements.Where(x => x.ContratId == e.ContratId && !x.Annule).SumAsync(x => (decimal?)x.Montant) ?? 0m;
        return Ok(new
        {
            e.NumeroRecu, Date = e.DateEncaissement, Client = e.Contrat.Client.Nom, Chantier = e.Contrat.Chantier.Nom, e.Contrat.Lot, e.Montant,
            Mode = e.Mode.ToString(), e.RefBancaire, Agent = "PromoImmo", TotalPaye = paye, Reste = e.Contrat.PrixTotal - paye, e.Contrat.PrixTotal
        });
    }
}

/// <summary>Création / réinitialisation du compte « espace client » d'un client (par le personnel).</summary>
[ApiController, Route("api/clients"), Authorize(Roles = "Gerant,Recette")]
public class PortailAdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    public PortailAdminController(AppDbContext db, UserManager<AppUser> users) { _db = db; _users = users; }

    private static string MotDePasse()
    {
        const string maj = "ABCDEFGHJKLMNPQRSTUVWXYZ", min = "abcdefghijkmnpqrstuvwxyz", chf = "23456789", sym = "@#$%";
        string Tirer(string s, int n) => new string(Enumerable.Range(0, n).Select(_ => s[RandomNumberGenerator.GetInt32(s.Length)]).ToArray());
        var chars = (Tirer(maj, 3) + Tirer(min, 4) + Tirer(chf, 3) + Tirer(sym, 1)).ToCharArray();
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    [HttpPost("{id:int}/compte-portail")]
    public async Task<IActionResult> Compte(int id)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (client is null) return NotFound();
        var login = $"client{id}";
        var mdp = MotDePasse();
        var u = await _users.FindByNameAsync(login);
        if (u is null)
        {
            u = new AppUser { UserName = login, NomComplet = client.Nom, ClientId = id, EmailConfirmed = true, Email = client.Email ?? $"{login}@promoimmo.local" };
            var cr = await _users.CreateAsync(u, mdp);
            if (!cr.Succeeded) return BadRequest(new { message = string.Join(" ", cr.Errors.Select(e => e.Description)) });
            await _users.AddToRoleAsync(u, "Client");
        }
        else
        {
            var token = await _users.GeneratePasswordResetTokenAsync(u);
            var rr = await _users.ResetPasswordAsync(u, token, mdp);
            if (!rr.Succeeded) return BadRequest(new { message = string.Join(" ", rr.Errors.Select(e => e.Description)) });
            await _users.SetLockoutEndDateAsync(u, null);
        }
        return Ok(new { identifiant = login, motDePasse = mdp });
    }
}
