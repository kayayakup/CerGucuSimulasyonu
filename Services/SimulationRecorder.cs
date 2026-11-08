using System;
using System.Collections.Generic;
using System.Linq;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public sealed class SimulationRecorder
{
    public const int MaximumDurationSeconds = 7 * 24 * 60 * 60;
    private const int DetailedSamplingDurationSeconds = 60 * 60;
    private const int LongRunSamplingIntervalSeconds = 10;

    private readonly int _maximumDurationSeconds;
    private int _lastRecordedSecond = -1;
    private double _consumedEnergyKwh;
    private double _recoveredEnergyKwh;
    private bool _isComplete;

    public SimulationRecorder(int maximumDurationSeconds = MaximumDurationSeconds)
    {
        if (maximumDurationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDurationSeconds));
        }

        _maximumDurationSeconds = maximumDurationSeconds;
    }

    public SimulationRunResult? CurrentRun { get; private set; }

    public bool IsComplete => _isComplete;

    public void Start(ScenarioDefinition scenario)
    {
        CurrentRun = new SimulationRunResult
        {
            Scenario = scenario,
            StartedAtUtc = DateTime.UtcNow
        };
        _lastRecordedSecond = -1;
        _consumedEnergyKwh = 0;
        _recoveredEnergyKwh = 0;
        _isComplete = false;
    }

    public bool CaptureIfDue(
        SimulationData data,
        double simulationTimeSeconds,
        double elapsedStepSeconds = 0)
    {
        if (CurrentRun is null)
        {
            throw new InvalidOperationException("Start must be called before capturing simulation data.");
        }

        double elapsedStep = Math.Max(0, elapsedStepSeconds);
        _consumedEnergyKwh += data.Trenler.Sum(train =>
            SimulationFormulas.EnergyKwh(
                Math.Max(0, train.AnlikPantografGucuKw),
                elapsedStep));
        _recoveredEnergyKwh += data.Trenler.Sum(train =>
            SimulationFormulas.EnergyKwh(
                Math.Max(0, -train.CekilenGucKw),
                elapsedStep));

        int elapsedSecond = (int)Math.Min(
            _maximumDurationSeconds,
            Math.Floor(Math.Max(0, simulationTimeSeconds)));
        _isComplete = simulationTimeSeconds >= _maximumDurationSeconds;
        int second = GetSnapshotSecond(elapsedSecond);
        if (_isComplete)
        {
            second = _maximumDurationSeconds;
        }

        if (second <= _lastRecordedSecond)
        {
            return false;
        }

        CurrentRun.Snapshots.Add(CreateSnapshot(
            data,
            second,
            _consumedEnergyKwh,
            _recoveredEnergyKwh));
        _lastRecordedSecond = second;
        if (_isComplete)
        {
            CurrentRun.CompletedAtUtc = DateTime.UtcNow;
        }

        return true;
    }

    private static int GetSnapshotSecond(int elapsedSecond)
    {
        if (elapsedSecond <= DetailedSamplingDurationSeconds)
        {
            return elapsedSecond;
        }

        return DetailedSamplingDurationSeconds +
               (elapsedSecond - DetailedSamplingDurationSeconds) /
               LongRunSamplingIntervalSeconds *
               LongRunSamplingIntervalSeconds;
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

    private static SimulationSnapshot CreateSnapshot(
        SimulationData data,
        int second,
        double consumedEnergyKwh,
        double recoveredEnergyKwh)
    {
        var trains = data.Trenler.Select(train => new TrainSnapshot(
            train.TrenAdi,
            train.HatTipi,
            train.Yon,
            train.Konum,
            train.AnlikHiz,
            train.Ivme,
            train.AnlikPantografGucuKw,
            train.AnlikPantografAkimiA,
            train.KatenerGerilimiV,
            train.CekilenGucKw,
            train.CekilenAkimA,
            train.YolcuSayisi,
            train.Durum,
            train.EnYakinTrafo,
            train.IsOutOfSpec,
            train.NeedsLimitReview,
            train.LimitStatus)).ToArray();

        var substations = data.TrafoMerkezleri.Select(substation => new SubstationSnapshot(
            substation.Ad,
            substation.AnlikGucKw,
            substation.AnlikAkimA,
            substation.AnlikGerilimV,
            substation.YuklenmeYuzdesi,
            substation.BeslenenTrenSayisi,
            substation.Durum,
            substation.IsOutOfSpec,
            substation.NeedsLimitReview,
            substation.LimitStatus)).ToArray();

        var sections = data.KatenerSeksiyonlari.Select(section => new SectionSnapshot(
            section.Ad,
            section.HatTipi,
            section.AnlikToplamGucKw,
            section.AnlikToplamAkimA,
            section.OrtalamaGerilimV,
            section.AktifTrenSayisi)).ToArray();

        double tractionPower = trains.Where(train => train.PowerKw > 0).Sum(train => train.PowerKw);
        double regenerativePower = trains
            .Where(train => train.TractionPowerKw < 0)
            .Sum(train => -train.TractionPowerKw);
        double netPower = trains.Sum(train => train.PowerKw);
        double minimumVoltage = trains.Length == 0 ? 0 : trains.Min(train => train.VoltageV);
        double averageVoltage = trains.Length == 0 ? 0 : trains.Average(train => train.VoltageV);
        double averageSpeed = trains.Length == 0 ? 0 : trains.Average(train => train.SpeedKmh);
        double maximumLoad = substations.Length == 0 ? 0 : substations.Max(substation => substation.LoadPercent);
        return new SimulationSnapshot(
            second,
            tractionPower,
            regenerativePower,
            netPower,
            consumedEnergyKwh,
            recoveredEnergyKwh,
            minimumVoltage,
            averageVoltage,
            maximumLoad,
            averageSpeed,
            trains,
            substations,
            sections);
    }
}
