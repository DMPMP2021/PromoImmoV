using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Services;

public record AlerteDto(string Niveau, string Message, string Lien);

/// <summary>Calcule les alertes de gestion et les transforme en notifications pour le gérant.</summary>
public class RappelsService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly ImpayesService _impayes;
    private readonly RentabiliteService _renta;
    private readonly Notifier _notif;

    public RappelsService(AppDbContext db, IConfiguration cfg, ImpayesService impayes, RentabiliteService renta, Notifier notif)
    { _db = db; _cfg = cfg; _impayes = impayes; _renta = renta; _notif = notif; }

    public async Task<List<AlerteDto>> CalculerAsync()
    {
        var today = DateTime.Today;
        var l = new List<AlerteDto>();

        // 1. Impayés
        var imp = await _impayes.ListerAsync();
        if (imp.Count > 0)
        {
            var plus30 = imp.Count(i => i.JoursRetard > 30);
            l.Add(new AlerteDto(plus30 > 0 ? "danger" : "warn",
                $"{imp.Count} client(s) en retard de paiement — {imp.Sum(i => i.MontantEnRetard):N0} DA" + (plus30 > 0 ? $" dont {plus30} depuis plus de 30 jours" : ""), "/impayes"));
        }

        // 2. Obligations fiscales / parafiscales
        var jours = _cfg.GetValue("Alertes:JoursFiscal", 7);
        var obl = await _db.Obligations.AsNoTracking().Where(o => o.Statut == StatutObligation.APayer)
            .Select(o => new { o.Montant, o.DateEcheance }).ToListAsync();
        var retardF = obl.Where(o => o.DateEcheance < today).ToList();
        var bientot = obl.Where(o => o.DateEcheance >= today && o.DateEcheance <= today.AddDays(jours)).ToList();
        if (retardF.Count > 0) l.Add(new AlerteDto("danger", $"{retardF.Count} obligation(s) fiscale(s)/parafiscale(s) en retard — {retardF.Sum(o => o.Montant):N0} DA", "/fiscal"));
        if (bientot.Count > 0) l.Add(new AlerteDto("warn", $"{bientot.Count} obligation(s) fiscale(s)/parafiscale(s) à payer d'ici {jours} jours — {bientot.Sum(o => o.Montant):N0} DA", "/fiscal"));

        // 3. Chantiers qui dépassent (ou approchent) leur budget
        foreach (var c in await _renta.CalculerAsync())
        {
            if (c.Alerte == "Depassement")
                l.Add(new AlerteDto("danger", $"Chantier « {c.Nom} » : dépenses {c.Depense:N0} DA > budget {c.BudgetReference:N0} DA ({c.TauxConsommation:0.#} %)", "/rentabilite"));
            else if (c.Alerte == "Attention")
                l.Add(new AlerteDto("warn", $"Chantier « {c.Nom} » : {c.TauxConsommation:0.#} % du budget consommé", "/rentabilite"));
        }

        // 4. Lots : réservations expirées
        var exp = await _db.Lots.CountAsync(x => x.Statut == StatutLot.Reserve && x.DateFinReservation != null && x.DateFinReservation < today);
        if (exp > 0) l.Add(new AlerteDto("warn", $"{exp} réservation(s) de lot expirée(s) à libérer ou à transformer en contrat", "/lots"));

        // 5. Éléments en attente de décision
        var cmd = await _db.Commandes.CountAsync(c => c.Statut == StatutCommande.AValider);
        if (cmd > 0) l.Add(new AlerteDto("warn", $"{cmd} commande(s) d'achat à valider", "/achats"));
        var rb = await _db.Remboursements.CountAsync(r => r.Statut == StatutRemboursement.Demandee);
        if (rb > 0) l.Add(new AlerteDto("warn", $"{rb} demande(s) de remboursement à valider", "/remboursements"));
        var rc = await _db.Reclamations.CountAsync(r => r.Statut == StatutReclamation.Nouvelle);
        if (rc > 0) l.Add(new AlerteDto("warn", $"{rc} réclamation(s) client non traitée(s)", "/reclamations"));

        // 6. Sauvegarde
        if (Sauvegarde.DernierOk == false)
            l.Add(new AlerteDto("danger", "La dernière sauvegarde de la base a échoué : " + Sauvegarde.DernierMessage, "/securite"));

        return l;
    }

    /// <summary>Crée des notifications (dédoublonnées sur 20 h). Retourne le nombre créé.</summary>
    public async Task<int> GenererAsync()
    {
        var n = 0;
        foreach (var a in await CalculerAsync())
            if (await _notif.GerantsUniqueAsync("ALERTE", "⚠ " + a.Message, TimeSpan.FromHours(20))) n++;
        return n;
    }
}
