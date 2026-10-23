using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PromoImmo.Api.Services;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;

namespace PromoImmo.Api.Controllers;

public class SocieteDto
{
    [Required, StringLength(150)] public string Nom { get; set; } = "";
    [StringLength(300)] public string? Adresse { get; set; }
    [StringLength(60)] public string? Telephone { get; set; }
    [StringLength(150)] public string? Email { get; set; }
    [StringLength(300)] public string? Identifiants { get; set; }
    [StringLength(300)] public string? PiedDePage { get; set; }
    [StringLength(200)] public string? UrlPublique { get; set; }
    [StringLength(100)] public string? SignataireNom { get; set; }
    [StringLength(100)] public string? SignataireFonction { get; set; }
}

/// <summary>Identité de l'entreprise : en-tête, logo, signature et adresse utilisée dans les QR codes.</summary>
[ApiController, Route("api/societe"), Authorize(Roles = "Gerant")]
public class SocieteController : ControllerBase
{
    private const int TailleMax = 600_000;
    private readonly AppDbContext _db;
    private readonly BrandingService _brand;
    public SocieteController(AppDbContext db, BrandingService brand) { _db = db; _brand = brand; }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var p = await _brand.ObtenirAsync();
        return Ok(new
        {
            p.Nom, p.Adresse, p.Telephone, p.Email, p.Identifiants, p.PiedDePage, p.UrlPublique, p.SignataireNom, p.SignataireFonction,
            ALogo = p.Logo != null, ASignature = p.Signature != null
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update(SocieteDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.UrlPublique) &&
            !(Uri.TryCreate(dto.UrlPublique, UriKind.Absolute, out var u) && (u.Scheme == "http" || u.Scheme == "https")))
            return BadRequest(new { message = "L'adresse publique doit commencer par http:// ou https://." });
        var p = await _brand.ObtenirAsync();
        p.Nom = dto.Nom.Trim(); p.Adresse = dto.Adresse; p.Telephone = dto.Telephone; p.Email = dto.Email;
        p.Identifiants = dto.Identifiants; p.PiedDePage = dto.PiedDePage;
        p.UrlPublique = string.IsNullOrWhiteSpace(dto.UrlPublique) ? null : dto.UrlPublique.Trim().TrimEnd('/');
        p.SignataireNom = dto.SignataireNom; p.SignataireFonction = dto.SignataireFonction;
        p.ModifieLe = DateTime.Now;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Contrôle le fichier : PNG ou JPEG réel (signature binaire), 600 Ko maximum.</summary>
    private static async Task<(byte[]? data, string? mime, string? erreur)> Lire(IFormFile? f)
    {
        if (f is null || f.Length == 0) return (null, null, "Aucun fichier reçu.");
        if (f.Length > TailleMax) return (null, null, "Image trop lourde (600 Ko maximum). Réduisez-la avant de l'envoyer.");
        using var ms = new MemoryStream();
        await f.CopyToAsync(ms);
        var d = ms.ToArray();
        var png = d.Length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47;
        var jpg = d.Length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF;
        if (!png && !jpg) return (null, null, "Format non accepté : utilisez une image PNG ou JPEG.");
        return (d, png ? "image/png" : "image/jpeg", null);
    }

    [HttpPost("logo"), RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> SetLogo(IFormFile fichier)
    {
        var (data, mime, err) = await Lire(fichier);
        if (err != null) return BadRequest(new { message = err });
        var p = await _brand.ObtenirAsync();
        p.Logo = data; p.LogoMime = mime; p.ModifieLe = DateTime.Now;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("signature"), RequestSizeLimit(1_000_000)]
    public async Task<IActionResult> SetSignature(IFormFile fichier)
    {
        var (data, mime, err) = await Lire(fichier);
        if (err != null) return BadRequest(new { message = err });
        var p = await _brand.ObtenirAsync();
        p.Signature = data; p.SignatureMime = mime; p.ModifieLe = DateTime.Now;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("logo")]
    public async Task<IActionResult> DelLogo()
    {
        var p = await _brand.ObtenirAsync();
        p.Logo = null; p.LogoMime = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("signature")]
    public async Task<IActionResult> DelSignature()
    {
        var p = await _brand.ObtenirAsync();
        p.Signature = null; p.SignatureMime = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("logo")]
    public async Task<IActionResult> GetLogo()
    {
        var p = await _brand.ObtenirAsync();
        return p.Logo == null ? NotFound() : File(p.Logo, p.LogoMime ?? "image/png");
    }

    [HttpGet("signature")]
    public async Task<IActionResult> GetSignature()
    {
        var p = await _brand.ObtenirAsync();
        return p.Signature == null ? NotFound() : File(p.Signature, p.SignatureMime ?? "image/png");
    }
}
