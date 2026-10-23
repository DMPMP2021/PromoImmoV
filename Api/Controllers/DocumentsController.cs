using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class DocumentUploadDto
{
    public IFormFile? Fichier { get; set; }
    public string Cible { get; set; } = "";
    public int CibleId { get; set; }
    public string? Categorie { get; set; }
    public string? Description { get; set; }
    public bool VisibleClient { get; set; }
}
public class VisibiliteDto { public bool Visible { get; set; } }

[ApiController, Route("api/documents"), Authorize]
public class DocumentsController : ControllerBase
{
    public static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".gif", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
    private const long TailleMax = 10L * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;
    public DocumentsController(AppDbContext db, IConfiguration cfg, IWebHostEnvironment env) { _db = db; _cfg = cfg; _env = env; }
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Dossier de stockage (Fichiers:Directory, sinon App_Data\fichiers).</summary>
    public static string DossierFichiers(IConfiguration cfg, IWebHostEnvironment env)
    {
        var d = cfg["Fichiers:Directory"];
        if (string.IsNullOrWhiteSpace(d)) d = Path.Combine(env.ContentRootPath, "App_Data", "fichiers");
        Directory.CreateDirectory(d);
        return d;
    }

    private static bool Peut(ClaimsPrincipal u, string cible)
    {
        if (u.IsInRole("Gerant")) return cible is "Client" or "Contrat" or "Lot" or "Reclamation" or "Chantier" or "Rapport" or "Commande" or "Employe" or "Materiel";
        return cible switch
        {
            "Client" or "Contrat" or "Lot" or "Reclamation" => u.IsInRole("Recette"),
            "Chantier" => u.IsInRole("Technique") || u.IsInRole("Suivi"),
            "Rapport" => u.IsInRole("Suivi"),
            "Commande" => u.IsInRole("Achats"),
            "Employe" => u.IsInRole("Personnel"),
            "Materiel" => u.IsInRole("Materiel"),
            _ => false
        };
    }

    private async Task<bool> ExisteAsync(string cible, int id) => cible switch
    {
        "Client" => await _db.Clients.AnyAsync(x => x.Id == id),
        "Contrat" => await _db.Contrats.AnyAsync(x => x.Id == id),
        "Lot" => await _db.Lots.AnyAsync(x => x.Id == id),
        "Chantier" => await _db.Chantiers.AnyAsync(x => x.Id == id),
        "Rapport" => await _db.Rapports.AnyAsync(x => x.Id == id),
        "Commande" => await _db.Commandes.AnyAsync(x => x.Id == id),
        "Employe" => await _db.Employes.AnyAsync(x => x.Id == id),
        "Materiel" => await _db.Materiels.AnyAsync(x => x.Id == id),
        "Reclamation" => await _db.Reclamations.AnyAsync(x => x.Id == id),
        _ => false
    };

    [HttpGet]
    public async Task<IActionResult> List(string cible, int cibleId)
    {
        if (!Peut(User, cible)) return Forbid();
        return Ok(await _db.Documents.AsNoTracking().Where(d => d.Cible == cible && d.CibleId == cibleId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                d.Id, d.NomFichier, d.TypeMime, d.Taille, d.Categorie, d.Description, d.VisibleClient, d.CreatedAt,
                EstImage = d.TypeMime.StartsWith("image/")
            }).Take(200).ToListAsync());
    }

    [HttpPost, RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> Upload([FromForm] DocumentUploadDto dto)
    {
        if (!Peut(User, dto.Cible)) return Forbid();
        var f = dto.Fichier;
        if (f is null || f.Length == 0) return BadRequest(new { message = "Aucun fichier reçu." });
        if (f.Length > TailleMax) return BadRequest(new { message = "Fichier trop volumineux (10 Mo maximum)." });
        var ext = Path.GetExtension(f.FileName);
        if (string.IsNullOrEmpty(ext) || !Extensions.Contains(ext))
            return BadRequest(new { message = "Type de fichier non autorisé (PDF, images, Word, Excel, texte)." });
        if (!await ExisteAsync(dto.Cible, dto.CibleId)) return BadRequest(new { message = "Élément introuvable." });

        var stocke = Guid.NewGuid().ToString("N") + ext.ToLowerInvariant();
        var chemin = Path.Combine(DossierFichiers(_cfg, _env), stocke);
        await using (var fs = new FileStream(chemin, FileMode.CreateNew))
            await f.CopyToAsync(fs);

        var nom = Path.GetFileName(f.FileName);
        if (nom.Length > 200) nom = nom.Substring(nom.Length - 200);
        if (!new FileExtensionContentTypeProvider().TryGetContentType(nom, out var mime)) mime = "application/octet-stream";

        var visible = dto.VisibleClient && (dto.Cible == "Client" || dto.Cible == "Contrat");
        var d = new DocumentJoint
        {
            Cible = dto.Cible, CibleId = dto.CibleId, Categorie = string.IsNullOrWhiteSpace(dto.Categorie) ? "Autre" : dto.Categorie!,
            NomFichier = nom, TypeMime = mime, Taille = f.Length, Chemin = stocke, Description = dto.Description,
            VisibleClient = visible, CreatedBy = Uid
        };
        _db.Documents.Add(d);
        await _db.SaveChangesAsync();
        return Ok(new { d.Id });
    }

    [HttpGet("{id:int}/fichier")]
    public async Task<IActionResult> Telecharger(int id)
    {
        var d = await _db.Documents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();
        if (!Peut(User, d.Cible)) return Forbid();
        var chemin = Path.Combine(DossierFichiers(_cfg, _env), d.Chemin);
        if (!System.IO.File.Exists(chemin)) return NotFound(new { message = "Fichier introuvable sur le serveur." });
        return PhysicalFile(chemin, d.TypeMime, d.NomFichier);
    }

    [HttpPut("{id:int}/visibilite"), Authorize(Roles = "Gerant,Recette")]
    public async Task<IActionResult> Visibilite(int id, VisibiliteDto dto)
    {
        var d = await _db.Documents.FindAsync(id);
        if (d is null) return NotFound();
        if (d.Cible != "Client" && d.Cible != "Contrat") return BadRequest(new { message = "Seuls les documents d'un client ou d'un contrat peuvent être partagés." });
        d.VisibleClient = dto.Visible;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Supprimer(int id)
    {
        var d = await _db.Documents.FindAsync(id);
        if (d is null) return NotFound();
        if (!Peut(User, d.Cible)) return Forbid();
        if (!User.IsInRole("Gerant") && d.CreatedBy != Uid) return Forbid();
        var chemin = Path.Combine(DossierFichiers(_cfg, _env), d.Chemin);
        _db.Documents.Remove(d);
        await _db.SaveChangesAsync();
        try { if (System.IO.File.Exists(chemin)) System.IO.File.Delete(chemin); } catch { /* fichier verrouillé : ignoré */ }
        return NoContent();
    }
}
