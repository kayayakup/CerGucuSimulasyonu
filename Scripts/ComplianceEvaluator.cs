using System.Collections.Generic;
using System.Globalization;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public sealed class ComplianceEvaluator
{
    public IReadOnlyList<ComplianceResult> Evaluate(
        SimulationRunResult result,
        IReadOnlyList<StandardReference> references)
    {
        var evaluations = new List<ComplianceResult>(references.Count);
        foreach (var reference in references)
        {
            evaluations.Add(EvaluateReference(result, reference));
        }

        return evaluations;
    }

    private static ComplianceResult EvaluateReference(
        SimulationRunResult result,
        StandardReference reference)
    {
        switch (reference.MetricKey)
        {
            case "MinimumTrainVoltage":
            {
                double observed = result.MinimumTrainVoltageV;
                return new ComplianceResult(
                    reference,
                    ComplianceStatus.Review,
                    $"{observed.ToString("0.0", CultureInfo.InvariantCulture)} V",
                    "Olculen minimum gerilim kaydedildi; standart baskisi, zaman esikleri ve uygulama kosullari dogrulanmadan PASS/FAIL verilemez.");
            }
            case "MaximumSubstationLoad":
            {
                double observed = result.MaximumSubstationLoadPercent;
                return new ComplianceResult(
                    reference,
                    ComplianceStatus.Review,
                    $"{observed.ToString("0.0", CultureInfo.InvariantCulture)} %",
                    "Trafo Duty Class ve proje limitleri tanimlanmadan otomatik PASS/FAIL verilmez.");
            }
            case "ValidationEvidence":
                return new ComplianceResult(
                    reference,
                    ComplianceStatus.Review,
                    "Bagimsiz dogrulama kaniti gerekli",
                    "Verilen rapordaki Hi-SimuX/SimuX dogrulama beyanı bu uygulamayi dogrulamaz.");
            default:
                return new ComplianceResult(
                    reference,
                    ComplianceStatus.Review,
                    "Olcum alani bekliyor",
                    "Kaynak baskisi, madde numarasi ve proje limiti dogrulanmalidir.");
        }
    }
}
