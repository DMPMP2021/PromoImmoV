namespace PromoImmo.Domain;

public enum ServiceCode { Technique = 1, Achats = 2, Remboursement = 3, Fiscal = 4, Personnel = 5, Suivi = 6, Divers = 7 }
public enum StatutTache { APrevoir = 1, EnCours = 2, Termine = 3, Bloque = 4 }
public enum NiveauPriorite { Basse = 1, Normale = 2, Haute = 3, Urgente = 4 }
public enum StatutCommande { Demandee = 1, Commandee = 2, Livree = 3, Payee = 4, Annulee = 5, AValider = 6 }
public enum TypeRemboursement { Desistement = 1, TropPercu = 2 }
public enum StatutRemboursement { Demandee = 1, Validee = 2, Rejetee = 3, Payee = 4 }
public enum TypeObligation { Fiscale = 1, Parafiscale = 2 }
public enum StatutObligation { APayer = 1, Payee = 2 }
public enum StatutPointage { Present = 1, Absent = 2, Retard = 3, Conge = 4, Maladie = 5 }
public enum TypePaiementPersonnel { Salaire = 1, Avance = 2, Prime = 3 }
public enum StatutMission { Planifiee = 1, EnCours = 2, Terminee = 3, Annulee = 4 }
public enum StatutReclamation { Nouvelle = 1, EnCours = 2, Traitee = 3, Rejetee = 4 }

/// <summary>Tâche générique, rattachée à un service (et éventuellement à un chantier).</summary>
public class Tache : Auditable
{
    public int Id { get; set; }
    public ServiceCode Service { get; set; }
    public string Titre { get; set; } = "";
    public string? Description { get; set; }
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public string? AssigneA { get; set; }
    public NiveauPriorite Priorite { get; set; } = NiveauPriorite.Normale;
    public StatutTache Statut { get; set; } = StatutTache.APrevoir;
    public DateTime? DateEcheance { get; set; }
    public DateTime? DateFin { get; set; }
}

// ---- Service technique ----
public class AvancementChantier : Auditable
{
    public int Id { get; set; }
    public int ChantierId { get; set; }
    public Chantier Chantier { get; set; } = null!;
    public DateTime Date { get; set; }
    public string Phase { get; set; } = "";
    public int Pourcentage { get; set; }
    public string? Observations { get; set; }
}

// ---- Achats et approvisionnement ----
public class Fournisseur : Auditable
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Adresse { get; set; }
}

public class CommandeAchat : Auditable
{
    public int Id { get; set; }
    public int FournisseurId { get; set; }
    public Fournisseur Fournisseur { get; set; } = null!;
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public string Objet { get; set; } = "";
    public decimal Quantite { get; set; }
    public string Unite { get; set; } = "";
    public decimal Montant { get; set; }
    public StatutCommande Statut { get; set; } = StatutCommande.Demandee;
    public DateTime DateCommande { get; set; } = DateTime.Today;
    public DateTime? DateLivraisonPrevue { get; set; }
    public DateTime? DateLivraison { get; set; }
}

// ---- Remboursements (désistements, trop-perçus) ----
public class DemandeRemboursement : Auditable
{
    public int Id { get; set; }
    public int ContratId { get; set; }
    public Contrat Contrat { get; set; } = null!;
    public TypeRemboursement Type { get; set; }
    public decimal Montant { get; set; }
    public string Motif { get; set; } = "";
    public StatutRemboursement Statut { get; set; } = StatutRemboursement.Demandee;
    public DateTime? DateDecision { get; set; }
    public string? DecidePar { get; set; }
    public string? Commentaire { get; set; }
    public DateTime? DatePaiement { get; set; }
    public ModePaiement? Mode { get; set; }
    public string? RefPaiement { get; set; }
}

// ---- Fiscal et parafiscal ----
public class ObligationFiscale : Auditable
{
    public int Id { get; set; }
    public TypeObligation Type { get; set; }
    public string Libelle { get; set; } = "";
    public string Periode { get; set; } = "";
    public decimal Montant { get; set; }
    public DateTime DateEcheance { get; set; }
    public StatutObligation Statut { get; set; } = StatutObligation.APayer;
    public DateTime? DatePaiement { get; set; }
    public string? Reference { get; set; }
}

// ---- Personnel ----
public class Employe : Auditable
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string Poste { get; set; } = "";
    public string? Telephone { get; set; }
    public decimal SalaireJournalier { get; set; }
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public bool Actif { get; set; } = true;
}

public class Pointage : Auditable
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe Employe { get; set; } = null!;
    public DateTime Date { get; set; }
    public StatutPointage Statut { get; set; } = StatutPointage.Present;
    public decimal Heures { get; set; } = 8;
    public int? ChantierId { get; set; }
    public string? Remarque { get; set; }
}

public class PaiementPersonnel : Auditable
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public Employe Employe { get; set; } = null!;
    public TypePaiementPersonnel Type { get; set; }
    public decimal Montant { get; set; }
    public string? Periode { get; set; }
    public string? Motif { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
}

// ---- Suivi journalier et missions ----
public class RapportJournalier : Auditable
{
    public int Id { get; set; }
    public int ChantierId { get; set; }
    public Chantier Chantier { get; set; } = null!;
    public DateTime Date { get; set; }
    public string Travaux { get; set; } = "";
    public string? Incidents { get; set; }
    public string? Meteo { get; set; }
}

public class Mission : Auditable
{
    public int Id { get; set; }
    public int ChantierId { get; set; }
    public Chantier Chantier { get; set; } = null!;
    public string Titre { get; set; } = "";
    public string? Description { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFin { get; set; }
    public StatutMission Statut { get; set; } = StatutMission.Planifiee;
    public List<MissionAffectation> Affectations { get; set; } = new();
}

public class MissionAffectation
{
    public int MissionId { get; set; }
    public Mission Mission { get; set; } = null!;
    public int EmployeId { get; set; }
    public Employe Employe { get; set; } = null!;
}

// ---- Divers ----
public class DepenseDiverse : Auditable
{
    public int Id { get; set; }
    public string Libelle { get; set; } = "";
    public string Categorie { get; set; } = "";
    public decimal Montant { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public string? Reference { get; set; }
}

// ---- Réclamations clients (formulaire public « Contactez-nous ») ----
//public enum StatutReclamation { Nouvelle = 1, EnCours = 2, Traitee = 3, Rejetee = 4 }

public class Reclamation : Auditable
{
    public int Id { get; set; }
    public int? ClientId { get; set; }
    public Client? Client { get; set; }

    public string Nom { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    public string? ContratRef { get; set; }
    public string Objet { get; set; } = "";
    public string Message { get; set; } = "";

    public StatutReclamation Statut { get; set; } = StatutReclamation.Nouvelle;
    public string? Reponse { get; set; }
    public string? TraiteePar { get; set; }

    public DateTime DateCreation { get; set; } = DateTime.Now;
    public DateTime? DateTraitement { get; set; }
}