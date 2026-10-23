using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.Globalization;
using PromoImmo.Domain;

namespace PromoImmo.Api.Controllers;

/// <summary>Exports PDF / Excel : ?format=pdf (défaut) ou ?format=xlsx</summary>
[ApiController, Route("api/exports"), Authorize]
public class ExportsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<AppUser> _users;
    private readonly IConfiguration _cfg;
    private readonly ImpayesService _impayes;
    private readonly RentabiliteService _renta;
    private readonly BrandingService _brand;

    public ExportsController(AppDbContext db, UserManager<AppUser> users, IConfiguration cfg, ImpayesService impayes, RentabiliteService renta, BrandingService brand)
    { _db = db; _users = users; _cfg = cfg; _impayes = impayes; _renta = renta; _brand = brand; }

    /// <summary>Produit le PDF / Excel avec logo, signature et QR code de vérification.</summary>
    private async Task<IActionResult> FichierAsync(string? format, string nom, string titre, string sous, List<ExportSection> sections, int? recuId = null)
    {
        var b = await _brand.ChargerAsync();
        var emisPar = User.Identity?.Name ?? "?";
        string code, url;
        if (recuId != null)
        {
            // Reçu : QR signé (HMAC) — vérifiable en ligne et qui signale un éventuel reçu annulé.
            var sig = _brand.Signer("recu", recuId.Value);
            code = sig.ToUpperInvariant();
            url = $"{b.UrlPublique}/verifier/recu/{recuId}/{sig}";
        }
        else
        {
            var emis = await _brand.EnregistrerAsync(nom, titre, sous, BrandingService.Empreinte(titre, sous, sections), emisPar);
            code = emis.Code;
            url = $"{b.UrlPublique}/verifier/doc/{emis.Code}";
        }
        var cachet = new CachetDocument
        {
            Branding = b, Qr = _brand.QrPng(url), Code = code, UrlVerification = url, EmisPar = emisPar,
            AvecSignature = !(nom.StartsWith("rentabilite") || nom.StartsWith("journal_audit"))   // documents internes : sans signature
        };
        var horo = DateTime.Now.ToString("yyyyMMdd");
        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
            return File(ExcelExport.Generer(titre, sous, cachet, sections),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{nom}_{horo}.xlsx");
        return File(PdfExport.Generer(titre, sous, cachet, sections), "application/pdf", $"{nom}_{horo}.pdf");
    }

    private static DateTime ParseMois(string? mois) =>
        DateTime.TryParseExact((mois ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    private static string LibPointage(StatutPointage s) => s switch
    {
        StatutPointage.Present => "Présent", StatutPointage.Retard => "Retard", StatutPointage.Absent => "Absent",
        StatutPointage.Conge => "Congé", _ => "Maladie"
    };

    // ---------------------------------------------------------------- Journal des encaissements
    [HttpGet("journal-encaissements"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Journal(DateTime? du, DateTime? au, string? format)
    {
        var d1 = (du ?? DateTime.Today).Date;
        var d2 = (au ?? d1).Date;
        var rows = await _db.Encaissements.AsNoTracking()
            .Where(e => e.Montant > 0 && e.DateEncaissement >= d1 && e.DateEncaissement < d2.AddDays(1))
            .OrderBy(e => e.DateEncaissement)
            .Select(e => new
            {
                e.DateEncaissement, e.NumeroRecu, Client = e.Contrat.Client.Nom, Chantier = e.Contrat.Chantier.Nom, e.Contrat.Lot,
                e.Mode, e.RefBancaire, e.AgentId, e.Montant, e.Annule
            }).ToListAsync();
        var agents = await _users.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.NomComplet);

        var s = new ExportSection { Titre = "Encaissements", Colonnes = new[] { "Date", "Reçu", "Client", "Chantier / Lot", "Mode", "Référence", "Agent", "Statut", "Montant" } };
        foreach (var r in rows)
            s.Lignes.Add(new object?[]
            {
                r.DateEncaissement.ToString("dd/MM/yyyy HH:mm"), r.NumeroRecu, r.Client, $"{r.Chantier} / {r.Lot}",
                r.Mode == ModePaiement.Especes ? "Espèces" : "Bancaire", r.RefBancaire, agents.GetValueOrDefault(r.AgentId, "?"),
                r.Annule ? "Annulé" : "Valide", r.Montant
            });
        var valides = rows.Where(r => !r.Annule).ToList();
        s.Total = new object?[] { "TOTAL (encaissements valides)", null, null, null, null, null, null, valides.Count, valides.Sum(r => r.Montant) };

        var s2 = new ExportSection { Titre = "Récapitulatif par mode de paiement", Colonnes = new[] { "Mode", "Nombre", "Montant" } };
        foreach (var g in valides.GroupBy(r => r.Mode))
            s2.Lignes.Add(new object?[] { g.Key == ModePaiement.Especes ? "Espèces" : "Bancaire", g.Count(), g.Sum(x => x.Montant) });

        var sous = d1 == d2 ? $"Journée du {d1:dd/MM/yyyy}" : $"Du {d1:dd/MM/yyyy} au {d2:dd/MM/yyyy}";
        return await FichierAsync(format, "journal_encaissements", "Journal des encaissements", sous, new List<ExportSection> { s, s2 });
    }

    // ---------------------------------------------------------------- Situation d'un client
    [HttpGet("situation-client/{clientId:int}"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Situation(int clientId, string? format)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId);
        if (client is null) return NotFound();
        var today = DateTime.Today;
        var contrats = await _db.Contrats.AsNoTracking().Include(c => c.Chantier).Include(c => c.Echeances)
            .Where(c => c.ClientId == clientId).OrderBy(c => c.Id).ToListAsync();
        var encs = await _db.Encaissements.AsNoTracking().Where(e => e.Contrat.ClientId == clientId && e.Montant > 0)
            .OrderBy(e => e.DateEncaissement)
            .Select(e => new { e.ContratId, e.DateEncaissement, e.NumeroRecu, e.Mode, e.RefBancaire, e.Montant, e.Annule }).ToListAsync();

        var sections = new List<ExportSection>();
        var resume = new ExportSection { Titre = "Résumé des contrats", Colonnes = new[] { "Chantier", "Lot", "Prix total", "Total payé", "Reste à payer" } };
        decimal sumPrix = 0, sumPaye = 0;
        foreach (var ct in contrats)
        {
            var paye = encs.Where(x => x.ContratId == ct.Id && !x.Annule).Sum(x => x.Montant);
            resume.Lignes.Add(new object?[] { ct.Chantier.Nom, ct.Lot, ct.PrixTotal, paye, ct.PrixTotal - paye });
            sumPrix += ct.PrixTotal; sumPaye += paye;
        }
        resume.Total = new object?[] { "TOTAL", null, sumPrix, sumPaye, sumPrix - sumPaye };
        sections.Add(resume);

        foreach (var ct in contrats)
        {
            var restant = encs.Where(x => x.ContratId == ct.Id && !x.Annule).Sum(x => x.Montant);
            var s = new ExportSection { Titre = $"Échéancier — {ct.Chantier.Nom} / {ct.Lot}", Colonnes = new[] { "N°", "Date prévue", "Montant prévu", "Payé", "Reste", "Situation" } };
            foreach (var ech in ct.Echeances.OrderBy(x => x.Numero))
            {
                var alloue = Math.Min(restant, ech.MontantPrevu);
                restant -= alloue;
                var reste = ech.MontantPrevu - alloue;
                var enRetard = ech.DatePrevue.Date < today;
                var situation = reste <= 0.009m ? "Soldée" : alloue > 0 ? (enRetard ? "Partielle — en retard" : "Partielle") : (enRetard ? "En retard" : "À venir");
                s.Lignes.Add(new object?[] { ech.Numero, ech.DatePrevue, ech.MontantPrevu, alloue, reste, situation });
            }
            sections.Add(s);
        }

        var hist = new ExportSection { Titre = "Historique des paiements", Colonnes = new[] { "Date", "Reçu", "Chantier / Lot", "Mode", "Référence", "Statut", "Montant" } };
        var libContrat = contrats.ToDictionary(x => x.Id, x => $"{x.Chantier.Nom} / {x.Lot}");
        foreach (var en in encs)
            hist.Lignes.Add(new object?[] { en.DateEncaissement.ToString("dd/MM/yyyy HH:mm"), en.NumeroRecu, libContrat.GetValueOrDefault(en.ContratId, ""),
                en.Mode == ModePaiement.Especes ? "Espèces" : "Bancaire", en.RefBancaire, en.Annule ? "Annulé" : "Valide", en.Montant });
        sections.Add(hist);

        var sous = $"Client : {client.Nom}" + (string.IsNullOrWhiteSpace(client.Telephone) ? "" : $" — Tél. {client.Telephone}") + $" — situation au {today:dd/MM/yyyy}";
        return await FichierAsync(format, $"situation_client_{clientId}", "Situation client", sous, sections);
    }

    // ---------------------------------------------------------------- Impayés
    [HttpGet("impayes"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Impayes(int joursMin, string? format)
    {
        var l = await _impayes.ListerAsync(joursMin);
        var s = new ExportSection { Titre = "Clients en retard de paiement", Colonnes = new[] { "Client", "Téléphone", "Chantier", "Lot", "Échu depuis", "Retard (jours)", "Relances", "Montant en retard" } };
        foreach (var i in l)
            s.Lignes.Add(new object?[] { i.ClientNom, i.Telephone, i.ChantierNom, i.Lot, i.EcheanceDepuis, i.JoursRetard, i.NbRelances, i.MontantEnRetard });
        s.Total = new object?[] { $"TOTAL ({l.Count} clients)", null, null, null, null, null, null, l.Sum(i => i.MontantEnRetard) };
        return await FichierAsync(format, "etat_impayes", "État des impayés", $"Situation au {DateTime.Today:dd/MM/yyyy}", new List<ExportSection> { s });
    }

    // ---------------------------------------------------------------- Obligations fiscales
    [HttpGet("obligations"), Authorize(Roles = "Gerant,Fiscal")]
    public async Task<IActionResult> Obligations(TypeObligation? type, StatutObligation? statut, string? format)
    {
        var today = DateTime.Today;
        var rows = await _db.Obligations.AsNoTracking()
            .Where(o => (type == null || o.Type == type) && (statut == null || o.Statut == statut))
            .OrderBy(o => o.DateEcheance).ToListAsync();
        var s = new ExportSection { Titre = "Obligations fiscales et parafiscales", Colonnes = new[] { "Échéance", "Nature", "Libellé", "Période", "Statut", "Référence de paiement", "Montant" } };
        foreach (var o in rows)
            s.Lignes.Add(new object?[]
            {
                o.DateEcheance, o.Type == TypeObligation.Fiscale ? "Fiscale" : "Parafiscale", o.Libelle, o.Periode,
                o.Statut == StatutObligation.Payee ? $"Payée le {o.DatePaiement:dd/MM/yyyy}" : (o.DateEcheance < today ? "EN RETARD" : "À payer"),
                o.Reference, o.Montant
            });
        var aPayer = rows.Where(o => o.Statut == StatutObligation.APayer).Sum(o => o.Montant);
        s.Total = new object?[] { "TOTAL À PAYER", null, null, null, null, null, aPayer };
        return await FichierAsync(format, "obligations_fiscales", "Obligations fiscales et parafiscales", $"Situation au {today:dd/MM/yyyy}", new List<ExportSection> { s });
    }

    // ---------------------------------------------------------------- Bulletin de paie
    [HttpGet("bulletin-paie/{employeId:int}"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> Bulletin(int employeId, string? mois, string? format)
    {
        var emp = await _db.Employes.AsNoTracking().Include(x => x.Chantier).FirstOrDefaultAsync(x => x.Id == employeId);
        if (emp is null) return NotFound();
        var debut = ParseMois(mois);
        var fin = debut.AddMonths(1);
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.EmployeId == employeId && p.Date >= debut && p.Date < fin).OrderBy(p => p.Date).ToListAsync();
        var pay = await _db.PaiementsPersonnel.AsNoTracking().Where(p => p.EmployeId == employeId && p.Date >= debut && p.Date < fin).OrderBy(p => p.Date).ToListAsync();

        var presents = pts.Where(p => p.Statut == StatutPointage.Present || p.Statut == StatutPointage.Retard).ToList();
        var jours = presents.Count;
        var heures = presents.Sum(p => p.Heures);
        var brut = jours * emp.SalaireJournalier;
        var salaires = pay.Where(p => p.Type == TypePaiementPersonnel.Salaire).Sum(p => p.Montant);
        var avances = pay.Where(p => p.Type == TypePaiementPersonnel.Avance).Sum(p => p.Montant);
        var primes = pay.Where(p => p.Type == TypePaiementPersonnel.Prime).Sum(p => p.Montant);

        var sections = new List<ExportSection>();
        var s1 = new ExportSection { Titre = "Employé", Colonnes = new[] { "Nom", "Poste", "Chantier d'affectation", "Salaire journalier" } };
        s1.Lignes.Add(new object?[] { emp.Nom, emp.Poste, emp.Chantier?.Nom, emp.SalaireJournalier });
        sections.Add(s1);

        var s2 = new ExportSection { Titre = "Calcul du mois", Colonnes = new[] { "Rubrique", "Montant" } };
        s2.Lignes.Add(new object?[] { $"Salaire de base estimé ({jours} jour(s) × {emp.SalaireJournalier:N2} DA — {heures:0.#} h)", brut });
        s2.Lignes.Add(new object?[] { "+ Primes", primes });
        s2.Lignes.Add(new object?[] { "= Total des gains", brut + primes });
        s2.Lignes.Add(new object?[] { "− Salaires déjà versés", salaires });
        s2.Lignes.Add(new object?[] { "− Avances déjà versées", avances });
        s2.Lignes.Add(new object?[] { "− Primes déjà versées", primes });
        s2.Total = new object?[] { "= RESTE À PAYER", brut + primes - salaires - avances - primes };
        sections.Add(s2);

        var s3 = new ExportSection { Titre = "Présences du mois", Colonnes = new[] { "Date", "Statut", "Heures", "Remarque" } };
        foreach (var p in pts) s3.Lignes.Add(new object?[] { p.Date, LibPointage(p.Statut), p.Heures, p.Remarque });
        sections.Add(s3);

        var s4 = new ExportSection { Titre = "Paiements du mois", Colonnes = new[] { "Date", "Type", "Période", "Motif", "Montant" } };
        foreach (var p in pay) s4.Lignes.Add(new object?[] { p.Date, p.Type.ToString(), p.Periode, p.Motif, p.Montant });
        sections.Add(s4);

        return await FichierAsync(format, $"bulletin_{emp.Nom.Replace(' ', '_')}_{debut:yyyyMM}", "Bulletin de paie", $"{emp.Nom} — {debut:MMMM yyyy}", sections);
    }

    // ---------------------------------------------------------------- Récapitulatif de paie (tous les employés)
    [HttpGet("recap-paie"), Authorize(Roles = "Gerant,Personnel")]
    public async Task<IActionResult> RecapPaie(string? mois, string? format)
    {
        var debut = ParseMois(mois);
        var fin = debut.AddMonths(1);
        var emps = await _db.Employes.AsNoTracking().Where(e => e.Actif).OrderBy(e => e.Nom).ToListAsync();
        var pts = await _db.Pointages.AsNoTracking().Where(p => p.Date >= debut && p.Date < fin).ToListAsync();
        var pay = await _db.PaiementsPersonnel.AsNoTracking().Where(p => p.Date >= debut && p.Date < fin).ToListAsync();

        var s = new ExportSection { Titre = "Récapitulatif mensuel du personnel", Colonnes = new[] { "Employé", "Poste", "Salaire/jour", "Jours présents", "Absences", "Heures", "Salaire estimé", "Payé", "Reste" } };
        decimal tEst = 0, tPaye = 0;
        foreach (var e in emps)
        {
            var mp = pts.Where(p => p.EmployeId == e.Id).ToList();
            var jours = mp.Count(p => p.Statut == StatutPointage.Present || p.Statut == StatutPointage.Retard);
            var du = jours * e.SalaireJournalier;
            var paye = pay.Where(p => p.EmployeId == e.Id).Sum(p => p.Montant);
            s.Lignes.Add(new object?[] { e.Nom, e.Poste, e.SalaireJournalier, jours, mp.Count(p => p.Statut == StatutPointage.Absent), mp.Sum(p => p.Heures), du, paye, du - paye });
            tEst += du; tPaye += paye;
        }
        s.Total = new object?[] { "TOTAL", null, null, null, null, null, tEst, tPaye, tEst - tPaye };
        return await FichierAsync(format, $"recap_paie_{debut:yyyyMM}", "Récapitulatif de paie", $"{debut:MMMM yyyy}", new List<ExportSection> { s });
    }

    // ---------------------------------------------------------------- Rentabilité par chantier
    [HttpGet("rentabilite"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Rentabilite(string? format)
    {
        var l = await _renta.CalculerAsync();
        var s = new ExportSection { Titre = "Rentabilité par chantier", Colonnes = new[] { "Chantier", "CA contractuel", "Encaissé", "Achats", "Main-d'œuvre", "Matériel", "Divers", "Dépensé", "Marge réelle", "Budget", "% consommé", "Alerte" } };
        foreach (var c in l)
            s.Lignes.Add(new object?[] { c.Nom, c.ChiffreAffaires, c.Encaisse - c.Rembourse, c.Achats, c.MainOeuvre, c.Materiel, c.Divers, c.Depense,
                c.MargeReelle, c.BudgetReference, c.TauxConsommation, c.Alerte == "Depassement" ? "DÉPASSEMENT" : c.Alerte == "Attention" ? "Attention" : c.Alerte == "Ok" ? "OK" : "Budget non défini" });
        s.Total = new object?[] { "TOTAL", l.Sum(c => c.ChiffreAffaires), l.Sum(c => c.Encaisse - c.Rembourse), l.Sum(c => c.Achats), l.Sum(c => c.MainOeuvre),
            l.Sum(c => c.Materiel), l.Sum(c => c.Divers), l.Sum(c => c.Depense), l.Sum(c => c.MargeReelle), null, null, null };
        return await FichierAsync(format, "rentabilite_chantiers", "Rentabilité par chantier", $"Situation au {DateTime.Today:dd/MM/yyyy} — encaissé net des remboursements", new List<ExportSection> { s });
    }

    // ---------------------------------------------------------------- Journal d'audit
    [HttpGet("journal-audit"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Audit(DateTime? du, DateTime? au, string? format)
    {
        var d1 = (du ?? DateTime.Today.AddDays(-7)).Date;
        var d2 = (au ?? DateTime.Today).Date;
        var rows = await _db.AuditLogs.AsNoTracking().Where(a => a.Date >= d1 && a.Date < d2.AddDays(1))
            .OrderByDescending(a => a.Date).Take(5000).ToListAsync();
        var s = new ExportSection { Titre = "Journal d'audit", Colonnes = new[] { "Date", "Utilisateur", "Action", "Élément", "N°", "Détails", "IP" } };
        foreach (var a in rows)
            s.Lignes.Add(new object?[] { a.Date.ToString("dd/MM/yyyy HH:mm:ss"), a.UserName, a.Action, a.Entite, a.EntiteId, a.Details, a.Ip });
        return await FichierAsync(format, "journal_audit", "Journal d'audit", $"Du {d1:dd/MM/yyyy} au {d2:dd/MM/yyyy}", new List<ExportSection> { s });
    }

    // ---------------------------------------------------------------- Reçu de paiement officiel (PDF)
    [HttpGet("recu/{id:int}"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> RecuPdf(int id)
    {
        var e = await _db.Encaissements.AsNoTracking().Include(x => x.Contrat).ThenInclude(c => c.Client)
            .Include(x => x.Contrat).ThenInclude(c => c.Chantier).FirstOrDefaultAsync(x => x.Id == id && x.Montant > 0);
        if (e is null) return NotFound();
        var paye = await _db.Encaissements.Where(x => x.ContratId == e.ContratId && !x.Annule).SumAsync(x => (decimal?)x.Montant) ?? 0m;
        var agent = await _users.FindByIdAsync(e.AgentId);

        var s = new ExportSection { Titre = "Détail du paiement", Colonnes = new[] { "Désignation", "Valeur" } };
        s.Lignes.Add(new object?[] { "Reçu n°", e.NumeroRecu });
        s.Lignes.Add(new object?[] { "Date du paiement", e.DateEncaissement.ToString("dd/MM/yyyy HH:mm") });
        s.Lignes.Add(new object?[] { "Reçu de", e.Contrat.Client.Nom });
        s.Lignes.Add(new object?[] { "Chantier / Lot", $"{e.Contrat.Chantier.Nom} / {e.Contrat.Lot}" });
        s.Lignes.Add(new object?[] { "Mode de paiement", e.Mode == ModePaiement.Especes ? "Espèces" : "Bancaire" });
        if (!string.IsNullOrWhiteSpace(e.RefBancaire)) s.Lignes.Add(new object?[] { "Référence", e.RefBancaire });
        s.Lignes.Add(new object?[] { "Prix total du bien", e.Contrat.PrixTotal });
        s.Lignes.Add(new object?[] { "Total payé à ce jour", paye });
        s.Lignes.Add(new object?[] { "Reste à payer", e.Contrat.PrixTotal - paye });
        s.Lignes.Add(new object?[] { "Encaissé par", agent?.NomComplet });
        s.Total = new object?[] { "MONTANT ENCAISSÉ", e.Montant };

        var sous = $"N° {e.NumeroRecu} — {e.DateEncaissement:dd/MM/yyyy}" + (e.Annule ? "  —  REÇU ANNULÉ" : "");
        return await FichierAsync("pdf", $"recu_{e.NumeroRecu}", "Reçu de paiement", sous, new List<ExportSection> { s }, e.Id);
    }

    // ---------------------------------------------------------------- Aperçu de la mise en page (logo, signature, QR)
    [HttpGet("apercu"), Authorize(Roles = "Gerant")]
    public async Task<IActionResult> Apercu()
    {
        var s = new ExportSection { Titre = "Exemple de tableau", Colonnes = new[] { "Désignation", "Quantité", "Montant" } };
        s.Lignes.Add(new object?[] { "Ligne d'exemple 1", 2, 150000m });
        s.Lignes.Add(new object?[] { "Ligne d'exemple 2", 1, 180000m });
        s.Total = new object?[] { "TOTAL", null, 330000m };
        return await FichierAsync("pdf", "apercu_document", "Aperçu d'un document officiel", "Ce document sert uniquement à contrôler la mise en page.", new List<ExportSection> { s });
    }
}
