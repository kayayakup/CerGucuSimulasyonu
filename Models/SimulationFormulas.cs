using System;
using System.Collections.Generic;
using System.Linq;

namespace CerGucuSimulasyonu.Models;

public static class SimulationFormulas
{
    /// <summary>Calculates a non-negative length from its start and end coordinates.</summary>
    public static double Length(double start, double end) => Math.Max(0, end - start);

    /// <summary>Converts a grade percentage to its absolute permille value for display.</summary>
    public static double GradePermille(double gradePercent) =>
        Math.Abs(Math.Round(gradePercent * 10, 1));

    /// <summary>Calculates gravity force along the track; uphill is positive and downhill negative.</summary>
    public static double GradeForceKn(double massTon, double gradePercent, string direction) =>
        massTon * 9.81 * gradePercent / 100.0 * (direction == "geri" ? -1 : 1);

    /// <summary>Calculates the Röckl curve-resistance coefficient from curve radius.</summary>
    public static double CurveResistanceCoefficient(double radiusM) =>
        radiusM > 55 ? Math.Round(650.0 / (radiusM - 55.0), 2) : 0;

    /// <summary>Calculates the curve speed limit from radius, cant ratio and gravity.</summary>
    public static double CurveSpeedLimitKmh(double radiusM) =>
        Math.Round(Math.Sqrt(Math.Max(10, radiusM) * 11.8 * 0.15) * 3.6, 0);

    /// <summary>Calculates the grade-force value shown for a 236-ton reference train.</summary>
    public static double ReferenceGradeForceKn(double gradePercent) =>
        Math.Round(236.0 * 9.81 * gradePercent / 100.0, 1);

    /// <summary>Calculates rotating mass as a percentage of the empty vehicle mass.</summary>
    public static double RotatingMassTon(double emptyMassTon, double rotatingMassPercent) =>
        Math.Max(0, emptyMassTon) * Math.Max(0, rotatingMassPercent) / 100.0;

    /// <summary>Adds rotating mass to train mass for acceleration calculations.</summary>
    public static double EffectiveTrainMassTon(double trainMassTon, double rotatingMassTon) =>
        trainMassTon + rotatingMassTon;

    /// <summary>Calculates the inertial force required for a train's acceleration in kN.</summary>
    public static double InertialForceKn(double effectiveMassTon, double accelerationMps2) =>
        effectiveMassTon * accelerationMps2;

    /// <summary>Adds running resistance to inertial force to obtain required traction effort.</summary>
    public static double RequiredTractionEffortKn(double inertialForceKn, double resistanceKn) =>
        Math.Max(0, inertialForceKn + resistanceKn);

    /// <summary>Calculates braking effort needed to decelerate the effective train mass.</summary>
    public static double BrakingEffortKn(double effectiveMassTon, double decelerationMps2) =>
        effectiveMassTon * Math.Abs(decelerationMps2);

    /// <summary>Limits requested mechanical effort to the vehicle's available force.</summary>
    public static double LimitedEffortKn(double requestedEffortKn, double availableEffortKn) =>
        Math.Min(requestedEffortKn, availableEffortKn);

    /// <summary>Calculates the auxiliary electrical load after applying its utilization ratio.</summary>
    public static double AuxiliaryPowerKw(double ratedPowerKw, double utilizationRatio) =>
        Math.Max(0, ratedPowerKw) * Math.Clamp(utilizationRatio, 0, 1);

    /// <summary>Converts a speed expressed in km/h to m/s for mechanical power calculations.</summary>
    public static double SpeedMetersPerSecond(double speedKmh) => speedKmh / 3.6;

    /// <summary>Converts a percentage value, such as motor efficiency, into a unit ratio.</summary>
    public static double PercentToRatio(double percent) => percent / 100.0;

    /// <summary>Calculates the Davis A+Bv+Cv² running resistance in kN.</summary>
    public static double DavisResistanceKn(
        double massTon,
        double speedKmh,
        double coefficientA,
        double coefficientB,
        double coefficientC) =>
        (coefficientA * massTon +
         coefficientB * massTon * speedKmh +
         coefficientC * Math.Pow(speedKmh, 2)) / 1000.0;

    /// <summary>Converts a curve-resistance coefficient to force in kN for the specified mass.</summary>
    public static double CurveResistanceKn(double massTon, double resistanceCoefficient) =>
        massTon * 9.81 * resistanceCoefficient / 1000.0;

    /// <summary>Interpolates a current limit at the requested voltage from the vehicle current curve.</summary>
    public static double InterpolateCurrent(
        IReadOnlyList<TrainCurrentCurvePoint> curve,
        double voltageV,
        Func<TrainCurrentCurvePoint, double> selector)
    {
        if (curve.Count == 0)
        {
            return 0;
        }

        if (voltageV <= curve[0].VoltageV)
        {
            return selector(curve[0]);
        }

        for (int i = 1; i < curve.Count; i++)
        {
            TrainCurrentCurvePoint previous = curve[i - 1];
            TrainCurrentCurvePoint next = curve[i];
            if (voltageV <= next.VoltageV)
            {
                double ratio = InterpolationRatio(
                    voltageV,
                    previous.VoltageV,
                    next.VoltageV);
                return selector(previous) +
                       (selector(next) - selector(previous)) * ratio;
            }
        }

        return selector(curve[^1]);
    }

    /// <summary>Interpolates the force-speed curve at the requested train speed.</summary>
    public static double InterpolateForce(
        IReadOnlyList<TrainForceCurvePoint> curve,
        double speedKmh)
    {
        if (curve.Count == 0)
        {
            return 0;
        }

        if (speedKmh <= curve[0].SpeedKmh)
        {
            return curve[0].ForceKn;
        }

        for (int i = 1; i < curve.Count; i++)
        {
            TrainForceCurvePoint previous = curve[i - 1];
            TrainForceCurvePoint next = curve[i];
            if (speedKmh <= next.SpeedKmh)
            {
                double ratio = InterpolationRatio(
                    speedKmh,
                    previous.SpeedKmh,
                    next.SpeedKmh);
                return previous.ForceKn + (next.ForceKn - previous.ForceKn) * ratio;
            }
        }

        return curve[^1].ForceKn;
    }

    /// <summary>Finds the first zero-current point above the permitted operating voltage.</summary>
    public static double RegenerativeVoltageCutoffV(
        IReadOnlyList<TrainCurrentCurvePoint> curve,
        double maximumOperatingVoltageV)
    {
        TrainCurrentCurvePoint? cutoff = curve
            .Where(point =>
                point.VoltageV > maximumOperatingVoltageV &&
                point.TractionCurrentA <= 0 &&
                point.RegenerativeCurrentA >= 0)
            .OrderBy(point => point.VoltageV)
            .FirstOrDefault();
        return cutoff?.VoltageV ?? maximumOperatingVoltageV;
    }

    /// <summary>Calculates the train-set traction-force limit from its speed curve and configured cap.</summary>
    public static double TractionEffortAtSpeedKn(
        IReadOnlyList<TrainForceCurvePoint> curve,
        double maximumTractionForceKn,
        double speedKmh)
    {
        double force = InterpolateForce(curve, speedKmh);
        return maximumTractionForceKn > 0
            ? Math.Min(force, maximumTractionForceKn)
            : force;
    }

    /// <summary>Calculates the force allowed by the motor-current curve and efficiency.</summary>
    public static double MaximumTractionForceKn(
        IReadOnlyList<TrainForceCurvePoint> forceCurve,
        IReadOnlyList<TrainCurrentCurvePoint> currentCurve,
        double maximumTractionForceKn,
        double speedKmh,
        double voltageV,
        double efficiency,
        double auxiliaryPowerKw)
    {
        double force = TractionEffortAtSpeedKn(
            forceCurve,
            maximumTractionForceKn,
            speedKmh);
        double maximumCurrentA = InterpolateCurrent(
            currentCurve,
            voltageV,
            point => point.TractionCurrentA);
        return LimitForceByCurrent(
            force,
            speedKmh,
            voltageV,
            maximumCurrentA,
            efficiency,
            regenerative: false,
            auxiliaryPowerKw);
    }

    /// <summary>Calculates regenerative braking force allowed by the speed and current curves.</summary>
    public static double MaximumRegenerativeBrakeForceKn(
        IReadOnlyList<TrainForceCurvePoint> forceCurve,
        IReadOnlyList<TrainCurrentCurvePoint> currentCurve,
        double speedKmh,
        double voltageV,
        double efficiency)
    {
        double force = InterpolateForce(forceCurve, speedKmh);
        double maximumCurrentA = Math.Abs(InterpolateCurrent(
            currentCurve,
            voltageV,
            point => point.RegenerativeCurrentA));
        return LimitForceByCurrent(
            force,
            speedKmh,
            voltageV,
            maximumCurrentA,
            efficiency,
            regenerative: true,
            auxiliaryPowerKw: 0);
    }

    /// <summary>
    /// Calculates the base speed where maximum traction force transitions to the power limit:
    /// Vbase = (Pmax × efficiency) / Fmax × 3.6.
    /// </summary>
    public static double BaseSpeedKmh(
        double maximumMotorPowerKw,
        double efficiency,
        double maximumTractionForceKn) =>
        maximumTractionForceKn > 0
            ? Math.Round(maximumMotorPowerKw * efficiency / maximumTractionForceKn * 3.6, 1)
            : 30.0;

    /// <summary>Calculates mechanical force power in kW as force in kN times speed in m/s.</summary>
    public static double MechanicalPowerKw(double forceKn, double speedKmh) =>
        forceKn * SpeedMetersPerSecond(speedKmh);

    /// <summary>Calculates signed traction-minus-regeneration power at the motor set.</summary>
    public static double SignedTractionPowerKw(double tractionPowerKw, double regenerativePowerKw) =>
        tractionPowerKw - regenerativePowerKw;

    /// <summary>Calculates net pantograph power, including auxiliaries.</summary>
    public static double NetPantographPowerKw(
        double tractionPowerKw,
        double auxiliaryPowerKw,
        double regenerativePowerKw) =>
        tractionPowerKw + auxiliaryPowerKw - regenerativePowerKw;

    /// <summary>Calculates DC current from power and voltage, returning zero for invalid voltage.</summary>
    public static double CurrentA(double powerKw, double voltageV) =>
        voltageV > 0 ? powerKw * 1000.0 / voltageV : 0;

    /// <summary>Converts Davis, grade and curve resistance components into total running resistance.</summary>
    public static double TotalResistanceKn(
        double davisResistanceKn,
        double gradeResistanceKn,
        double curveResistanceKn) =>
        davisResistanceKn + gradeResistanceKn + curveResistanceKn;

    /// <summary>Calculates acceleration from net force and effective train mass.</summary>
    public static double AccelerationMps2(double effortKn, double resistanceKn, double massTon) =>
        massTon > 0 ? Math.Max(0, (effortKn - resistanceKn) / massTon) : 0;

    /// <summary>Converts a speed change in km/h over elapsed seconds to acceleration in m/s².</summary>
    public static double AccelerationFromSpeedChangeMps2(double deltaSpeedKmh, double elapsedSeconds) =>
        elapsedSeconds > 0 ? deltaSpeedKmh / (elapsedSeconds * 3.6) : 0;

    /// <summary>Calculates the bounded speed change during one simulation step.</summary>
    public static double SpeedChangeKmh(double requestedChangeKmh, double rateKmhPerSecond, double dt) =>
        Math.Min(requestedChangeKmh, rateKmhPerSecond * dt);

    /// <summary>Converts acceleration in m/s² into the corresponding speed rate in km/h per second.</summary>
    public static double SpeedRateKmhPerSecond(double accelerationMps2) =>
        accelerationMps2 * 3.6;

    /// <summary>Calculates the requested braking speed change with a minimum deceleration floor.</summary>
    public static double RequestedBrakingSpeedChangeKmh(
        double speedDifferenceKmh,
        double hysteresisKmh,
        double minimumBrakingAccelerationMps2,
        double elapsedSeconds)
    {
        double absoluteDifference = Math.Abs(speedDifferenceKmh);
        return absoluteDifference <= hysteresisKmh
            ? Math.Max(
                absoluteDifference,
                SpeedRateKmhPerSecond(minimumBrakingAccelerationMps2) * elapsedSeconds)
            : absoluteDifference;
    }

    /// <summary>Calculates the speed target for an approaching station within a braking zone.</summary>
    public static double StationApproachSpeedKmh(
        double distanceToStationM,
        double maximumOperatingSpeedKmh) =>
        Math.Max(15, distanceToStationM / 400.0 * maximumOperatingSpeedKmh);

    /// <summary>Calculates the following train speed cap from the remaining safe-distance margin.</summary>
    public static double FollowingTrainSpeedLimitKmh(double gapM, double minimumSafeDistanceM) =>
        Math.Max(
            10,
            ((gapM - minimumSafeDistanceM * 0.7) /
             (minimumSafeDistanceM * 0.3)) * 35);

    /// <summary>Calculates distance advanced in metres from speed in km/h and elapsed seconds.</summary>
    public static double DistanceMeters(double speedKmh, double elapsedSeconds) =>
        SpeedMetersPerSecond(speedKmh) * elapsedSeconds;

    /// <summary>Converts distance in metres to kilometres for accumulated run statistics.</summary>
    public static double MetersToKilometers(double distanceM) => distanceM / 1000.0;

    /// <summary>Applies relaxation to an iterative voltage solution to improve convergence.</summary>
    public static double RelaxedVoltageV(
        double previousVoltageV,
        double calculatedVoltageV,
        double relaxationFactor) =>
        previousVoltageV +
        (calculatedVoltageV - previousVoltageV) * Math.Clamp(relaxationFactor, 0, 1);

    /// <summary>Calculates energy in kWh contributed by power over an elapsed time step.</summary>
    public static double EnergyKwh(double powerKw, double elapsedSeconds) =>
        powerKw * elapsedSeconds / 3600.0;

    /// <summary>Calculates a power load as a percentage of the specified rated power.</summary>
    public static double LoadPercent(double powerKw, double ratedPowerKw) =>
        ratedPowerKw > 0 ? powerKw / ratedPowerKw * 100.0 : 0;

    /// <summary>Calculates DC power in kW from current in amperes and voltage in volts.</summary>
    public static double DcPowerKw(double currentA, double voltageV) =>
        currentA * voltageV / 1000.0;

    /// <summary>Calculates transformer and cable source resistance in ohms.</summary>
    public static double SourceResistanceOhm(
        double transformerResistanceMilliOhm,
        double feederResistanceMilliOhm,
        double returnResistanceMilliOhm) =>
        Math.Max(
            0,
            transformerResistanceMilliOhm +
            feederResistanceMilliOhm +
            returnResistanceMilliOhm) / 1000.0;

    /// <summary>Converts combined catenary and rail resistance from ohm/km to ohm/m.</summary>
    public static double LineResistanceOhmPerMeter(double catenaryOhmPerKm, double railOhmPerKm)
    {
        double resistance = (catenaryOhmPerKm + railOhmPerKm) / 1_000_000.0;
        return resistance > 0 ? resistance : 0.000036;
    }

    /// <summary>Calculates the equivalent line resistance for the distance to a substation.</summary>
    public static double LineResistanceOhm(double resistanceOhmPerMeter, double distanceM) =>
        resistanceOhmPerMeter * distanceM;

    /// <summary>Calculates branch conductance as the reciprocal of its protected total resistance.</summary>
    public static double BranchConductance(double sourceResistanceOhm, double lineResistanceOhm) =>
        1.0 / Math.Max(0.000001, sourceResistanceOhm + lineResistanceOhm);

    /// <summary>Calculates a feed's share of total conductance for current distribution.</summary>
    public static double FeedWeight(double conductance, double totalConductance) =>
        totalConductance > 0 ? conductance / totalConductance : 0;

    /// <summary>Calculates substation DC output voltage after source-resistance drop or rise.</summary>
    public static double SubstationVoltageV(
        double nominalVoltageV,
        double outputCurrentA,
        double sourceResistanceOhm,
        double maximumVoltageV) =>
        Math.Clamp(
            nominalVoltageV - outputCurrentA * sourceResistanceOhm,
            0,
            maximumVoltageV);

    /// <summary>Calculates train pantograph voltage from weighted substations and feeder voltage drops.</summary>
    public static double TrainVoltageV(
        IReadOnlyList<(double Weight, double SubstationVoltageV, double LineResistanceOhm)> feeds,
        double trainCurrentA,
        double maximumVoltageV)
    {
        double voltage = feeds.Sum(feed =>
            feed.Weight *
            (feed.SubstationVoltageV -
             trainCurrentA * feed.Weight * feed.LineResistanceOhm));
        return Math.Clamp(voltage, 0, maximumVoltageV);
    }

    /// <summary>Calculates train-set force allowed by its current and electrical power limits.</summary>
    private static double LimitForceByCurrent(
        double forceKn,
        double speedKmh,
        double voltageV,
        double maximumCurrentA,
        double efficiency,
        bool regenerative,
        double auxiliaryPowerKw)
    {
        double speedMs = SpeedMetersPerSecond(speedKmh);
        if (voltageV <= 0 || maximumCurrentA <= 0 || efficiency <= 0)
        {
            return 0;
        }

        if (speedMs <= 0.1)
        {
            return Math.Max(0, forceKn);
        }

        double electricalPowerKw = Math.Max(
            0,
            maximumCurrentA * voltageV / 1000.0 -
            (regenerative ? 0 : Math.Max(0, auxiliaryPowerKw)));
        double currentLimitedForceKn = regenerative
            ? electricalPowerKw / (efficiency * speedMs)
            : electricalPowerKw * efficiency / speedMs;
        return Math.Max(0, Math.Min(forceKn, currentLimitedForceKn));
    }

    /// <summary>Calculates a clamped linear interpolation ratio between two curve points.</summary>
    private static double InterpolationRatio(double value, double lower, double upper)
    {
        double span = upper - lower;
        return span > 0 ? (value - lower) / span : 0;
    }
}
