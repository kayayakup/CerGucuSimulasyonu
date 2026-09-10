using System.Collections.Generic;
using System.IO;
using System.Linq;
using CerGucuSimulasyonu.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CerGucuSimulasyonu.Reporting;

public sealed class WordReportExporter
{
    public void Export(
        string filePath,
        SimulationRunResult result,
        IReadOnlyList<ComplianceResult> complianceResults)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        using var document = WordprocessingDocument.Create(
            filePath,
            WordprocessingDocumentType.Document);

        var mainPart = document.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());
        var body = mainPart.Document.Body!;

        AddParagraph(body, "CER GÜCÜ SİMÜLASYON RAPORU", "Title");
        AddParagraph(body, $"Senaryo: {result.Scenario.Name}", "Heading1");
        AddParagraph(body, result.Scenario.Description);
        AddParagraph(body, $"Süre: {result.DurationSeconds} s | Kayıt noktası: {result.Snapshots.Count}");

        AddParagraph(body, "Ana Sonuçlar", "Heading1");
        AddTable(body,
            new[] { "Metrik", "Değer" },
            new[]
            {
                new[] { "Maksimum cer gücü", $"{result.MaximumTractionPowerKw:0.0} kW" },
                new[] { "Maksimum rejeneratif güç", $"{result.MaximumRegenerativePowerKw:0.0} kW" },
                new[] { "Minimum tren gerilimi", $"{result.MinimumTrainVoltageV:0.0} V" },
                new[] { "Maksimum trafo yüklenmesi", $"{result.MaximumSubstationLoadPercent:0.0} %" },
                new[] { "Tüketilen enerji", $"{result.TotalConsumedEnergyKwh:0.00} kWh" },
                new[] { "Geri kazanılan enerji", $"{result.TotalRecoveredEnergyKwh:0.00} kWh" }
            });

        AddParagraph(body, "Standart Referansları ve Uygunluk", "Heading1");
        AddTable(body,
            new[] { "Standart", "Baskı", "Madde", "Durum", "Açıklama" },
            complianceResults.Select(item => new[]
            {
                item.Reference.Standard,
                item.Reference.Edition,
                item.Reference.Clause,
                item.Status.ToString().ToUpperInvariant(),
                item.Comment
            }).ToArray());

        AddParagraph(body, "Zaman Serisi Özeti (60 saniyelik örnekler)", "Heading1");
        AddTable(body,
            new[] { "Saniye", "Cer [kW]", "Rejen. [kW]", "Min. Gerilim [V]", "Max. TM Yük [%]" },
            result.Snapshots
                .Where(snapshot => snapshot.Second % 60 == 0 || snapshot == result.Snapshots[^1])
                .Select(snapshot => new[]
                {
                    snapshot.Second.ToString(),
                    $"{snapshot.TractionPowerKw:0.0}",
                    $"{snapshot.RegenerativePowerKw:0.0}",
                    $"{snapshot.MinimumTrainVoltageV:0.0}",
                    $"{snapshot.MaximumSubstationLoadPercent:0.0}"
                }).ToArray());

        mainPart.Document.Save();
    }

    private static void AddParagraph(Body body, string text, string? style = null)
    {
        var paragraph = new Paragraph();
        if (!string.IsNullOrWhiteSpace(style))
        {
            paragraph.ParagraphProperties = new ParagraphProperties(
                new ParagraphStyleId { Val = style });
        }
        paragraph.Append(new Run(new Text(text)));
        body.Append(paragraph);
    }

    private static void AddTable(Body body, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        var table = new Table(new TableProperties(new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 })));

        table.Append(CreateRow(headers, true));
        foreach (var row in rows)
        {
            table.Append(CreateRow(row, false));
        }

        body.Append(table);
    }

    private static TableRow CreateRow(IReadOnlyList<string> cells, bool header)
    {
        var row = new TableRow();
        foreach (var cellText in cells)
        {
            var run = new Run(new Text(cellText));
            if (header)
            {
                run.RunProperties = new RunProperties(new Bold());
            }
            row.Append(new TableCell(new Paragraph(run)));
        }
        return row;
    }
}
