using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PromoImmo.Domain;

namespace PromoImmo.Infrastructure;

public static class DbSeeder
{
    public static readonly string[] Roles = { "Gerant", "Recette", "Materiel", "Technique", "Achats", "Remboursement", "Fiscal", "Personnel", "Suivi" };

    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();

        try { await db.Lots.AnyAsync(); await db.AuditLogs.AnyAsync(); await db.Societe.AnyAsync(); await db.DocumentsEmis.AnyAsync(); }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "La base de données n'est pas à jour (nouvelles tables absentes). Dans le dossier backend, exécutez : " +
                "dotnet ef migrations add v4   (ou un autre nom si v4 existe déjà) puis relancez l'application : la migration s'applique automatiquement.", ex);
        }

        var roles = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var r in Roles)
            if (!await roles.RoleExistsAsync(r)) await roles.CreateAsync(new IdentityRole(r));

        var users = sp.GetRequiredService<UserManager<AppUser>>();
        async Task Ensure(string login, string nom, string pwd, string role)
        {
            if (await users.FindByNameAsync(login) != null) return;
            var u = new AppUser { UserName = login, Email = $"{login}@promoimmo.local", NomComplet = nom, EmailConfirmed = true };
            var res = await users.CreateAsync(u, pwd);
            if (res.Succeeded) await users.AddToRoleAsync(u, role);
        }
        await Ensure("gerant", "Gérant Principal", "Gerant@123", "Gerant");
        await Ensure("recette", "Agent Recette", "Recette@123", "Recette");
        await Ensure("materiel", "Responsable Matériel", "Materiel@123", "Materiel");
        await Ensure("technique", "Service Technique", "Technique@123", "Technique");
        await Ensure("achats", "Service Achats", "Achats@123", "Achats");
        await Ensure("remboursement", "Service Remboursement", "Remb@1234", "Remboursement");
        await Ensure("fiscal", "Service Fiscal", "Fiscal@123", "Fiscal");
        await Ensure("personnel", "Service Personnel", "Personnel@123", "Personnel");
        await Ensure("suivi", "Suivi Journalier", "Suivi@123", "Suivi");

        if (!await db.Chantiers.AnyAsync())
        {
            var ch = new Chantier { Nom = "Résidence Les Jasmins", Localisation = "Alger", BudgetPrevu = 6_000_000m };
            var cl = new Client { Nom = "Ahmed Benali", Telephone = "0555000000", Cin = "123456789", Email = "ahmed@example.com" };
            db.AddRange(ch, cl);
            await db.SaveChangesAsync();

            var ct = new Contrat { ClientId = cl.Id, ChantierId = ch.Id, Lot = "A-12", PrixTotal = 8_000_000m };
            for (int i = 1; i <= 4; i++)
                ct.Echeances.Add(new Echeance { Numero = i, MontantPrevu = 2_000_000m, DatePrevue = DateTime.Today.AddMonths(i) });
            db.Contrats.Add(ct);

            db.Materiels.AddRange(
                new Materiel { Designation = "Camion benne", Type = TypeMateriel.Roulant, Immatriculation = "00123-116-16" },
                new Materiel { Designation = "Bétonnière", Type = TypeMateriel.NonRoulant, Immatriculation = "BET-01" });
            await db.SaveChangesAsync();
        }

        if (!await db.Employes.AnyAsync())
        {
            var chId = (await db.Chantiers.OrderBy(c => c.Id).FirstAsync()).Id;
            db.Employes.AddRange(
                new Employe { Nom = "Karim Maçon", Poste = "Maçon", SalaireJournalier = 2500m, ChantierId = chId },
                new Employe { Nom = "Samir Ferrailleur", Poste = "Ferrailleur", SalaireJournalier = 2800m, ChantierId = chId },
                new Employe { Nom = "Yacine Chef d'équipe", Poste = "Chef d'équipe", SalaireJournalier = 3500m, ChantierId = chId });
            db.Fournisseurs.Add(new Fournisseur { Nom = "Ciment & Matériaux SARL", Telephone = "0661000000" });
            await db.SaveChangesAsync();
        }

        if (!await db.Lots.AnyAsync())
        {
            var ch = await db.Chantiers.OrderBy(c => c.Id).FirstAsync();
            var contrat = await db.Contrats.OrderBy(c => c.Id).FirstOrDefaultAsync(c => c.ChantierId == ch.Id);
            var client = await db.Clients.OrderBy(c => c.Id).FirstOrDefaultAsync();
            for (int n = 1; n <= 12; n++)
            {
                var lot = new Lot { ChantierId = ch.Id, Reference = $"A-{n}", TypeBien = n % 3 == 0 ? "F4" : "F3", Etage = ((n - 1) / 3).ToString(),
                    Surface = n % 3 == 0 ? 98m : 78m, Prix = n % 3 == 0 ? 9_500_000m : 8_000_000m };
                if (n == 12 && contrat != null && client != null) { lot.Statut = StatutLot.Vendu; lot.ClientId = client.Id; lot.ContratId = contrat.Id; lot.Prix = 8_000_000m; }
                else if (n == 5 && client != null) { lot.Statut = StatutLot.Reserve; lot.ClientId = client.Id; lot.DateReservation = DateTime.Today; lot.DateFinReservation = DateTime.Today.AddDays(15); }
                db.Lots.Add(lot);
            }
            await db.SaveChangesAsync();
        }
    }
}
