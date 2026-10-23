using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Services;

public class RentabiliteLigne
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string Statut { get; set; } = "";
    public decimal ChiffreAffaires { get; set; }
    public decimal Encaisse { get; set; }
    public decimal Rembourse { get; set; }
    public decimal BudgetPrevu { get; set; }
    public decimal Alloue { get; set; }
    public decimal BudgetReference { get; set; }
    public decimal Achats { get; set; }
    public decimal MainOeuvre { get; set; }
    public decimal Materiel { get; set; }
    public decimal Divers { get; set; }
    public decimal Depense { get; set; }
    public decimal MargeReelle { get; set; }
    public decimal MargePrevisionnelle { get; set; }
    public decimal TauxConsommation { get; set; }
    /// <summary>NonDefini | Ok | Attention (≥ 80 %) | Depassement (&gt; 100 %)</summary>
    public string Alerte { get; set; } = "NonDefini";
}

/// <summary>Calcule, chantier par chantier, ce qui est encaissé, dépensé et la marge.</summary>
public class RentabiliteService
{
    private readonly AppDbContext _db;
    public RentabiliteService(AppDbContext db) => _db = db;

    private static decimal G(Dictionary<int, decimal> d, int id) => d.TryGetValue(id, out var v) ? v : 0m;

    public async Task<List<RentabiliteLigne>> CalculerAsync()
    {
        var chantiers = await _db.Chantiers.AsNoTracking().OrderBy(c => c.Nom).ToListAsync();

        var enc = (await _db.Encaissements.Where(e => !e.Annule).GroupBy(e => e.Contrat.ChantierId)
            .Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var remb = (await _db.Remboursements.Where(r => r.Statut == StatutRemboursement.Payee).GroupBy(r => r.Contrat.ChantierId)
            .Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var prix = (await _db.Contrats.GroupBy(c => c.ChantierId)
            .Select(g => new { g.Key, T = g.Sum(x => x.PrixTotal) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var alloc = (await _db.Allocations.Where(a => a.ChantierId != null).GroupBy(a => a.ChantierId!.Value)
            .Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var achats = (await _db.Commandes.Where(c => c.ChantierId != null &&
                (c.Statut == StatutCommande.Commandee || c.Statut == StatutCommande.Livree || c.Statut == StatutCommande.Payee))
            .GroupBy(c => c.ChantierId!.Value).Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var divers = (await _db.Depenses.Where(d => d.ChantierId != null).GroupBy(d => d.ChantierId!.Value)
            .Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var materiel = (await _db.PaiementsMateriel.Where(p => p.ChantierId != null).GroupBy(p => p.ChantierId!.Value)
            .Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);

        // Main-d'œuvre = jours de présence pointés sur le chantier × salaire journalier de l'employé
        var mo = (await _db.Pointages.Where(p => p.ChantierId != null && (p.Statut == StatutPointage.Present || p.Statut == StatutPointage.Retard))
            .Select(p => new { ChantierId = p.ChantierId!.Value, p.Employe.SalaireJournalier }).ToListAsync())
            .GroupBy(x => x.ChantierId).ToDictionary(g => g.Key, g => g.Sum(x => x.SalaireJournalier));

        var res = new List<RentabiliteLigne>();
        foreach (var c in chantiers)
        {
            var l = new RentabiliteLigne
            {
                Id = c.Id, Nom = c.Nom, Statut = c.Statut.ToString(),
                ChiffreAffaires = G(prix, c.Id), Encaisse = G(enc, c.Id), Rembourse = G(remb, c.Id),
                BudgetPrevu = c.BudgetPrevu, Alloue = G(alloc, c.Id),
                Achats = G(achats, c.Id), MainOeuvre = G(mo, c.Id), Materiel = G(materiel, c.Id), Divers = G(divers, c.Id)
            };
            l.BudgetReference = l.BudgetPrevu > 0 ? l.BudgetPrevu : l.Alloue;
            l.Depense = l.Achats + l.MainOeuvre + l.Materiel + l.Divers;
            l.MargeReelle = l.Encaisse - l.Rembourse - l.Depense;
            l.MargePrevisionnelle = l.ChiffreAffaires - l.BudgetReference;
            if (l.BudgetReference > 0)
            {
                l.TauxConsommation = Math.Round(l.Depense * 100m / l.BudgetReference, 1);
                l.Alerte = l.Depense > l.BudgetReference ? "Depassement" : l.TauxConsommation >= 80m ? "Attention" : "Ok";
            }
            res.Add(l);
        }
        return res;
    }
}
