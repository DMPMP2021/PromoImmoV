using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Api.Services;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Controllers;

[ApiController, Route("api/audit"), Authorize(Roles = "Gerant")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _db;
    public AuditController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(DateTime? du, DateTime? au, string? utilisateur, string? entite, string? q, int page = 1, int taille = 50)
    {
        if (page < 1) page = 1;
        if (taille < 1 || taille > 200) taille = 50;
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (du != null) query = query.Where(a => a.Date >= du.Value.Date);
        if (au != null) query = query.Where(a => a.Date < au.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(utilisateur)) query = query.Where(a => EF.Functions.ILike(a.UserName, $"%{utilisateur}%"));
        if (!string.IsNullOrWhiteSpace(entite)) query = query.Where(a => a.Entite == entite);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(a => (a.Details != null && EF.Functions.ILike(a.Details, $"%{q}%")) || EF.Functions.ILike(a.Action, $"%{q}%"));

        var total = await query.CountAsync();
        var items = await query.OrderByDescending(a => a.Date).ThenByDescending(a => a.Id)
            .Skip((page - 1) * taille).Take(taille)
            .Select(a => new { a.Id, a.Date, a.UserName, a.Action, a.Entite, a.EntiteId, a.Details, a.Ip }).ToListAsync();
        return Ok(new { total, page, taille, items });
    }

    [HttpGet("entites")]
    public async Task<IActionResult> Entites() =>
        Ok(await _db.AuditLogs.AsNoTracking().Select(a => a.Entite).Distinct().OrderBy(x => x).ToListAsync());
}

[ApiController, Route("api/backup"), Authorize(Roles = "Gerant")]
public class BackupController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    public BackupController(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    [HttpGet]
    public IActionResult Etat()
    {
        var dir = Sauvegarde.Dossier(_cfg);
        var fichiers = Directory.Exists(dir)
            ? new DirectoryInfo(dir).GetFiles("*.bak").OrderByDescending(f => f.LastWriteTime).Take(30)
                .Select(f => new { Nom = f.Name, Taille = f.Length, Date = f.LastWriteTime }).ToList()
            : null;
        return Ok(new
        {
            Dossier = dir,
            Actif = _cfg.GetValue("Backup:Enabled", true),
            Heure = _cfg.GetValue("Backup:Hour", 2),
            Retention = _cfg.GetValue("Backup:RetentionDays", 30),
            Derniere = new { Date = Sauvegarde.DerniereDate, Ok = Sauvegarde.DernierOk, Message = Sauvegarde.DernierMessage },
            Fichiers = fichiers ?? new[] { new { Nom = "", Taille = 0L, Date = DateTime.MinValue } }.Take(0).ToList()
        });
    }

    [HttpPost("now")]
    public async Task<IActionResult> Maintenant()
    {
        try
        {
            var file = await Sauvegarde.ExecuterAsync(_db, _cfg);
            Sauvegarde.DerniereDate = DateTime.Now; Sauvegarde.DernierOk = true; Sauvegarde.DernierMessage = file;
            return Ok(new { fichier = file });
        }
        catch (Exception ex)
        {
            Sauvegarde.DerniereDate = DateTime.Now; Sauvegarde.DernierOk = false; Sauvegarde.DernierMessage = ex.Message;
            return BadRequest(new { message = "Sauvegarde impossible : " + (ex.InnerException?.Message ?? ex.Message) });
        }
    }
}
