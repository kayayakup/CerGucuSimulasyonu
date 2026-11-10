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

        if (timeStepSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeStepSeconds));
        }

        var results = new List<SimulationRunResult>(scenarios.Count);
        foreach (var scenario in scenarios)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var scenarioData = Clone(source);
            InitializeScenarioTrainsAtDepot(scenarioData);
            var engine = new RailwaySimulationEngine(
                scenarioData,
                scenario.OutOfServiceSubstations);
            var recorder = new SimulationRecorder(durationSeconds);
            recorder.Start(scenario);

            double elapsedSeconds = 0;
            recorder.CaptureIfDue(scenarioData, elapsedSeconds, 0);
            while (elapsedSeconds < durationSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                engine.AdvanceOneStep(timeStepSeconds);
                elapsedSeconds += timeStepSeconds;
                recorder.CaptureIfDue(scenarioData, elapsedSeconds, timeStepSeconds);

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

    private static void InitializeScenarioTrainsAtDepot(SimulationData data)
    {
        double lineLength = data.HatUzunlugu > 0 ? data.HatUzunlugu : 15394;
        double depotPosition = data.Istasyonlar
            .Where(station => station.Ad.Contains("depo", StringComparison.OrdinalIgnoreCase))
            .Select(station => station.H2OrtaNokta)
            .DefaultIfEmpty(lineLength)
            .Last();
        double headwaySeconds = data.Isletme.SeferAraligiHeadwaySaniye > 0
            ? data.Isletme.SeferAraligiHeadwaySaniye
            : 90;

        for (int i = 0; i < data.Trenler.Count; i++)
        {
            TrenKonumu train = data.Trenler[i];
            train.Konum = Math.Clamp(depotPosition, 0, lineLength);
            train.HatTipi = "H2";
            train.Yon = "geri";
            train.AnlikHiz = 0;
            train.IsDispatched = false;
            train.DispatchTimeSeconds = i * headwaySeconds;
            train.Durum = "DEPO SAHASINDA - SEFER BEKLİYOR";
        }
    }
}
