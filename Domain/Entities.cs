using Microsoft.AspNetCore.Identity;

namespace PromoImmo.Domain;

public enum ModePaiement { Especes = 1, Bancaire = 2 }
public enum TypeMateriel { Roulant = 1, NonRoulant = 2 }
public enum StatutChantier { EnCours = 1, Termine = 2, Suspendu = 3 }

public abstract class Auditable
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}

public class AppUser : IdentityUser
{
    public string? NomComplet { get; set; }
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
}

public class Client : Auditable
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Cin { get; set; }
    public string? Adresse { get; set; }
    public string? Email { get; set; }
    public List<Contrat> Contrats { get; set; } = new();
}

public class Chantier : Auditable
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Localisation { get; set; }
    public StatutChantier Statut { get; set; } = StatutChantier.EnCours;
    public decimal BudgetPrevu { get; set; }
}

public class Contrat : Auditable
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public int ChantierId { get; set; }
    public Chantier Chantier { get; set; } = null!;
    public string Lot { get; set; } = "";
    public decimal PrixTotal { get; set; }
    public List<Echeance> Echeances { get; set; } = new();
    public List<Encaissement> Encaissements { get; set; } = new();
}

public class Echeance
{
    public int Id { get; set; }
    public int ContratId { get; set; }
    public int Numero { get; set; }
    public decimal MontantPrevu { get; set; }
    public DateTime DatePrevue { get; set; }
}

public class Encaissement : Auditable
{
    public int Id { get; set; }
    public int ContratId { get; set; }
    public Contrat Contrat { get; set; } = null!;
    public int? EcheanceId { get; set; }
    public decimal Montant { get; set; }
    public ModePaiement Mode { get; set; }
    public string? RefBancaire { get; set; }        // n° chèque / virement
    public string NumeroRecu { get; set; } = "";
    public DateTime DateEncaissement { get; set; } = DateTime.UtcNow;
    public string AgentId { get; set; } = "";
    public bool Annule { get; set; }                // jamais de suppression
    public int? EncaissementOrigineId { get; set; } // écriture inverse
}

public class Notification
{
    public int Id { get; set; }
    public string DestinataireId { get; set; } = "";
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public int? ReferenceId { get; set; }
    public bool Lu { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

public class AllocationBudget : Auditable
{
    public int Id { get; set; }
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public string? Service { get; set; }            // ex: "Matériel roulant"
    public decimal Montant { get; set; }
    public string Motif { get; set; } = "";
    public string DecidePar { get; set; } = "";
}

public class Materiel : Auditable
{
    public int Id { get; set; }
    public string Designation { get; set; } = "";
    public TypeMateriel Type { get; set; }
    public string? Immatriculation { get; set; }
    public string Statut { get; set; } = "En service";
    public List<Entretien> Entretiens { get; set; } = new();
    public List<PaiementMateriel> Paiements { get; set; } = new();
}

public class Entretien : Auditable
{
    public int Id { get; set; }
    public int MaterielId { get; set; }
    public Materiel Materiel { get; set; } = null!;
    public string Type { get; set; } = "";
    public DateTime Date { get; set; }
    public string? Description { get; set; }
}

public class PaiementMateriel : Auditable
{
    public int Id { get; set; }
    public int MaterielId { get; set; }
    public Materiel Materiel { get; set; } = null!;
    public int? EntretienId { get; set; }
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public decimal Montant { get; set; }
    public string Motif { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.UtcNow;
}
