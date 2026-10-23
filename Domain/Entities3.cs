namespace PromoImmo.Domain;

public enum StatutLot { Disponible = 1, Reserve = 2, Vendu = 3 }

/// <summary>Bien à vendre (appartement, local…) rattaché à un chantier.</summary>
public class Lot : Auditable
{
    public int Id { get; set; }
    public int ChantierId { get; set; }
    public Chantier Chantier { get; set; } = null!;
    public string Reference { get; set; } = "";
    public string? TypeBien { get; set; }
    public string? Etage { get; set; }
    public decimal Surface { get; set; }
    public decimal Prix { get; set; }
    public StatutLot Statut { get; set; } = StatutLot.Disponible;
    public int? ClientId { get; set; }
    public Client? Client { get; set; }
    public int? ContratId { get; set; }
    public Contrat? Contrat { get; set; }
    public DateTime? DateReservation { get; set; }
    public DateTime? DateFinReservation { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Trace d'une relance envoyée à un client en retard de paiement.</summary>
public class Relance
{
    public int Id { get; set; }
    public int ContratId { get; set; }
    public Contrat Contrat { get; set; } = null!;
    public DateTime Date { get; set; } = DateTime.Now;
    public string Canal { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Par { get; set; }
}

/// <summary>Journal d'audit : qui a fait quoi, quand.</summary>
public class AuditLog
{
    public long Id { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
    public string? UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Action { get; set; } = "";
    public string Entite { get; set; } = "";
    public string? EntiteId { get; set; }
    public string? Details { get; set; }
    public string? Ip { get; set; }
}
