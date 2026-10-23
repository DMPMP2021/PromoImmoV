using System.ComponentModel.DataAnnotations;
using PromoImmo.Domain;

namespace PromoImmo.Api.Dtos;

public class EncaissementCreateDto
{
    [Required] public int ContratId { get; set; }
    public int? EcheanceId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal Montant { get; set; }
    [Required] public ModePaiement Mode { get; set; }
    public string? RefBancaire { get; set; }
}

public record EncaissementDto(int Id, string NumeroRecu, decimal Montant,
    string Mode, string? RefBancaire, DateTime Date, bool Annule, string Agent);

public record HistoriqueClientDto(int ClientId, string Nom, List<ContratHistoriqueDto> Contrats);
public record ContratHistoriqueDto(int ContratId, string Chantier, string Lot,
    decimal PrixTotal, decimal TotalPaye, decimal Reste, List<EncaissementDto> Encaissements);

public class AnnulationDto { [Required] public string Motif { get; set; } = ""; }
