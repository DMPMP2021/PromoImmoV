using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Controllers;

[ApiController, Route("api")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly BrandingService _brand;
    public DashboardController(AppDbContext db, UserManager<AppUser> users, BrandingService brand) { _db = db; _users = users; _brand = brand; }

    [HttpGet("dashboard"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Dashboard()
    {
        var encaisse = await _db.Encaissements.Where(e => !e.Annule).SumAsync(e => (decimal?)e.Montant) ?? 0m;
        var alloue = await _db.Allocations.SumAsync(a => (decimal?)a.Montant) ?? 0m;
        var depMat = await _db.PaiementsMateriel.SumAsync(p => (decimal?)p.Montant) ?? 0m;
        var prixTotal = await _db.Contrats.SumAsync(c => (decimal?)c.PrixTotal) ?? 0m;
        var remb = await _db.Remboursements.Where(r => r.Statut == StatutRemboursement.Payee).SumAsync(r => (decimal?)r.Montant) ?? 0m;

        var encByCh = await _db.Encaissements.Where(e => !e.Annule)
            .GroupBy(e => e.Contrat.ChantierId).Select(g => new { Id = g.Key, T = g.Sum(x => x.Montant) }).ToListAsync();
        var allocByCh = await _db.Allocations.Where(a => a.ChantierId != null)
            .GroupBy(a => a.ChantierId).Select(g => new { Id = g.Key, T = g.Sum(x => x.Montant) }).ToListAsync();
        var chantiers = await _db.Chantiers.AsNoTracking().ToListAsync();

        var parService = await _db.Allocations.Where(a => a.Service != null)
            .GroupBy(a => a.Service).Select(g => new { Service = g.Key, Montant = g.Sum(x => x.Montant) }).ToListAsync();

        return Ok(new
        {
            TotalEncaisse = encaisse, TotalAlloue = alloue, RemboursementsPayes = remb,
            Tresorerie = encaisse - remb - alloue,
            DepensesMateriel = depMat, ResteACollecter = prixTotal - encaisse,
            ParChantier = chantiers.Select(c => new
            {
                c.Id, c.Nom,
                Encaisse = encByCh.FirstOrDefault(x => x.Id == c.Id)?.T ?? 0m,
                Alloue = allocByCh.FirstOrDefault(x => x.Id == c.Id)?.T ?? 0m
            }),
            ParService = parService
        });
    }

    /// <summary>Vue d'ensemble de tous les services pour le gérant.</summary>
    [HttpGet("dashboard/global"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Global()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var taches = await _db.Taches.AsNoTracking().Select(t => new { t.Service, t.Statut, t.DateEcheance }).ToListAsync();
        var parService = Enum.GetValues<ServiceCode>().Select(s =>
        {
            var l = taches.Where(t => t.Service == s).ToList();
            return new
            {
                Service = s.ToString(), Total = l.Count,
                APrevoir = l.Count(x => x.Statut == StatutTache.APrevoir), EnCours = l.Count(x => x.Statut == StatutTache.EnCours),
                Termine = l.Count(x => x.Statut == StatutTache.Termine), Bloque = l.Count(x => x.Statut == StatutTache.Bloque),
                EnRetard = l.Count(x => x.Statut != StatutTache.Termine && x.DateEcheance != null && x.DateEcheance < today)
            };
        }).ToList();
        var reclNouv = await _db.Reclamations.CountAsync(r => r.Statut == StatutReclamation.Nouvelle);
        var reclEnCours = await _db.Reclamations.CountAsync(r => r.Statut == StatutReclamation.EnCours);

        var rembs = await _db.Remboursements.AsNoTracking().Select(r => new { r.Statut, r.Montant }).ToListAsync();
        var cmds = await _db.Commandes.AsNoTracking().Select(c => new { c.Statut, c.Montant }).ToListAsync();
        var oblig = await _db.Obligations.AsNoTracking().Select(o => new { o.Statut, o.Montant, o.DateEcheance }).ToListAsync();

        var effectif = await _db.Employes.CountAsync(e => e.Actif);
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.Date == today).Select(p => p.Statut).ToListAsync();
        var missions = await _db.Missions.CountAsync(m => m.DateDebut <= today && m.DateFin >= today &&
            m.Statut != StatutMission.Annulee && m.Statut != StatutMission.Terminee);
        var paieMois = await _db.PaiementsPersonnel.Where(p => p.Date >= monthStart).SumAsync(p => (decimal?)p.Montant) ?? 0m;
        var divers = await _db.Depenses.SumAsync(d => (decimal?)d.Montant) ?? 0m;

        var av = (await _db.Avancements.AsNoTracking().OrderByDescending(a => a.Date).ThenByDescending(a => a.Id).ToListAsync())
            .GroupBy(a => a.ChantierId).ToDictionary(g => g.Key, g => g.First());
        var chantiers = await _db.Chantiers.AsNoTracking().OrderBy(c => c.Nom).ToListAsync();

        return Ok(new
        {
            ParService = parService,
            ReclamationsNouvelles = reclNouv, ReclamationsEnCours = reclEnCours,
            RemboursementsAValider = rembs.Count(r => r.Statut == StatutRemboursement.Demandee),
            RemboursementsAPayer = rembs.Count(r => r.Statut == StatutRemboursement.Validee),
            MontantRemboursementsAPayer = rembs.Where(r => r.Statut == StatutRemboursement.Validee).Sum(r => r.Montant),
            CommandesEnCours = cmds.Count(c => c.Statut == StatutCommande.Demandee || c.Statut == StatutCommande.Commandee),
            MontantCommandesEnCours = cmds.Where(c => c.Statut == StatutCommande.Demandee || c.Statut == StatutCommande.Commandee).Sum(c => c.Montant),
            AchatsTotal = cmds.Where(c => c.Statut != StatutCommande.Annulee).Sum(c => c.Montant),
            ObligationsAPayer = oblig.Count(o => o.Statut == StatutObligation.APayer),
            MontantObligationsAPayer = oblig.Where(o => o.Statut == StatutObligation.APayer).Sum(o => o.Montant),
            ObligationsEnRetard = oblig.Count(o => o.Statut == StatutObligation.APayer && o.DateEcheance < today),
            ObligationsSous7Jours = oblig.Count(o => o.Statut == StatutObligation.APayer && o.DateEcheance >= today && o.DateEcheance <= today.AddDays(7)),
            EffectifActif = effectif,
            PresentsAujourdhui = pts.Count(s => s == StatutPointage.Present || s == StatutPointage.Retard),
            AbsentsAujourdhui = pts.Count(s => s != StatutPointage.Present && s != StatutPointage.Retard),
            NonPointes = Math.Max(0, effectif - pts.Count),
            MissionsAujourdhui = missions,
            PaiementsPersonnelMois = paieMois,
            DepensesDiverses = divers,
            Avancement = chantiers.Select(c => new { c.Id, c.Nom, Pourcentage = av.TryGetValue(c.Id, out var a) ? a.Pourcentage : 0, Phase = av.TryGetValue(c.Id, out var b) ? b.Phase : null })
        });
    }

    [HttpGet("recette/recents"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Recents() =>
        Ok(await _db.Encaissements.AsNoTracking().Where(e => e.Montant > 0)
            .OrderByDescending(e => e.DateEncaissement).Take(30)
            .Select(e => new { e.Id, e.NumeroRecu, e.Montant, Mode = e.Mode.ToString(), e.Annule,
                ClientNom = e.Contrat.Client.Nom, ChantierNom = e.Contrat.Chantier.Nom, Date = e.DateEncaissement })
            .ToListAsync());

    [HttpGet("encaissements/{id:int}/recu"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Recu(int id)
    {
        var e = await _db.Encaissements.AsNoTracking().Include(x => x.Contrat).ThenInclude(c => c.Client)
            .Include(x => x.Contrat).ThenInclude(c => c.Chantier).FirstOrDefaultAsync(x => x.Id == id);
        if (e is null) return NotFound();
        var paye = await _db.Encaissements.Where(x => x.ContratId == e.ContratId && !x.Annule).SumAsync(x => (decimal?)x.Montant) ?? 0m;
        var agent = await _users.FindByIdAsync(e.AgentId);
        return Ok(new
        {
            e.NumeroRecu, Date = e.DateEncaissement, Client = e.Contrat.Client.Nom, Chantier = e.Contrat.Chantier.Nom,
            e.Contrat.Lot, e.Montant, Mode = e.Mode.ToString(), e.RefBancaire, Agent = agent?.NomComplet,
            TotalPaye = paye, Reste = e.Contrat.PrixTotal - paye, e.Contrat.PrixTotal,
            Branding = await _brand.RecuAsync(e.Id)
        });
    }
}
