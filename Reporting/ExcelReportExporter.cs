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
        var rows = result.Snapshots.SelectMany(snapshot => snapshot.Trains.Select(train => new[]
        {
            snapshot.Second.ToString(), train.TrainName, train.Line, train.Direction,
            train.PositionM.ToString("0.0"), train.SpeedKmh.ToString("0.0"),
            train.AccelerationMps2.ToString("0.00"), train.PowerKw.ToString("0.0"),
            train.CurrentA.ToString("0.0"), train.VoltageV.ToString("0.0"),
            train.PassengerCount.ToString(), train.State, train.NearestSubstation
        })).ToArray();
        WriteTable(sheet, 1, 1,
            new[] { "Saniye", "Tren", "Hat", "Yön", "Konum [m]", "Hız [km/h]", "İvme [m/s²]", "Güç [kW]", "Akım [A]", "Gerilim [V]", "Yolcu", "Durum", "En yakın TM" },
            rows);
        FormatSheet(sheet);
    }

    private static void AddSubstationSheet(XLWorkbook workbook, SimulationRunResult result)
    {
        var sheet = workbook.Worksheets.Add("Substations");
        var rows = result.Snapshots.SelectMany(snapshot => snapshot.Substations.Select(substation => new[]
        {
            snapshot.Second.ToString(), substation.Name, substation.PowerKw.ToString("0.0"),
            substation.CurrentA.ToString("0.0"), substation.VoltageV.ToString("0.0"),
            substation.LoadPercent.ToString("0.0"), substation.FedTrainCount.ToString(), substation.State
        })).ToArray();
        WriteTable(sheet, 1, 1,
            new[] { "Saniye", "TM", "Güç [kW]", "Akım [A]", "Gerilim [V]", "Yük [%]", "Beslenen tren", "Durum" },
            rows);
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
