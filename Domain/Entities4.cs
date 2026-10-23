namespace PromoImmo.Domain;

public enum TypeMouvement { Entree = 1, Sortie = 2, Ajustement = 3 }

/// <summary>Fichier joint (contrat signé, CIN, facture, PV, photo de chantier…) stocké sur le disque du serveur.</summary>
public class DocumentJoint : Auditable
{
    public int Id { get; set; }
    /// <summary>Client | Contrat | Lot | Chantier | Rapport | Commande | Employe | Materiel | Reclamation</summary>
    public string Cible { get; set; } = "";
    public int CibleId { get; set; }
    public string Categorie { get; set; } = "Autre";
    public string NomFichier { get; set; } = "";
    public string TypeMime { get; set; } = "application/octet-stream";
    public long Taille { get; set; }
    /// <summary>Nom du fichier sur le disque (GUID) : le nom d'origine n'est jamais utilisé comme chemin.</summary>
    public string Chemin { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>Si vrai, le document apparaît dans l'espace client en ligne.</summary>
    public bool VisibleClient { get; set; }
}

public class Article : Auditable
{
    public int Id { get; set; }
    public string Designation { get; set; } = "";
    public string Unite { get; set; } = "u";
    public string? Categorie { get; set; }
    public decimal StockMin { get; set; }
    public decimal PrixUnitaire { get; set; }
    public bool Actif { get; set; } = true;
}

/// <summary>Entrée (+), sortie (−) ou ajustement d'inventaire (± quantité signée).</summary>
public class MouvementStock : Auditable
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public DateTime Date { get; set; } = DateTime.Now;
    public TypeMouvement Type { get; set; }
    public decimal Quantite { get; set; }
    public int? ChantierId { get; set; }
    public Chantier? Chantier { get; set; }
    public int? CommandeId { get; set; }
    public string? Motif { get; set; }
}
