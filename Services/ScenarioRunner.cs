using System;
using System.Collections.Generic;
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
                engine.AdvanceOneStep(timeStepSeconds);
                elapsedSeconds += timeStepSeconds;
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
}
