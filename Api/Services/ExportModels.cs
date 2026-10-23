using PromoImmo.Domain;

namespace PromoImmo.Api.Services;

/// <summary>Un tableau à exporter (utilisé à l'identique pour le PDF et l'Excel).</summary>
public class ExportSection
{
    public string Titre { get; set; } = "";
    public string[] Colonnes { get; set; } = Array.Empty<string>();
    public List<object?[]> Lignes { get; } = new();
    public object?[]? Total { get; set; }
}
public class CachetDocument
{
    public ParametresSociete Branding { get; set; } = new();
    public byte[]? Qr { get; set; }
    public string Code { get; set; } = "";
    public string UrlVerification { get; set; } = "";
    public string EmisPar { get; set; } = "";
    public bool AvecSignature { get; set; } = true;
}