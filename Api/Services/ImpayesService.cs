using Microsoft.EntityFrameworkCore;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Services;

public class ImpayeDto
{
    public int ContratId { get; set; }
    public int ClientId { get; set; }
    public string ClientNom { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string ChantierNom { get; set; } = "";
    public string Lot { get; set; } = "";
    public decimal MontantEnRetard { get; set; }
    public int JoursRetard { get; set; }
    public DateTime? EcheanceDepuis { get; set; }
    public int NbRelances { get; set; }
    public DateTime? DerniereRelance { get; set; }
    public string Message { get; set; } = "";
    public string? WhatsApp { get; set; }
    public string? Sms { get; set; }
    public string? Mailto { get; set; }
}

/// <summary>
/// Détecte les contrats dont les tranches échues ne sont pas totalement payées.
/// Principe : retard = somme des tranches échues − total réellement encaissé (les paiements « libres » comptent).
/// </summary>
public class ImpayesService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public ImpayesService(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    private static string? Intl(string? tel, string indicatif)
    {
        if (string.IsNullOrWhiteSpace(tel)) return null;
        var d = new string(tel.Where(char.IsDigit).ToArray());
        if (d.Length == 0) return null;
        if (tel.TrimStart().StartsWith("+")) return d;
        if (d.StartsWith("00")) return d.Substring(2);
        if (d.StartsWith("0")) return indicatif + d.Substring(1);
        return d;
    }

    public async Task<List<ImpayeDto>> ListerAsync(int joursMin = 0)
    {
        var today = DateTime.Today;
        var societe = _cfg["Societe:Nom"] ?? "PromoImmo";
        var indicatif = _cfg["Societe:IndicatifPays"] ?? "213";

        var contrats = await _db.Contrats.AsNoTracking().Include(c => c.Client).Include(c => c.Chantier).Include(c => c.Echeances).ToListAsync();
        var paye = (await _db.Encaissements.Where(e => !e.Annule).GroupBy(e => e.ContratId)
            .Select(g => new { g.Key, T = g.Sum(x => x.Montant) }).ToListAsync()).ToDictionary(x => x.Key, x => x.T);
        var relances = (await _db.Relances.AsNoTracking().GroupBy(r => r.ContratId)
            .Select(g => new { g.Key, N = g.Count(), D = g.Max(x => x.Date) }).ToListAsync()).ToDictionary(x => x.Key);

        var res = new List<ImpayeDto>();
        foreach (var c in contrats)
        {
            var p = paye.TryGetValue(c.Id, out var pv) ? pv : 0m;
            var du = c.Echeances.Where(e => e.DatePrevue.Date < today).Sum(e => e.MontantPrevu);
            var retard = du - p;
            if (retard <= 0.009m) continue;

            decimal cum = 0; DateTime? plusAncienne = null;
            foreach (var e in c.Echeances.OrderBy(x => x.Numero))
            {
                cum += e.MontantPrevu;
                if (cum > p + 0.009m) { plusAncienne = e.DatePrevue.Date; break; }
            }
            var jours = plusAncienne == null ? 0 : (today - plusAncienne.Value).Days;
            if (jours < joursMin) continue;

            var msg = $"Bonjour {c.Client.Nom}, sauf erreur de notre part, un montant de {retard:N2} DA est en retard de paiement " +
                      $"pour votre bien {c.Lot} ({c.Chantier.Nom}), échu depuis le {plusAncienne:dd/MM/yyyy}. " +
                      $"Merci de bien vouloir régulariser votre situation auprès de notre service recette. Cordialement, {societe}.";
            var intl = Intl(c.Client.Telephone, indicatif);
            relances.TryGetValue(c.Id, out var rel);

            res.Add(new ImpayeDto
            {
                ContratId = c.Id, ClientId = c.ClientId, ClientNom = c.Client.Nom, Telephone = c.Client.Telephone, Email = c.Client.Email,
                ChantierNom = c.Chantier.Nom, Lot = c.Lot, MontantEnRetard = retard, JoursRetard = jours, EcheanceDepuis = plusAncienne,
                NbRelances = rel?.N ?? 0, DerniereRelance = rel?.D, Message = msg,
                WhatsApp = intl == null ? null : $"https://wa.me/{intl}?text={Uri.EscapeDataString(msg)}",
                Sms = string.IsNullOrWhiteSpace(c.Client.Telephone) ? null : $"sms:{c.Client.Telephone}?body={Uri.EscapeDataString(msg)}",
                Mailto = string.IsNullOrWhiteSpace(c.Client.Email) ? null
                    : $"mailto:{c.Client.Email}?subject={Uri.EscapeDataString("Rappel de paiement — " + societe)}&body={Uri.EscapeDataString(msg)}"
            });
        }
        return res.OrderByDescending(i => i.JoursRetard).ThenByDescending(i => i.MontantEnRetard).ToList();
    }
}
