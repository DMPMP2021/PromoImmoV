using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Controllers;

/// <summary>Vérification publique (sans connexion) de l'authenticité d'un document via son QR code.</summary>
[ApiController, Route("api/verification"), AllowAnonymous]
public class VerificationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BrandingService _brand;
    public VerificationController(AppDbContext db, BrandingService brand) { _db = db; _brand = brand; }

    /// <summary>« Ahmed Benali » → « A**** B***** » : on ne divulgue pas l'identité complète du client.</summary>
    private static string Masquer(string nom) =>
        string.Join(' ', nom.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(m => m.Length <= 1 ? m : m.Substring(0, 1) + new string('*', Math.Min(m.Length - 1, 5))));

    [HttpGet("recu/{id:int}/{sig}")]
    public async Task<IActionResult> Recu(int id, string sig)
    {
        if (!_brand.Verifier("recu", id, sig)) return Ok(new { valide = false });
        var e = await _db.Encaissements.AsNoTracking().Include(x => x.Contrat).ThenInclude(c => c.Client)
            .Include(x => x.Contrat).ThenInclude(c => c.Chantier).FirstOrDefaultAsync(x => x.Id == id && x.Montant > 0);
        if (e is null) return Ok(new { valide = false });
        var b = await _brand.ChargerAsync();
        return Ok(new
        {
            valide = true, type = "Reçu de paiement", societe = b.Nom, numero = e.NumeroRecu, date = e.DateEncaissement,
            montant = e.Montant, client = Masquer(e.Contrat.Client.Nom), chantier = e.Contrat.Chantier.Nom, annule = e.Annule
        });
    }

    [HttpGet("doc/{code}")]
    public async Task<IActionResult> Doc(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 20) return Ok(new { valide = false });
        var c = code.Trim().ToUpperInvariant();
        var d = await _db.DocumentsEmis.AsNoTracking().FirstOrDefaultAsync(x => x.Code == c);
        if (d is null) return Ok(new { valide = false });
        var b = await _brand.ChargerAsync();
        return Ok(new
        {
            valide = true, type = "Document officiel", societe = b.Nom, titre = d.Titre, sousTitre = d.SousTitre,
            dateEmission = d.DateEmission, emisPar = d.EmisPar, empreinte = d.Empreinte.Substring(0, 16)
        });
    }
}
