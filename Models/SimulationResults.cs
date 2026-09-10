using System;
using System.Collections.Generic;

namespace CerGucuSimulasyonu.Models;

public sealed record TrainSnapshot(
    string TrainName,
    string Line,
    string Direction,
    double PositionM,
    double SpeedKmh,
    double AccelerationMps2,
    double PowerKw,
    double CurrentA,
    double VoltageV,
    int PassengerCount,
    string State,
    string NearestSubstation);

public sealed record SubstationSnapshot(
    string Name,
    double PowerKw,
    double CurrentA,
    double VoltageV,
    double LoadPercent,
    int FedTrainCount,
    string State);

public sealed record SectionSnapshot(
    string Name,
    string Line,
    double PowerKw,
    double CurrentA,
    double AverageVoltageV,
    int ActiveTrainCount);

public sealed record SimulationSnapshot(
    int Second,
    double TractionPowerKw,
    double RegenerativePowerKw,
    double NetPowerKw,
    double ConsumedEnergyKwh,
    double RecoveredEnergyKwh,
    double MinimumTrainVoltageV,
    double AverageTrainVoltageV,
    double MaximumSubstationLoadPercent,
    double AverageTrainSpeedKmh,
    IReadOnlyList<TrainSnapshot> Trains,
    IReadOnlyList<SubstationSnapshot> Substations,
    IReadOnlyList<SectionSnapshot> Sections);

public sealed record ScenarioDefinition(
    string Id,
    string Name,
    string Description,
    double HeadwaySeconds,
    IReadOnlySet<string> OutOfServiceSubstations);

public sealed class SimulationRunResult
{
    public required ScenarioDefinition Scenario { get; init; }
    public DateTime StartedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    public int DurationSeconds => Snapshots.Count == 0 ? 0 : Snapshots[^1].Second;
    public List<SimulationSnapshot> Snapshots { get; } = new();

    public double MaximumTractionPowerKw => Max(s => s.TractionPowerKw);
    public double MaximumRegenerativePowerKw => Max(s => s.RegenerativePowerKw);
    public double MinimumTrainVoltageV => Min(s => s.MinimumTrainVoltageV);
    public double MaximumSubstationLoadPercent => Max(s => s.MaximumSubstationLoadPercent);
    public double TotalConsumedEnergyKwh => Snapshots.Count == 0 ? 0 : Snapshots[^1].ConsumedEnergyKwh;
    public double TotalRecoveredEnergyKwh => Snapshots.Count == 0 ? 0 : Snapshots[^1].RecoveredEnergyKwh;

    private double Max(Func<SimulationSnapshot, double> selector) =>
        Snapshots.Count == 0 ? 0 : Snapshots.Max(selector);

    private double Min(Func<SimulationSnapshot, double> selector) =>
        Snapshots.Count == 0 ? 0 : Snapshots.Min(selector);
}

public sealed record StandardReference(
    string Standard,
    string Edition,
    string Clause,
    string Requirement,
    string MeasuredMetric,
    string Limit,
    string Notes,
    string MetricKey = "");

public enum ComplianceStatus
{
    Pass,
    Fail,
    Review
}

public sealed record ComplianceResult(
    StandardReference Reference,
    ComplianceStatus Status,
    string ObservedValue,
    string Comment);
