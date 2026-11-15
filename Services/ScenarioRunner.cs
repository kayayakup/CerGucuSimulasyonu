using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public sealed class ScenarioRunner
{
    private readonly JsonSerializerOptions _cloneOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<SimulationRunResult>> RunAsync(
        SimulationData source,
        IReadOnlyList<ScenarioDefinition> scenarios,
        int durationSeconds = 3600,
        double timeStepSeconds = 0.2,
        CancellationToken cancellationToken = default)
    {
        if (durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (!double.IsFinite(timeStepSeconds) || timeStepSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds));
        }

        var results = new List<SimulationRunResult>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scenarioData = Clone(source);
            InitializeScenarioTrainsAtDepot(scenarioData, scenario.HeadwaySeconds);
            var engine = new RailwaySimulationEngine(
                scenarioData,
                scenario.OutOfServiceSubstations);
            var recorder = new SimulationRecorder(durationSeconds);
            recorder.Start(scenario);

            double elapsedSeconds = 0;
            recorder.CaptureIfDue(scenarioData, elapsedSeconds);
            while (elapsedSeconds < durationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double stepSeconds = Math.Min(timeStepSeconds, durationSeconds - elapsedSeconds);
                engine.AdvanceOneStep(stepSeconds);
                elapsedSeconds += stepSeconds;
                recorder.CaptureIfDue(scenarioData, elapsedSeconds);

                if (((int)(elapsedSeconds / timeStepSeconds)) % 25 == 0)
                {
                    await Task.Yield();
                }
            }

            results.Add(recorder.Complete());
        }

        return results;
    }

    private SimulationData Clone(SimulationData source)
    {
        string json = JsonSerializer.Serialize(source, _cloneOptions);
        return JsonSerializer.Deserialize<SimulationData>(json, _cloneOptions)
            ?? throw new InvalidOperationException("Simulation data could not be cloned.");
    }

    private static void InitializeScenarioTrainsAtDepot(
        SimulationData data,
        double headwaySeconds)
    {
        if (!double.IsFinite(headwaySeconds) || headwaySeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(headwaySeconds));
        }

        double lineLength = data.HatUzunlugu > 0 ? data.HatUzunlugu : 15394;
        double depotPosition = data.Istasyonlar
            .Where(station => station.Ad.Contains("depo", StringComparison.OrdinalIgnoreCase))
            .Select(station => station.H2OrtaNokta)
            .DefaultIfEmpty(lineLength)
            .Last();
        for (int i = 0; i < data.Trenler.Count; i++)
        {
            TrenKonumu train = data.Trenler[i];
            train.Konum = Math.Clamp(depotPosition, 0, lineLength);
            train.HatTipi = "H2";
            train.Yon = "geri";
            train.AnlikHiz = 0;
            train.Ivme = 0;
            train.CekilenGucKw = 0;
            train.CekilenAkimA = 0;
            train.AnlikCekisKuvvetiKn = 0;
            train.AnlikRejeneratifFrenKuvvetiKn = 0;
            train.AnlikMekanikFrenKuvvetiKn = 0;
            train.AnlikPantografGucuKw = 0;
            train.AnlikPantografAkimiA = 0;
            train.ToplamTuketilenEnerjiKwh = 0;
            train.ToplamRejeneratifEnerjiKwh = 0;
            train.KatEdilenMesafeKm = 0;
            train.IstasyonBeklemeSayaci = 0;
            train.EnYakinTrafo = "";
            train.IsDispatched = false;
            train.DispatchTimeSeconds = i * headwaySeconds;
            train.Durum = "DEPO SAHASINDA - SEFER BEKLİYOR";
            train.IsletimDurumu = "SEFER BEKLİYOR";
            train.IsOutOfSpec = false;
            train.NeedsLimitReview = false;
            train.LimitStatus = "SEFER BEKLİYOR";
        }
    }
}
