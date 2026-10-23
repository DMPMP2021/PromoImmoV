using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Infrastructure;

namespace PromoImmo.Api.Services;

/// <summary>Sauvegarde de la base PostgreSQL (pg_dump, format .dump) + purge des anciennes sauvegardes.</summary>
public static class Sauvegarde
{
    public static DateTime? DerniereDate { get; set; }
    public static bool? DernierOk { get; set; }
    public static string? DernierMessage { get; set; }

    public static string Dossier(IConfiguration cfg) =>
        cfg["Backup:Directory"] ?? (OperatingSystem.IsWindows() ? @"D:\Sauvegardes\PromoImmo" : "/app/Sauvegardes");

    public static async Task<string> ExecuterAsync(AppDbContext db, IConfiguration cfg)
    {
        var dir = Dossier(cfg);
        Directory.CreateDirectory(dir);

        var csb = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        var file = Path.Combine(dir, $"{csb.Database}_{DateTime.Now:yyyyMMdd_HHmmss}.dump");

        var psi = new ProcessStartInfo("pg_dump")
        {
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var a in new[] { "--host", csb.Host!, "--port", csb.Port.ToString(), "--username", csb.Username!,
                                  "--dbname", csb.Database!, "--schema", "immo", "--format", "custom",
                                  "--no-owner", "--no-privileges", "--file", file })
            psi.ArgumentList.Add(a);
        psi.Environment["PGPASSWORD"] = csb.Password ?? "";   // jamais dans la ligne de commande
        psi.Environment["PGSSLMODE"] = "require";

        try
        {
            using var proc = Process.Start(psi)!;
            var err = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (proc.ExitCode != 0) throw new InvalidOperationException($"pg_dump a échoué (code {proc.ExitCode}) : {err.Trim()}");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("pg_dump est introuvable : installez postgresql-client (version >= celle de Supabase) sur le serveur de l'API.");
        }

        var jours = cfg.GetValue("Backup:RetentionDays", 30);
        foreach (var f in new DirectoryInfo(dir).GetFiles("*.dump").Where(f => f.LastWriteTime < DateTime.Now.AddDays(-jours)))
        {
            try { f.Delete(); } catch { /* fichier verrouillé : on ignore */ }
        }
        return file;
    }
}

public class BackupService : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly IConfiguration _cfg;
    private readonly ILogger<BackupService> _log;
    public BackupService(IServiceScopeFactory sf, IConfiguration cfg, ILogger<BackupService> log) { _sf = sf; _cfg = cfg; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_cfg.GetValue("Backup:Enabled", true)) return;
        var heure = _cfg.GetValue("Backup:Hour", 2);
        while (!ct.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var next = now.Date.AddHours(heure);
            if (next <= now) next = next.AddDays(1);
            try { await Task.Delay(next - now, ct); } catch (OperationCanceledException) { break; }

            using var scope = _sf.CreateScope();
            try
            {
                var file = await Sauvegarde.ExecuterAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), _cfg);
                Sauvegarde.DerniereDate = DateTime.Now; Sauvegarde.DernierOk = true; Sauvegarde.DernierMessage = file;
                _log.LogInformation("Sauvegarde OK : {File}", file);
            }
            catch (Exception ex)
            {
                Sauvegarde.DerniereDate = DateTime.Now; Sauvegarde.DernierOk = false; Sauvegarde.DernierMessage = ex.Message;
                _log.LogError(ex, "Échec de la sauvegarde");
                try { await scope.ServiceProvider.GetRequiredService<Notifier>().GerantsUniqueAsync("ALERTE", "⚠ Échec de la sauvegarde automatique : " + ex.Message, TimeSpan.FromHours(12)); }
                catch { /* ignoré */ }
            }
        }
    }
}

/// <summary>Génère régulièrement les alertes (impayés, fiscal, budgets…) pour le gérant.</summary>
public class AlertesService : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AlertesService> _log;
    public AlertesService(IServiceScopeFactory sf, IConfiguration cfg, ILogger<AlertesService> log) { _sf = sf; _cfg = cfg; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_cfg.GetValue("Alertes:Enabled", true)) return;
        try { await Task.Delay(TimeSpan.FromMinutes(1), ct); } catch (OperationCanceledException) { return; }
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sf.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<RappelsService>().GenererAsync();
                _log.LogInformation("Alertes générées : {N}", n);
            }
            catch (Exception ex) { _log.LogError(ex, "Erreur de génération des alertes"); }
            try { await Task.Delay(TimeSpan.FromHours(_cfg.GetValue("Alertes:IntervalleHeures", 6)), ct); } catch (OperationCanceledException) { break; }
        }
    }
}
