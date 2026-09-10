using System;
using System.Collections.Generic;
using System.Linq;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public sealed class SimulationRecorder
{
    private readonly int _maximumDurationSeconds;
    private int _lastRecordedSecond = -1;

    public SimulationRecorder(int maximumDurationSeconds = 3600)
    {
        if (maximumDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDurationSeconds));
        }

        _maximumDurationSeconds = maximumDurationSeconds;
    }

    public SimulationRunResult? CurrentRun { get; private set; }

    public bool IsComplete => CurrentRun is not null &&
                              CurrentRun.DurationSeconds >= _maximumDurationSeconds;

    public void Start(ScenarioDefinition scenario)
    {
        CurrentRun = new SimulationRunResult
        {
            Scenario = scenario,
            StartedAtUtc = DateTime.UtcNow
        };
        _lastRecordedSecond = -1;
    }

    public bool CaptureIfDue(SimulationData data, double simulationTimeSeconds)
    {
        if (CurrentRun is null)
        {
            throw new InvalidOperationException("Start must be called before capturing simulation data.");
        }

        int second = Math.Min(_maximumDurationSeconds, (int)Math.Floor(simulationTimeSeconds));
        if (second <= _lastRecordedSecond)
        {
            return false;
        }

        CurrentRun.Snapshots.Add(CreateSnapshot(data, second));
        _lastRecordedSecond = second;
        if (second >= _maximumDurationSeconds)
        {
            CurrentRun.CompletedAtUtc = DateTime.UtcNow;
        }

        return true;
    }

    public SimulationRunResult Complete()
    {
        if (CurrentRun is null)
        {
            throw new InvalidOperationException("No simulation run is active.");
        }

        CurrentRun.CompletedAtUtc = DateTime.UtcNow;
        return CurrentRun;
    }

    private static SimulationSnapshot CreateSnapshot(SimulationData data, int second)
    {
        var trains = data.Trenler.Select(train => new TrainSnapshot(
            train.TrenAdi,
            train.HatTipi,
            train.Yon,
            train.Konum,
            train.AnlikHiz,
            train.Ivme,
            train.CekilenGucKw,
            train.CekilenAkimA,
            train.KatenerGerilimiV,
            train.YolcuSayisi,
            train.Durum,
            train.EnYakinTrafo)).ToArray();

        var substations = data.TrafoMerkezleri.Select(substation => new SubstationSnapshot(
            substation.Ad,
            substation.AnlikGucKw,
            substation.AnlikAkimA,
            substation.AnlikGerilimV,
            substation.YuklenmeYuzdesi,
            substation.BeslenenTrenSayisi,
            substation.Durum)).ToArray();

        var sections = data.KatenerSeksiyonlari.Select(section => new SectionSnapshot(
            section.Ad,
            section.HatTipi,
            section.AnlikToplamGucKw,
            section.AnlikToplamAkimA,
            section.OrtalamaGerilimV,
            section.AktifTrenSayisi)).ToArray();

        double tractionPower = trains.Where(train => train.PowerKw > 0).Sum(train => train.PowerKw);
        double regenerativePower = trains.Where(train => train.PowerKw < 0).Sum(train => -train.PowerKw);
        double minimumVoltage = trains.Length == 0 ? 0 : trains.Min(train => train.VoltageV);
        double averageVoltage = trains.Length == 0 ? 0 : trains.Average(train => train.VoltageV);
        double averageSpeed = trains.Length == 0 ? 0 : trains.Average(train => train.SpeedKmh);
        double maximumLoad = substations.Length == 0 ? 0 : substations.Max(substation => substation.LoadPercent);
        double consumedEnergy = trains.Sum(train => train.PowerKw > 0 ? train.PowerKw : 0) / 3600.0 * (second + 1);
        double recoveredEnergy = trains.Sum(train => train.PowerKw < 0 ? -train.PowerKw : 0) / 3600.0 * (second + 1);

        return new SimulationSnapshot(
            second,
            tractionPower,
            regenerativePower,
            tractionPower - regenerativePower,
            consumedEnergy,
            recoveredEnergy,
            minimumVoltage,
            averageVoltage,
            maximumLoad,
            averageSpeed,
            trains,
            substations,
            sections);
    }
}
