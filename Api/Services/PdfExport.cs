using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PromoImmo.Api.Services;

namespace PromoImmo.Api.Controllers;

public static class PdfExport
{
    public static byte[] Generer(string titre, string sousTitre, CachetDocument cachet, List<ExportSection> sections)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                // EN-TÊTE
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(cachet.Branding.Nom).Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                            if (!string.IsNullOrWhiteSpace(cachet.Branding.Adresse))
                                c.Item().Text(cachet.Branding.Adresse).FontSize(9).FontColor(Colors.Grey.Medium);
                            if (!string.IsNullOrWhiteSpace(cachet.Branding.Identifiants))
                                c.Item().Text(cachet.Branding.Identifiants).FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        if (cachet.Branding.Logo != null)
                        {
                            row.ConstantItem(80).Height(40).Image(cachet.Branding.Logo);
                        }
                    });
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // CONTENU PRINCIPAL
                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text(titre).Bold().FontSize(14).FontColor(Colors.Grey.Darken3);
                    if (!string.IsNullOrWhiteSpace(sousTitre))
                        col.Item().Text(sousTitre).FontSize(10).Italic().FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingVertical(5);

                    foreach (var section in sections)
                    {
                        if (!string.IsNullOrWhiteSpace(section.Titre))
                        {
                            col.Item().PaddingTop(8).Text(section.Titre).SemiBold().FontSize(11).FontColor(Colors.Blue.Medium);
                        }

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var _ in section.Colonnes)
                                    columns.RelativeColumn();
                            });

                            // En-têtes du tableau
                            table.Header(header =>
                            {
                                foreach (var colName in section.Colonnes)
                                {
                                    header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(colName).Bold().FontSize(9);
                                }
                            });

                            // Lignes du tableau
                            foreach (var ligne in section.Lignes)
                            {
                                foreach (var cell in ligne)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(cell?.ToString() ?? "").FontSize(9);
                                }
                            }
                        });

                        // Ligne de total si présente
                        if (section.Total != null)
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    foreach (var _ in section.Colonnes)
                                        columns.RelativeColumn();
                                });
                                foreach (var cell in section.Total)
                                {
                                    table.Cell().Background(Colors.Grey.Lighten4).Padding(4).Text(cell?.ToString() ?? "").Bold().FontSize(9);
                                }
                            });
                        }
                        col.Item().PaddingVertical(3);
                    }

                    // CACHET / SIGNATURE & QR CODE
                    col.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Émis par : {cachet.EmisPar}").FontSize(8).Italic();
                            c.Item().Text($"Code : {cachet.Code}").FontSize(8).FontColor(Colors.Grey.Medium);
                            if (cachet.AvecSignature && cachet.Branding.Signature != null)
                            {
                                c.Item().PaddingTop(5).Height(40).Image(cachet.Branding.Signature);
                                if (!string.IsNullOrWhiteSpace(cachet.Branding.SignataireNom))
                                    c.Item().Text($"{cachet.Branding.SignataireNom} — {cachet.Branding.SignataireFonction}").FontSize(8);
                            }
                        });

                        if (cachet.Qr != null)
                        {
                            row.ConstantItem(70).Height(70).Image(cachet.Qr);
                        }
                    });
                });

                // PIED DE PAGE
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span(cachet.Branding.PiedDePage ?? "PromoImmo — Document officiel sécurisé").FontSize(8).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }
}