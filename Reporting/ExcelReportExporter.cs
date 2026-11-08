using System.Collections.Generic;
using System.Linq;
using CerGucuSimulasyonu.Models;
using ClosedXML.Excel;

namespace CerGucuSimulasyonu.Reporting;

public sealed class ExcelReportExporter
{
    public void Export(
        string filePath,
        SimulationRunResult result,
        IReadOnlyList<ComplianceResult> complianceResults)
    {
        using var workbook = new XLWorkbook();
        AddSummarySheet(workbook, result);
        AddTimeSeriesSheet(workbook, result);
        AddTrainSheet(workbook, result);
        AddSubstationSheet(workbook, result);
        AddStandardsSheet(workbook, complianceResults);
        workbook.SaveAs(filePath);
    }

    private static void AddSummarySheet(XLWorkbook workbook, SimulationRunResult result)
    {
        var sheet = workbook.Worksheets.Add("Summary");
        sheet.Cell("A1").Value = "CER GÜCÜ SİMÜLASYON RAPORU";
        sheet.Cell("A2").Value = "Senaryo";
        sheet.Cell("B2").Value = result.Scenario.Name;
        sheet.Cell("A3").Value = "Açıklama";
        sheet.Cell("B3").Value = result.Scenario.Description;
        sheet.Cell("A5").Value = "Metrik";
        sheet.Cell("B5").Value = "Değer";
        var rows = new[]
        {
            new[] { "Süre [s]", result.DurationSeconds.ToString() },
            new[] { "Kayıt noktası", result.Snapshots.Count.ToString() },
            new[] { "Maks. cer gücü [kW]", result.MaximumTractionPowerKw.ToString("0.0") },
            new[] { "Maks. rejeneratif güç [kW]", result.MaximumRegenerativePowerKw.ToString("0.0") },
            new[] { "Min. tren gerilimi [V]", result.MinimumTrainVoltageV.ToString("0.0") },
            new[] { "Maks. trafo yüklenmesi [%]", result.MaximumSubstationLoadPercent.ToString("0.0") },
            new[] { "Tüketilen enerji [kWh]", result.TotalConsumedEnergyKwh.ToString("0.00") },
            new[] { "Geri kazanılan enerji [kWh]", result.TotalRecoveredEnergyKwh.ToString("0.00") }
        };
        WriteTable(sheet, 6, 1, new[] { "Metrik", "Değer" }, rows);
        FormatSheet(sheet);
    }

    private static void AddTimeSeriesSheet(XLWorkbook workbook, SimulationRunResult result)
    {
        var sheet = workbook.Worksheets.Add("TimeSeries");
        var rows = result.Snapshots.Select(snapshot => new[]
        {
            snapshot.Second.ToString(),
            snapshot.TractionPowerKw.ToString("0.0"),
            snapshot.RegenerativePowerKw.ToString("0.0"),
            snapshot.NetPowerKw.ToString("0.0"),
            snapshot.ConsumedEnergyKwh.ToString("0.000"),
            snapshot.RecoveredEnergyKwh.ToString("0.000"),
            snapshot.MinimumTrainVoltageV.ToString("0.0"),
            snapshot.AverageTrainVoltageV.ToString("0.0"),
            snapshot.MaximumSubstationLoadPercent.ToString("0.0"),
            snapshot.AverageTrainSpeedKmh.ToString("0.0")
        }).ToArray();
        WriteTable(sheet, 1, 1,
            new[] { "Saniye", "Cer [kW]", "Rejen. [kW]", "Net [kW]", "Tüketim [kWh]", "Geri kazanım [kWh]", "Min. gerilim [V]", "Ort. gerilim [V]", "Max. TM yük [%]", "Ort. hız [km/h]" },
            rows);
        FormatSheet(sheet);
    }

    private static void AddTrainSheet(XLWorkbook workbook, SimulationRunResult result)
    {
        var sheet = workbook.Worksheets.Add("Trains");
        var entries = result.Snapshots.SelectMany(snapshot => snapshot.Trains.Select(train =>
            (Second: snapshot.Second, Train: train))).ToArray();
        var rows = entries.Select(entry => new[]
        {
            entry.Second.ToString(), entry.Train.TrainName, entry.Train.Line, entry.Train.Direction,
            entry.Train.PositionM.ToString("0.0"), entry.Train.SpeedKmh.ToString("0.0"),
            entry.Train.AccelerationMps2.ToString("0.00"), entry.Train.PowerKw.ToString("0.0"),
            entry.Train.CurrentA.ToString("0.0"), entry.Train.VoltageV.ToString("0.0"),
            entry.Train.TractionPowerKw.ToString("0.0"), entry.Train.TractionCurrentA.ToString("0.0"),
            entry.Train.PassengerCount.ToString(), entry.Train.State, entry.Train.NearestSubstation,
            entry.Train.LimitStatus
        }).ToArray();
        WriteTable(sheet, 1, 1,
            new[] { "Saniye", "Tren", "Hat", "Yön", "Konum [m]", "Hız [km/h]", "İvme [m/s²]", "Pantograf net güç [kW]", "Pantograf net I [A]", "Gerilim [V]", "Cer motor gücü (+/-) [kW]", "Cer motor I (+/-) [A]", "Yolcu", "Durum", "En yakın TM", "Limit Durumu" },
            rows);
        for (int index = 0; index < entries.Length; index++)
        {
            if (entries[index].Train.IsOutOfSpec)
            {
                sheet.Row(index + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FECACA");
                sheet.Row(index + 2).Style.Font.FontColor = XLColor.FromHtml("#7F1D1D");
            }
            else if (entries[index].Train.NeedsLimitReview)
            {
                sheet.Row(index + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                sheet.Row(index + 2).Style.Font.FontColor = XLColor.FromHtml("#78350F");
            }
        }
        FormatSheet(sheet);
    }

    private static void AddSubstationSheet(XLWorkbook workbook, SimulationRunResult result)
    {
        var sheet = workbook.Worksheets.Add("Substations");
        var entries = result.Snapshots.SelectMany(snapshot => snapshot.Substations.Select(substation =>
            (Second: snapshot.Second, Substation: substation))).ToArray();
        var rows = entries.Select(entry => new[]
        {
            entry.Second.ToString(), entry.Substation.Name, entry.Substation.PowerKw.ToString("0.0"),
            entry.Substation.CurrentA.ToString("0.0"), entry.Substation.VoltageV.ToString("0.0"),
            entry.Substation.LoadPercent.ToString("0.0"), entry.Substation.FedTrainCount.ToString(),
            entry.Substation.State, entry.Substation.LimitStatus
        }).ToArray();
        WriteTable(sheet, 1, 1,
            new[] { "Saniye", "TM", "Güç [kW]", "I [A]", "Gerilim [V]", "Yük [%]", "Beslenen tren", "Durum", "Limit Durumu" },
            rows);
        for (int index = 0; index < entries.Length; index++)
        {
            if (entries[index].Substation.IsOutOfSpec)
            {
                sheet.Row(index + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FECACA");
                sheet.Row(index + 2).Style.Font.FontColor = XLColor.FromHtml("#7F1D1D");
            }
            else if (entries[index].Substation.NeedsLimitReview)
            {
                sheet.Row(index + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF3C7");
                sheet.Row(index + 2).Style.Font.FontColor = XLColor.FromHtml("#78350F");
            }
        }
        FormatSheet(sheet);
    }

    private static void AddStandardsSheet(XLWorkbook workbook, IReadOnlyList<ComplianceResult> results)
    {
        var sheet = workbook.Worksheets.Add("Standards");
        var rows = results.Select(result => new[]
        {
            result.Reference.Standard,
            result.Reference.Edition,
            result.Reference.Clause,
            result.Reference.Requirement,
            result.Reference.MeasuredMetric,
            result.Reference.Limit,
            result.Status.ToString().ToUpperInvariant(),
            result.ObservedValue,
            result.Comment,
            result.Reference.Notes
        }).ToArray();
        WriteTable(sheet, 1, 1,
            new[] { "Standart", "Baskı/yıl", "Madde", "Gereksinim", "Ölçüm", "Limit", "Durum", "Gözlenen", "Açıklama", "Not" },
            rows);
        FormatSheet(sheet);
    }

    private static void WriteTable(IXLWorksheet sheet, int row, int column, IReadOnlyList<string> headers, IReadOnlyList<string[]> rows)
    {
        for (int index = 0; index < headers.Count; index++)
        {
            sheet.Cell(row, column + index).Value = headers[index];
        }

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < rows[rowIndex].Length; columnIndex++)
            {
                sheet.Cell(row + rowIndex + 1, column + columnIndex).Value = rows[rowIndex][columnIndex];
            }
        }

        var lastRow = row + rows.Count;
        var lastColumn = column + headers.Count - 1;
        sheet.Range(row, column, lastRow, lastColumn).CreateTable();
    }

    private static void FormatSheet(IXLWorksheet sheet)
    {
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();
    }
}
