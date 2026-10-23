using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using QRCoder;

namespace PromoImmo.Api.Services;

public class BrandingService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;

    public BrandingService(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    public async Task<ParametresSociete> ChargerAsync()
    {
        var societe = await _db.ParametresSocietes.FirstOrDefaultAsync();
        if (societe == null)
        {
            societe = new ParametresSociete { Nom = "PromoImmo" };
            _db.ParametresSocietes.Add(societe);
            await _db.SaveChangesAsync();
        }
        if (string.IsNullOrWhiteSpace(societe.UrlPublique))
        {
            societe.UrlPublique = _cfg["App:UrlPublique"] ?? "https://localhost:5001";
        }
        return societe;
    }

    public async Task<ParametresSociete> ObtenirAsync() => await ChargerAsync();

    // Surcharge pour accepter un paramètre (utilisé dans le DashboardController)
    public async Task<ParametresSociete> RecuAsync(object? arg = null) => await ChargerAsync();

    public static string Empreinte(string titre, string sousTitre, List<ExportSection> sections)
    {
        var sb = new StringBuilder();
        sb.Append(titre).Append('|').Append(sousTitre).Append('|');
        foreach (var sec in sections)
        {
            sb.Append(sec.Titre).Append('|');
            foreach (var col in sec.Colonnes) sb.Append(col).Append(',');
            foreach (var ligne in sec.Lignes)
            {
                foreach (var cell in ligne) sb.Append(cell?.ToString() ?? "").Append(';');
            }
        }
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<DocumentEmis> EnregistrerAsync(string nom, string titre, string sousTitre, string empreinte, string emisPar)
    {
        var code = Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
        var docEmis = new DocumentEmis
        {
            Code = code,
            Type = nom,
            Titre = titre,
            SousTitre = sousTitre,
            DateEmission = DateTime.Now,
            EmisPar = emisPar,
            Empreinte = empreinte
        };
        _db.DocumentsEmis.Add(docEmis);
        await _db.SaveChangesAsync();
        return docEmis;
    }

    public string Signer(string type, int id)
    {
        var secretKey = _cfg["Jwt:Key"] ?? "CleSecreteParDefautPourPromoImmo2026!secure";
        var payload = $"{type}:{id}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
    }

    // Méthode de vérification HMAC pour le VerificationController
    public bool Verifier(string type, int id, string sig)
    {
        var expectedSig = Signer(type, id);
        return string.Equals(expectedSig, sig, StringComparison.OrdinalIgnoreCase);
    }

    public byte[]? QrPng(string url)
    {
        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            var pngByteQRCode = new PngByteQRCode(qrCodeData);
            return pngByteQRCode.GetGraphic(20);
        }
        catch
        {
            return null;
        }
    }
}