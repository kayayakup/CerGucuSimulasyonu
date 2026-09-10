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
                ComplianceStatus status = observed >= 1000
                    ? ComplianceStatus.Pass
                    : ComplianceStatus.Fail;
                return new ComplianceResult(
                    reference,
                    status,
                    $"{observed.ToString("0.0", CultureInfo.InvariantCulture)} V",
                    status == ComplianceStatus.Pass
                        ? "Kaydedilen minimum tren gerilimi 1000 V esiginin ustundedir."
                        : "Kaydedilen minimum tren gerilimi 1000 V esiginin altindadir.");
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
            default:
                return new ComplianceResult(
                    reference,
                    ComplianceStatus.Review,
                    "Olcum alani bekliyor",
                    "Kaynak baskisi, madde numarasi ve proje limiti dogrulanmalidir.");
        }
    }
}
