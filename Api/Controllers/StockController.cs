using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Domain;
using PromoImmo.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace PromoImmo.Api.Controllers;

public class ArticleDto
{
    [Required] public string Designation { get; set; } = "";
    public string Unite { get; set; } = "u";
    public string? Categorie { get; set; }
    [Range(0, double.MaxValue)] public decimal StockMin { get; set; }
    [Range(0, double.MaxValue)] public decimal PrixUnitaire { get; set; }
    public bool Actif { get; set; } = true;
}
public class MouvementDto
{
    [Required] public int ArticleId { get; set; }
    public TypeMouvement Type { get; set; }
    public decimal Quantite { get; set; }
    public DateTime? Date { get; set; }
    public int? ChantierId { get; set; }
    public int? CommandeId { get; set; }
    public string? Motif { get; set; }
}

[ApiController, Route("api/stock"), Authorize(Roles = "Gerant,Achats,Suivi,Technique")]
public class StockController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Notifier _notif;
    public StockController(AppDbContext db, Notifier notif) { _db = db; _notif = notif; }
    private string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<decimal> StockDe(int articleId) =>
        await _db.MouvementsStock.Where(m => m.ArticleId == articleId)
            .SumAsync(m => (decimal?)(m.Type == TypeMouvement.Sortie ? -m.Quantite : m.Quantite)) ?? 0m;

    [HttpGet("articles")]
    public async Task<IActionResult> Articles()
    {
        var stocks = (await _db.MouvementsStock.GroupBy(m => m.ArticleId)
            .Select(g => new { g.Key, S = g.Sum(x => x.Type == TypeMouvement.Sortie ? -x.Quantite : x.Quantite) }).ToListAsync())
            .ToDictionary(x => x.Key, x => x.S);
        var arts = await _db.Articles.AsNoTracking().OrderBy(a => a.Designation).ToListAsync();
        return Ok(arts.Select(a =>
        {
            var s = stocks.TryGetValue(a.Id, out var v) ? v : 0m;
            return new
            {
                a.Id, a.Designation, a.Unite, a.Categorie, a.StockMin, a.PrixUnitaire, a.Actif, StockActuel = s,
                Valeur = s * a.PrixUnitaire, SousSeuil = a.Actif && a.StockMin > 0 && s <= a.StockMin
            };
        }));
    }

    [HttpPost("articles"), Authorize(Roles = "Gerant,Achats")]
    public async Task<IActionResult> AddArticle(ArticleDto dto)
    {
        var a = new Article { Designation = dto.Designation.Trim(), Unite = dto.Unite, Categorie = dto.Categorie, StockMin = dto.StockMin,
            PrixUnitaire = dto.PrixUnitaire, Actif = dto.Actif, CreatedBy = Uid };
        _db.Articles.Add(a);
        await _db.SaveChangesAsync();
        return Ok(new { a.Id });
    }

    [HttpPut("articles/{id:int}"), Authorize(Roles = "Gerant,Achats")]
    public async Task<IActionResult> UpdArticle(int id, ArticleDto dto)
    {
        var a = await _db.Articles.FindAsync(id);
        if (a is null) return NotFound();
        a.Designation = dto.Designation.Trim(); a.Unite = dto.Unite; a.Categorie = dto.Categorie; a.StockMin = dto.StockMin;
        a.PrixUnitaire = dto.PrixUnitaire; a.Actif = dto.Actif;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("mouvements")]
    public async Task<IActionResult> Mouvements(int? articleId, int? chantierId, TypeMouvement? type, DateTime? du, DateTime? au) =>
        Ok(await _db.MouvementsStock.AsNoTracking()
            .Where(m => (articleId == null || m.ArticleId == articleId) && (chantierId == null || m.ChantierId == chantierId) &&
                        (type == null || m.Type == type) && (du == null || m.Date >= du) && (au == null || m.Date < au.Value.AddDays(1)))
            .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id)
            .Select(m => new
            {
                m.Id, m.Date, m.ArticleId, Article = m.Article.Designation, m.Article.Unite, m.Type, m.Quantite, m.ChantierId,
                ChantierNom = m.Chantier != null ? m.Chantier.Nom : null, m.CommandeId, m.Motif
            }).Take(500).ToListAsync());

    [HttpPost("mouvements")]
    public async Task<IActionResult> AddMouvement(MouvementDto dto)
    {
        var art = await _db.Articles.FirstOrDefaultAsync(a => a.Id == dto.ArticleId);
        if (art is null) return BadRequest(new { message = "Article introuvable." });
        if (dto.Quantite == 0) return BadRequest(new { message = "La quantité ne peut pas être nulle." });
        if (dto.Type != TypeMouvement.Ajustement && dto.Quantite < 0) return BadRequest(new { message = "La quantité doit être positive." });
        if (dto.ChantierId != null && !await _db.Chantiers.AnyAsync(c => c.Id == dto.ChantierId)) return BadRequest(new { message = "Chantier introuvable." });
        if (dto.CommandeId != null && !await _db.Commandes.AnyAsync(c => c.Id == dto.CommandeId)) return BadRequest(new { message = "Commande introuvable." });

        var stock = await StockDe(art.Id);
        var apres = stock + (dto.Type == TypeMouvement.Sortie ? -dto.Quantite : dto.Quantite);
        if (apres < 0) return BadRequest(new { message = $"Stock insuffisant : {stock:0.##} {art.Unite} disponible(s)." });

        _db.MouvementsStock.Add(new MouvementStock { ArticleId = art.Id, Date = (dto.Date ?? DateTime.Now), Type = dto.Type, Quantite = dto.Quantite,
            ChantierId = dto.ChantierId, CommandeId = dto.CommandeId, Motif = dto.Motif, CreatedBy = Uid });
        await _db.SaveChangesAsync();

        if (art.StockMin > 0 && apres <= art.StockMin)
            await _notif.GerantsUniqueAsync("ALERTE", $"⚠ Stock bas : {art.Designation} — {apres:0.##} {art.Unite} (seuil {art.StockMin:0.##})", TimeSpan.FromHours(12));
        return Ok(new { stock = apres });
    }
}
