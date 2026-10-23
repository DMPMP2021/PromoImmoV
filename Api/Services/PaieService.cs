using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Services;

public class PaieLigne
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string Poste { get; set; } = "";
    public decimal SalaireJournalier { get; set; }
    public int JoursPresents { get; set; }
    public int Absences { get; set; }
    public decimal Heures { get; set; }
    public decimal HeuresSup { get; set; }
    public decimal SalaireBase { get; set; }
    public decimal MontantHeuresSup { get; set; }
    public decimal Primes { get; set; }
    public decimal Brut { get; set; }
    public decimal CnasSalarie { get; set; }
    public decimal Irg { get; set; }
    public decimal Retenues { get; set; }
    public decimal Net { get; set; }
    public decimal CnasPatronale { get; set; }
    public decimal CoutEmployeur { get; set; }
    public decimal Verse { get; set; }
    public decimal Reste { get; set; }
}

/// <summary>
/// Paie mensuelle : jours de présence × salaire journalier + heures supplémentaires majorées + primes,
/// moins CNAS salarié et IRG. Tous les taux se règlent dans appsettings.json (section « Paie »)
/// et doivent être validés par votre comptable.
/// </summary>
public class PaieService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public PaieService(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public async Task<List<PaieLigne>> CalculerMoisAsync(DateTime debut, int? employeId = null)
    {
        var fin = debut.AddMonths(1);
        var heuresNormales = _cfg.GetValue("Paie:HeuresNormalesJour", 8m);
        var majoration = _cfg.GetValue("Paie:MajorationHeuresSup", 1.5m);
        var tSalarie = _cfg.GetValue("Paie:TauxCnasSalarie", 0.09m);
        var tEmployeur = _cfg.GetValue("Paie:TauxCnasEmployeur", 0.26m);
        var tIrg = _cfg.GetValue("Paie:TauxIrgForfait", 0m);

        var emps = await _db.Employes.AsNoTracking()
            .Where(e => employeId == null ? e.Actif : e.Id == employeId).OrderBy(e => e.Nom).ToListAsync();
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.Date >= debut && p.Date < fin).ToListAsync();
        var pay = await _db.PaiementsPersonnel.AsNoTracking().Where(p => p.Date >= debut && p.Date < fin).ToListAsync();

        var res = new List<PaieLigne>();
        foreach (var emp in emps)
        {
            var mp = pts.Where(p => p.EmployeId == emp.Id).ToList();
            var presents = mp.Where(p => p.Statut == StatutPointage.Present || p.Statut == StatutPointage.Retard).ToList();
            var hs = presents.Sum(p => Math.Max(0m, p.Heures - heuresNormales));
            var tauxHoraire = heuresNormales > 0 ? emp.SalaireJournalier / heuresNormales : 0m;

            var l = new PaieLigne
            {
                Id = emp.Id, Nom = emp.Nom, Poste = emp.Poste, SalaireJournalier = emp.SalaireJournalier,
                JoursPresents = presents.Count, Absences = mp.Count(p => p.Statut == StatutPointage.Absent),
                Heures = mp.Sum(p => p.Heures), HeuresSup = hs
            };
            l.SalaireBase = presents.Count * emp.SalaireJournalier;
            l.MontantHeuresSup = Math.Round(hs * tauxHoraire * majoration, 2);
            l.Primes = pay.Where(p => p.EmployeId == emp.Id && p.Type == TypePaiementPersonnel.Prime).Sum(p => p.Montant);
            l.Brut = l.SalaireBase + l.MontantHeuresSup + l.Primes;
            l.CnasSalarie = Math.Round(l.Brut * tSalarie, 2);
            l.Irg = Math.Round((l.Brut - l.CnasSalarie) * tIrg, 2);
            l.Retenues = l.CnasSalarie + l.Irg;
            l.Net = l.Brut - l.Retenues;
            l.CnasPatronale = Math.Round(l.Brut * tEmployeur, 2);
            l.CoutEmployeur = l.Brut + l.CnasPatronale;
            l.Verse = pay.Where(p => p.EmployeId == emp.Id).Sum(p => p.Montant);
            l.Reste = l.Net - l.Verse;
            res.Add(l);
        }
        return res;
    }
}
