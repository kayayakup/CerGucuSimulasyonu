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
    private double _lastTrainConsumedEnergyKwh;
    private double _lastTrainRecoveredEnergyKwh;
    private bool _hasTrainEnergyBaseline;
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
        _lastTrainConsumedEnergyKwh = 0;
        _lastTrainRecoveredEnergyKwh = 0;
        _hasTrainEnergyBaseline = false;
        _isComplete = false;
    }

    public bool CaptureIfDue(SimulationData data, double simulationTimeSeconds)
    {
        if (CurrentRun is null)
        {
            throw new InvalidOperationException("Start must be called before capturing simulation data.");
        }
        if (!double.IsFinite(simulationTimeSeconds) || simulationTimeSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(simulationTimeSeconds));
        }

        double trainConsumedEnergyKwh = data.Trenler.Sum(train => train.ToplamTuketilenEnerjiKwh);
        double trainRecoveredEnergyKwh = data.Trenler.Sum(train => train.ToplamRejeneratifEnerjiKwh);
        if (_hasTrainEnergyBaseline)
        {
            _consumedEnergyKwh += Math.Max(0, trainConsumedEnergyKwh - _lastTrainConsumedEnergyKwh);
            _recoveredEnergyKwh += Math.Max(0, trainRecoveredEnergyKwh - _lastTrainRecoveredEnergyKwh);
        }

        _lastTrainConsumedEnergyKwh = trainConsumedEnergyKwh;
        _lastTrainRecoveredEnergyKwh = trainRecoveredEnergyKwh;
        _hasTrainEnergyBaseline = true;

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
            train.AnlikMekanikFrenKuvvetiKn,
            train.AnlikPantografGucuKw,
            train.AnlikPantografAkimiA,
            train.KatenerGerilimiV,
            train.CekilenGucKw,
            train.CekilenAkimA,
            train.YolcuSayisi,
            $"{train.IsletimDurumu} ({train.Durum})",
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
            substation.AnlikDogrultucuGucKw,
            substation.AnlikDogrultucuAkimiA,
            substation.AnlikKopruDogrultucuGucuKw,
            substation.AnlikKopruDogrultucuAkimiA,
            substation.AnlikDcDalgalanmaFrekansiHz,
            substation.AnlikRejeneratifDcGucKw,
            substation.AnlikAcGucKw,
            substation.AnlikAcGorunurGucKva,
            substation.AnlikAcAkimiA,
            substation.TrafoYuklenmeYuzdesi,
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

        TrainSnapshot[] activeTrains = trains
            .Where(train => !train.State.StartsWith("SEFER BEKLİYOR", StringComparison.Ordinal))
            .ToArray();
        TrainSnapshot[] poweredTrains = activeTrains
            .Where(train => train.VoltageV > 0)
            .ToArray();
        double tractionPower = trains
            .Where(train => train.TractionPowerKw > 0)
            .Sum(train => train.TractionPowerKw);
        double regenerativePower = trains
            .Where(train => train.TractionPowerKw < 0)
            .Sum(train => -train.TractionPowerKw);
        double netPower = poweredTrains.Sum(train => train.PowerKw);
        double minimumVoltage = poweredTrains.Length == 0 ? 0 : poweredTrains.Min(train => train.VoltageV);
        double averageVoltage = poweredTrains.Length == 0 ? 0 : poweredTrains.Average(train => train.VoltageV);
        double averageSpeed = activeTrains.Length == 0 ? 0 : activeTrains.Average(train => train.SpeedKmh);
        SubstationSnapshot[] activeSubstations = substations
            .Where(substation => substation.State != "DEVRE DIŞI")
            .ToArray();
        double maximumLoad = activeSubstations.Length == 0 ? 0 : activeSubstations.Max(substation => substation.LoadPercent);
        double maximumRectifierLoad = maximumLoad;
        double maximumTransformerLoad = activeSubstations.Length == 0
            ? 0
            : activeSubstations.Max(substation => substation.TransformerLoadPercent);
        return new SimulationSnapshot(
            second,
            tractionPower,
            regenerativePower,
            netPower,
            consumedEnergyKwh,
            recoveredEnergyKwh,
            minimumVoltage,
            averageVoltage,
            Math.Max(maximumRectifierLoad, maximumTransformerLoad),
            maximumRectifierLoad,
            maximumTransformerLoad,
            averageSpeed,
            trains,
            substations,
            sections);
    }
}
