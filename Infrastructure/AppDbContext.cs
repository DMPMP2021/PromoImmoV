using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PromoImmo.Domain;
using System.Security.Claims;
using System.Text;

namespace PromoImmo.Infrastructure;

public class AppDbContext : IdentityDbContext<AppUser>
{
    private readonly IHttpContextAccessor? _http;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? http = null) : base(options) { _http = http; }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Chantier> Chantiers => Set<Chantier>();
    public DbSet<Contrat> Contrats => Set<Contrat>();
    public DbSet<Echeance> Echeances => Set<Echeance>();
    public DbSet<Encaissement> Encaissements => Set<Encaissement>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AllocationBudget> Allocations => Set<AllocationBudget>();
    public DbSet<Materiel> Materiels => Set<Materiel>();
    public DbSet<Entretien> Entretiens => Set<Entretien>();
    public DbSet<PaiementMateriel> PaiementsMateriel => Set<PaiementMateriel>();

    public DbSet<Tache> Taches => Set<Tache>();
    public DbSet<AvancementChantier> Avancements => Set<AvancementChantier>();
    public DbSet<Fournisseur> Fournisseurs => Set<Fournisseur>();
    public DbSet<CommandeAchat> Commandes => Set<CommandeAchat>();
    public DbSet<DemandeRemboursement> Remboursements => Set<DemandeRemboursement>();
    public DbSet<ObligationFiscale> Obligations => Set<ObligationFiscale>();
    public DbSet<Employe> Employes => Set<Employe>();
    public DbSet<Pointage> Pointages => Set<Pointage>();
    public DbSet<PaiementPersonnel> PaiementsPersonnel => Set<PaiementPersonnel>();
    public DbSet<RapportJournalier> Rapports => Set<RapportJournalier>();
    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionAffectation> Affectations => Set<MissionAffectation>();
    public DbSet<DepenseDiverse> Depenses => Set<DepenseDiverse>();
    public DbSet<Reclamation> Reclamations => Set<Reclamation>();
    public DbSet<DocumentJoint> Documents => Set<DocumentJoint>();
    public DbSet<ParametresSociete> ParametresSocietes => Set<ParametresSociete>();
    public DbSet<Article> Articles { get; set; }
    public DbSet<MouvementStock> MouvementsStock { get; set; }
    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<Relance> Relances => Set<Relance>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ParametresSociete> Societe => Set<ParametresSociete>();
    public DbSet<DocumentEmis> DocumentsEmis => Set<DocumentEmis>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Schéma dédié : non exposé par l'API REST (PostgREST) de Supabase -> inaccessible avec la clé publique
        b.HasDefaultSchema("immo");

        foreach (var p in b.Model.GetEntityTypes().SelectMany(t => t.GetProperties()).Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            p.SetPrecision(18);
            p.SetScale(2);
        }

        b.Entity<MissionAffectation>().HasKey(a => new { a.MissionId, a.EmployeId });
        b.Entity<Encaissement>().HasIndex(e => e.NumeroRecu).IsUnique();
        b.Entity<Encaissement>().HasIndex(e => new { e.ContratId, e.DateEncaissement });
        b.Entity<Notification>().HasIndex(n => new { n.DestinataireId, n.Lu });
        b.Entity<Pointage>().HasIndex(p => new { p.EmployeId, p.Date });
        b.Entity<Tache>().HasIndex(t => new { t.Service, t.Statut });
        b.Entity<Lot>().HasIndex(l => new { l.ChantierId, l.Reference }).IsUnique();
        b.Entity<AuditLog>().HasIndex(a => a.Date);
        b.Entity<DocumentEmis>().HasIndex(d => d.Code).IsUnique();

        // Aucune suppression en cascade sur les données métier (traçabilité)
        foreach (var fk in b.Model.GetEntityTypes()
                     .Where(t => t.ClrType.Namespace == "PromoImmo.Domain" && t.ClrType != typeof(AppUser))
                     .SelectMany(t => t.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }

    // ------------------------------------------------------------------
    //  Journal d'audit automatique des opérations sensibles
    // ------------------------------------------------------------------
    private static readonly HashSet<Type> Audites = new()
    {
        typeof(Encaissement), typeof(AllocationBudget), typeof(DemandeRemboursement), typeof(ObligationFiscale),
        typeof(Contrat), typeof(Client), typeof(Chantier), typeof(Lot), typeof(CommandeAchat), typeof(PaiementPersonnel),
        typeof(PaiementMateriel), typeof(DepenseDiverse), typeof(Employe), typeof(Reclamation)
    };

    private sealed record PendingAudit(EntityEntry Entry, EntityState State, string Details, string? KeyBefore);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pending = ChangeTracker.Entries()
            .Where(e => Audites.Contains(e.Entity.GetType()) &&
                        (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
            .Select(e => new PendingAudit(e, e.State, Describe(e), KeyOf(e)))
            .Where(p => p.State != EntityState.Modified || p.Details.Length > 0)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (pending.Count > 0)
        {
            var user = _http?.HttpContext?.User;
            var name = user?.Identity?.Name ?? "Système / anonyme";
            var uid = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            var ip = _http?.HttpContext?.Connection.RemoteIpAddress?.ToString();
            foreach (var p in pending)
            {
                AuditLogs.Add(new AuditLog
                {
                    Date = DateTime.Now, UserId = uid, UserName = name, Ip = ip,
                    Action = p.State == EntityState.Added ? "Création" : p.State == EntityState.Deleted ? "Suppression" : "Modification",
                    Entite = p.Entry.Entity.GetType().Name,
                    EntiteId = p.State == EntityState.Added ? KeyOf(p.Entry) : p.KeyBefore,
                    Details = p.Details
                });
            }
            await base.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    private static string? KeyOf(EntityEntry e) =>
        e.Properties.FirstOrDefault(x => x.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

    private static string Fmt(object? v) => v switch
    {
        null => "∅",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm"),
        decimal m => m.ToString("0.##"),
        _ => v.ToString() ?? ""
    };

    private static string Describe(EntityEntry e)
    {
        var sb = new StringBuilder();
        foreach (var p in e.Properties)
        {
            var name = p.Metadata.Name;
            if (name == "CreatedAt" || name == "CreatedBy" || name == "Id") continue;
            if (e.State == EntityState.Modified)
            {
                if (!p.IsModified) continue;
                sb.Append($"{name}: {Fmt(p.OriginalValue)} → {Fmt(p.CurrentValue)} ; ");
            }
            else sb.Append($"{name}={Fmt(p.CurrentValue)} ; ");
        }
        var s = sb.ToString();
        return s.Length > 1500 ? s.Substring(0, 1500) + "…" : s;
    }
}
