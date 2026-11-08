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

        AddParagraph(body, "Limit Dışı Trenler", "Heading1");
        var trainLimitRows = result.Snapshots
            .SelectMany(snapshot => snapshot.Trains
                .Where(train => train.IsOutOfSpec)
                .Select(train => (Second: snapshot.Second, Train: train)))
            .GroupBy(entry => entry.Train.TrainName)
            .Select(group =>
            {
                var entries = group.ToArray();
                return new[]
                {
                    group.Key,
                    entries.Min(entry => entry.Second).ToString(),
                    entries.Max(entry => entry.Second).ToString(),
                    string.Join(", ", entries.Select(entry => entry.Train.LimitStatus).Distinct()),
                    entries.Min(entry => entry.Train.VoltageV).ToString("0.0"),
                    entries.Max(entry => entry.Train.VoltageV).ToString("0.0"),
                    entries.Max(entry => entry.Train.SpeedKmh).ToString("0.0")
                };
            }).ToArray();
        if (trainLimitRows.Length == 0)
        {
            AddParagraph(body, "Koşu boyunca limit dışına çıkan tren kaydedilmedi.");
        }
        else
        {
            AddHighlightedTable(body,
                new[] { "Tren", "İlk saniye", "Son saniye", "Limit durumu", "Min. gerilim [V]", "Maks. gerilim [V]", "Maks. hız [km/h]" },
                trainLimitRows,
                "FECACA",
                "7F1D1D");
        }

        AddParagraph(body, "İnceleme Gerektiren Trenler", "Heading1");
        var trainReviewRows = result.Snapshots
            .SelectMany(snapshot => snapshot.Trains
                .Where(train => train.NeedsLimitReview && !train.IsOutOfSpec)
                .Select(train => (Second: snapshot.Second, Train: train)))
            .GroupBy(entry => entry.Train.TrainName)
            .Select(group =>
            {
                var entries = group.ToArray();
                return new[]
                {
                    group.Key,
                    entries.Min(entry => entry.Second).ToString(),
                    entries.Max(entry => entry.Second).ToString(),
                    string.Join(", ", entries.Select(entry => entry.Train.LimitStatus).Distinct()),
                    entries.Min(entry => entry.Train.VoltageV).ToString("0.0"),
                    entries.Max(entry => entry.Train.VoltageV).ToString("0.0"),
                    entries.Max(entry => entry.Train.SpeedKmh).ToString("0.0")
                };
            }).ToArray();
        if (trainReviewRows.Length > 0)
        {
            AddHighlightedTable(body,
                new[] { "Tren", "İlk saniye", "Son saniye", "İnceleme durumu", "Min. gerilim [V]", "Maks. gerilim [V]", "Maks. hız [km/h]" },
                trainReviewRows,
                "FEF3C7",
                "78350F");
        }
        else
        {
            AddParagraph(body, "Ek inceleme gerektiren tren kaydedilmedi.");
        }

        AddParagraph(body, "Limit / Duty İncelemesi Gerektiren Trafolar", "Heading1");
        var substationLimitRows = result.Snapshots
            .SelectMany(snapshot => snapshot.Substations
                .Where(substation => substation.IsOutOfSpec)
                .Select(substation => (Second: snapshot.Second, Substation: substation)))
            .GroupBy(entry => entry.Substation.Name)
            .Select(group =>
            {
                var entries = group.ToArray();
                return new[]
                {
                    group.Key,
                    entries.Min(entry => entry.Second).ToString(),
                    entries.Max(entry => entry.Second).ToString(),
                    string.Join(", ", entries.Select(entry => entry.Substation.LimitStatus).Distinct()),
                    entries.Max(entry => entry.Substation.LoadPercent).ToString("0.0"),
                    entries.Max(entry => entry.Substation.PowerKw).ToString("0.0"),
                    entries.Min(entry => entry.Substation.VoltageV).ToString("0.0")
                };
            }).ToArray();
        if (substationLimitRows.Length == 0)
        {
            AddParagraph(body, "Koşu boyunca limit dışına çıkan trafo kaydedilmedi.");
        }
        else
        {
            AddHighlightedTable(body,
                new[] { "Trafo", "İlk saniye", "Son saniye", "Limit durumu", "Maks. yük [%]", "Maks. güç [kW]", "Min. gerilim [V]" },
                substationLimitRows,
                "FECACA",
                "7F1D1D");
        }

        var substationReviewRows = result.Snapshots
            .SelectMany(snapshot => snapshot.Substations
                .Where(substation => substation.NeedsLimitReview && !substation.IsOutOfSpec)
                .Select(substation => (Second: snapshot.Second, Substation: substation)))
            .GroupBy(entry => entry.Substation.Name)
            .Select(group =>
            {
                var entries = group.ToArray();
                return new[]
                {
                    group.Key,
                    entries.Min(entry => entry.Second).ToString(),
                    entries.Max(entry => entry.Second).ToString(),
                    string.Join(", ", entries.Select(entry => entry.Substation.LimitStatus).Distinct()),
                    entries.Max(entry => entry.Substation.LoadPercent).ToString("0.0")
                };
            }).ToArray();
        if (substationReviewRows.Length > 0)
        {
            AddHighlightedTable(body,
                new[] { "Trafo", "İlk saniye", "Son saniye", "İnceleme durumu", "Maks. yük [%]" },
                substationReviewRows,
                "FEF3C7",
                "78350F");
        }
        else
        {
            AddParagraph(body, "Duty/RMS incelemesi gerektiren trafo kaydedilmedi.");
        }

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

    private static void AddHighlightedTable(
        Body body,
        IReadOnlyList<string> headers,
        IReadOnlyList<string[]> rows,
        string fill,
        string fontColor)
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
            table.Append(CreateRow(row, false, fill, fontColor));
        }

        body.Append(table);
    }

    private static TableRow CreateRow(
        IReadOnlyList<string> cells,
        bool header,
        string? fill = null,
        string? fontColor = null)
    {
        var row = new TableRow();
        foreach (var cellText in cells)
        {
            var run = new Run(new Text(cellText));
            if (header || fontColor is not null)
            {
                var properties = new RunProperties();
                if (header)
                {
                    properties.Append(new Bold());
                }
                if (fontColor is not null)
                {
                    properties.Append(new Color { Val = fontColor });
                }
                run.RunProperties = properties;
            }
            var tableCell = new TableCell(new Paragraph(run));
            if (fill is not null)
            {
                tableCell.TableCellProperties = new TableCellProperties(
                    new Shading { Val = ShadingPatternValues.Clear, Fill = fill });
            }
            row.Append(tableCell);
        }
        return row;
    }
}
