# Migration SQL Server -> PostgreSQL (Supabase)

## 1. Secrets (ne plus jamais committer de mot de passe)
```bash
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:QUITTANCEAppCon" "Host=aws-0-<region>.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<ref-projet>;Password=<mot-de-passe>;SSL Mode=Require;Trust Server Certificate=true"
dotnet user-secrets set "Jwt:Key" "<cle-aleatoire-de-32-caracteres-minimum>"
```
En production (Render / Docker) : variables d'environnement
`ConnectionStrings__QUITTANCEAppCon` et `Jwt__Key`.

La chaîne vient de Supabase > bouton **Connect** > **Session pooler** (port 5432, compatible IPv4).

## 2. Migrations (le dossier Migrations SQL Server a été supprimé)
```bash
dotnet restore
dotnet ef migrations add InitialPostgres
dotnet run          # DbSeeder applique la migration automatiquement
```
Les tables sont créées dans le schéma `immo`.

## 3. Reprise des données existantes (base SQL Server PromoImmo2)
Avec pgloader (SQL Server -> PostgreSQL), à lancer APRES l'étape 2 :
chargez les données dans le schéma `immo` en conservant les noms de tables et de colonnes
(EF Core utilise des noms sensibles à la casse : "Clients", "Encaissements"...).
Vérifiez ensuite les séquences d'identité (setval) et les totaux (nombre de lignes, somme des encaissements).

## 4. Sauvegardes
`BACKUP DATABASE` n'existe pas en PostgreSQL : le service utilise maintenant `pg_dump`
(installé dans le Dockerfile). Sur Render, le disque est éphémère : les fichiers .dump locaux
sont perdus au redéploiement. Prévoyez aussi les sauvegardes Supabase, ou un stockage externe.

## 5. Sécurité à faire
- Dépôt GitHub en privé ; changer l'ancien mot de passe SQL Server et la clé JWT (déjà publics).
- Changer les mots de passe des comptes créés par DbSeeder (Gerant@123, Recette@123...) après la première connexion.
