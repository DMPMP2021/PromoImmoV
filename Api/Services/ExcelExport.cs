using ClosedXML.Excel;
using PromoImmo.Api.Services;

namespace PromoImmo.Api.Controllers;

public static class ExcelExport
{
    public static byte[] Generer(string titre, string sousTitre, CachetDocument cachet, List<ExportSection> sections)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Rapport");

        int currentRow = 1;

        // En-tête de l'entreprise
        ws.Cell(currentRow, 1).Value = cachet.Branding.Nom;
        ws.Cell(currentRow, 1).Style.Font.Bold = true;
        ws.Cell(currentRow, 1).Style.Font.FontSize = 14;
        currentRow++;

        if (!string.IsNullOrWhiteSpace(cachet.Branding.Adresse))
        {
            ws.Cell(currentRow, 1).Value = cachet.Branding.Adresse;
            currentRow++;
        }

        currentRow++;
        // Titre du document
        ws.Cell(currentRow, 1).Value = titre;
        ws.Cell(currentRow, 1).Style.Font.Bold = true;
        ws.Cell(currentRow, 1).Style.Font.FontSize = 12;
        currentRow++;

        if (!string.IsNullOrWhiteSpace(sousTitre))
        {
            ws.Cell(currentRow, 1).Value = sousTitre;
            ws.Cell(currentRow, 1).Style.Font.Italic = true;
            currentRow++;
        }

        currentRow++;

        // Sections / Tableaux
        foreach (var section in sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Titre))
            {
                ws.Cell(currentRow, 1).Value = section.Titre;
                ws.Cell(currentRow, 1).Style.Font.Bold = true;
                ws.Cell(currentRow, 1).Style.Font.FontColor = XLColor.Blue;
                currentRow++;
            }

            // Colonnes
            for (int i = 0; i < section.Colonnes.Length; i++)
            {
                var cell = ws.Cell(currentRow, i + 1);
                cell.Value = section.Colonnes[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            }
            currentRow++;

            // Lignes
            foreach (var ligne in section.Lignes)
            {
                for (int i = 0; i < ligne.Length; i++)
                {
                    var val = ligne[i];
                    var cell = ws.Cell(currentRow, i + 1);

                    if (val is decimal d) cell.Value = d;
                    else if (val is int n) cell.Value = n;
                    else if (val is double dub) cell.Value = dub;
                    else cell.Value = val?.ToString() ?? "";
                }
                currentRow++;
            }

            // Total
            if (section.Total != null)
            {
                for (int i = 0; i < section.Total.Length; i++)
                {
                    var val = section.Total[i];
                    var cell = ws.Cell(currentRow, i + 1);
                    cell.Value = val?.ToString() ?? "";
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.LightSlateGray;
                }
                currentRow++;
            }

            currentRow += 2; // Espace entre les sections
        }

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}