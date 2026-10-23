namespace PromoImmo.Domain;

/// <summary>Identité de l'entreprise : une seule ligne. Sert à l'en-tête, à la signature et au QR code des documents officiels.</summary>
public class ParametresSociete
{
    public int Id { get; set; }
    public string Nom { get; set; } = "PromoImmo";
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Email { get; set; }
    /// <summary>Ligne légale libre : NIF, RC, NIS, AI…</summary>
    public string? Identifiants { get; set; }
    public string? PiedDePage { get; set; }
    /// <summary>Adresse de l'application utilisée dans le QR code (ex : https://gestion.monentreprise.dz).</summary>
    public string? UrlPublique { get; set; }
    public byte[]? Logo { get; set; }
    public string? LogoMime { get; set; }
    public byte[]? Signature { get; set; }
    public string? SignatureMime { get; set; }
    public string? SignataireNom { get; set; }
    public string? SignataireFonction { get; set; }
    public DateTime ModifieLe { get; set; } = DateTime.Now;
}

/// <summary>Trace de chaque document officiel émis : permet de vérifier son authenticité via le QR code.</summary>
public class DocumentEmis
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public string Titre { get; set; } = "";
    public string? SousTitre { get; set; }
    public DateTime DateEmission { get; set; } = DateTime.Now;
    public string EmisPar { get; set; } = "";
    /// <summary>Empreinte SHA-256 du contenu des tableaux : détecte toute modification ultérieure des chiffres.</summary>
    public string Empreinte { get; set; } = "";
}
