using System;
using System.Collections.Generic;
using System.Linq;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services
{
    /// <summary>
    /// Central simulation engine that handles train kinematics, power calculations,
    /// transformer loading and catenary voltage updates. All logic previously lived
    /// in MainViewModel (AdvanceSimulationStep & UpdateSimulationCalculations) but
    /// is now consolidated here.
    /// </summary>
    public class RailwaySimulationEngine
    {
        private const double SpeedControlHysteresisKmh = 0.5;
        private const double MinimumBrakingAccelerationMps2 = 0.05;
        private const double MechanicalBrakeTransitionSpeedKmh = 5.0;

        private sealed record SubstationFeed(
            TrafoMerkezi Substation,
            double Weight,
            double SourceResistanceOhm,
            double LineResistanceOhm);

        private sealed record TrainPowerDemand(
            double TractionEffortKn,
            double RegenerativeEffortKn,
            double AuxiliaryPowerKw);

        private sealed record TrainElectricalState(
            TrenKonumu Train,
            IReadOnlyList<SubstationFeed> Feeds,
            TrainPowerDemand Demand);

        private sealed record TrainPowerFlow(
            double TractionEffortKn,
            double RegenerativeEffortKn,
            double TractionPowerKw,
            double RegenerativePowerKw,
            double NetPowerKw);

        private sealed record ElectricalSolution(
            Dictionary<TrenKonumu, TrainPowerFlow> PowerFlows,
            Dictionary<TrenKonumu, double> TrainVoltages,
            Dictionary<TrafoMerkezi, double> SubstationCurrents);

        private readonly SimulationData _data;
        private readonly IReadOnlySet<string> _outOfServiceSubstations;
        private readonly Random _rand = new();
        private readonly Dictionary<TrafoMerkezi, double> _over150PercentLoadSeconds = new();
        private readonly Dictionary<TrafoMerkezi, double> _over300PercentLoadSeconds = new();
        private readonly Dictionary<TrafoMerkezi, double> _dcBusVoltagesV = new();

        private double _simTotalElapsed = 0;
        private const double MaximumElectricalStepSeconds = 0.05;

        public void ResetTime()
        {
            _simTotalElapsed = 0;
            _over150PercentLoadSeconds.Clear();
            _over300PercentLoadSeconds.Clear();
            _dcBusVoltagesV.Clear();
        }

        public RailwaySimulationEngine(
            SimulationData data,
            IReadOnlySet<string>? outOfServiceSubstations = null)
        {
            _data = data;
            _outOfServiceSubstations = outOfServiceSubstations ??
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Advance the simulation by the specified time step (seconds).
        /// </summary>
        public void AdvanceOneStep(double dt)
        {
            double elapsed = 0;
            while (elapsed < dt)
            {
                double step = Math.Min(MaximumElectricalStepSeconds, dt - elapsed);
                AdvanceOneSubstep(step);
                elapsed += step;
            }
        }

        private void AdvanceOneSubstep(double dt)
        {
            _simTotalElapsed += dt;
            double maxLine = _data.HatUzunlugu > 0 ? _data.HatUzunlugu : 15394;
            double minSafeDistance = _data.Isletme?.MinimumTrenMesafesiMetre > 0 ? _data.Isletme.MinimumTrenMesafesiMetre : 400.0;
            double trainWeightTon = _data.Tren.Aw3DoluAgirlik > 0 ? _data.Tren.Aw3DoluAgirlik : 236.0;
            double rotatingMassTon = SimulationFormulas.RotatingMassTon(
                _data.Tren.Aw0BosAgirlik,
                _data.Tren.DonerKutle);
            double effectiveTrainMassTon = SimulationFormulas.EffectiveTrainMassTon(
                trainWeightTon,
                rotatingMassTon);
            double auxiliaryPowerKw = SimulationFormulas.AuxiliaryPowerKw(
                _data.Tren.YardimciGuc > 0 ? _data.Tren.YardimciGuc : 300.0,
                _data.Tren.YardimciGucKullanimOrani);

            // 1. Kinematic update for each train
            foreach (var tren in _data.Trenler)
            {
                // Dispatch check for multi-train headway (e.g. 0s, 90s, 180s)
                if (!tren.IsDispatched)
                {
                    if (_simTotalElapsed >= tren.DispatchTimeSeconds)
                    {
                        tren.IsDispatched = true;
                        tren.Durum = "SEFERE BAŞLADI";
                        tren.YolcuSayisi = 120;
                    }
                    else
                    {
                        tren.AnlikHiz = 0;
                        tren.Ivme = 0;
                        tren.CekilenGucKw = 0;
                        tren.CekilenAkimA = 0;
                        tren.AnlikPantografGucuKw = 0;
                        tren.AnlikPantografAkimiA = 0;
                        tren.AnlikCekisKuvvetiKn = 0;
                        tren.AnlikRejeneratifFrenKuvvetiKn = 0;
                        tren.Durum = $"SEFER BEKLİYOR (~{Math.Max(0, tren.DispatchTimeSeconds - _simTotalElapsed):0} sn)";
                        tren.IsletimDurumu = "SEFER BEKLİYOR";
                        tren.IsOutOfSpec = false;
                        tren.NeedsLimitReview = false;
                        tren.LimitStatus = "SEFER BEKLİYOR";
                        continue;
                    }
                }

                // Station waiting logic
                if (tren.IstasyonBeklemeSayaci > 0)
                {
                    tren.IstasyonBeklemeSayaci -= dt;
                    tren.AnlikHiz = 0;
                    tren.Ivme = 0;
                    tren.Durum = "İSTASYONDA (YOLCU ALIYOR)";
                    tren.IsletimDurumu = "İSTASYONDA";
                    continue;
                }

                // Find next station based on direction
                Istasyon? nextStation = null;
                if (tren.Yon == "ileri")
                {
                    nextStation = _data.Istasyonlar
                        .Where(s => s.H1OrtaNokta > tren.Konum)
                        .OrderBy(s => s.H1OrtaNokta)
                        .FirstOrDefault();
                }
                else
                {
                    nextStation = _data.Istasyonlar
                        .Where(s => s.H1OrtaNokta < tren.Konum)
                        .OrderByDescending(s => s.H1OrtaNokta)
                        .FirstOrDefault();
                }

                double distToNext = 99999;
                if (nextStation != null)
                {
                    distToNext = Math.Abs(nextStation.H1OrtaNokta - tren.Konum);
                    tren.SonrakiIstasyon = nextStation.Ad;
                    tren.SonrakiIstasyonMesafe = Math.Round(distToNext, 0);

                    // If within 25 m and low speed, stop for passengers
                    if (distToNext <= 1 && tren.AnlikHiz <= 1)
                    {
                        tren.Konum = nextStation.H1OrtaNokta;
                        tren.AnlikHiz = 0;
                        tren.Ivme = 0;
                        tren.IstasyonBeklemeSayaci = 6.0;
                        tren.Durum = "İSTASYONDA (YOLCU ALIYOR)";
                        tren.IsletimDurumu = "İSTASYONDA";
                        int degisim = _rand.Next(-20, 30);
                        tren.YolcuSayisi = Math.Max(20, Math.Min(400, tren.YolcuSayisi + degisim));
                        continue;
                    }
                }
                else
                {
                    tren.SonrakiIstasyon = tren.Yon == "ileri" ? "Hat Sonu (Depo)" : "Hat Başı (Sahil)";
                    tren.SonrakiIstasyonMesafe = tren.Yon == "ileri" ? Math.Max(0, maxLine - tren.Konum) : tren.Konum;
                }

                // Determine target speed based on line speed limits and station approach
                double targetSpeed = _data.Tren.MaksIsletmeHizi;
                if (distToNext < 400)
                {
                    targetSpeed = SimulationFormulas.StationApproachSpeedKmh(
                        distToNext,
                        _data.Tren.MaksIsletmeHizi);
                }
                var applicableHizLimiti = _data.HizLimitleri
                    .FirstOrDefault(h => h.HatTipi == tren.HatTipi && tren.Konum >= h.Baslangic && tren.Konum <= h.Bitis);
                if (applicableHizLimiti != null)
                {
                    targetSpeed = Math.Min(targetSpeed, applicableHizLimiti.Limit);
                }

                // Automatic Train Protection (ATP): Safety Distance to leading train on the same track
                TrenKonumu? leadingTrain = null;
                if (tren.Yon == "ileri")
                {
                    leadingTrain = _data.Trenler
                        .Where(t => t != tren && t.IsDispatched && t.HatTipi == tren.HatTipi && t.Yon == tren.Yon && t.Konum > tren.Konum)
                        .OrderBy(t => t.Konum)
                        .FirstOrDefault();
                }
                else
                {
                    leadingTrain = _data.Trenler
                        .Where(t => t != tren && t.IsDispatched && t.HatTipi == tren.HatTipi && t.Yon == tren.Yon && t.Konum < tren.Konum)
                        .OrderByDescending(t => t.Konum)
                        .FirstOrDefault();
                }

                if (leadingTrain != null)
                {
                    double gap = Math.Abs(leadingTrain.Konum - tren.Konum);
                    if (gap < minSafeDistance * 0.7)
                    {
                        targetSpeed = 0; // Emergency safe stop
                    }
                    else if (gap < minSafeDistance)
                    {
                        targetSpeed = Math.Min(
                            targetSpeed,
                            SimulationFormulas.FollowingTrainSpeedLimitKmh(gap, minSafeDistance));
                    }
                }

                tren.HedefHiz = targetSpeed;

                // Acceleration / braking dynamics
                double speedDiff = targetSpeed - tren.AnlikHiz;
                double maximumAcceleration = Math.Max(0, _data.Tren.MaksIvmelenme);
                double maximumDeceleration = Math.Max(0, _data.Tren.MaksFrenlemeIvmesi);
                double jerkLimit = _data.Tren.JerkLimiti > 0 ? _data.Tren.JerkLimiti : 1.0;
                if (speedDiff > SpeedControlHysteresisKmh)
                {
                    double availableEffortKn = _data.Tren.MaximumTractionForceKn(
                        tren.AnlikHiz,
                        tren.KatenerGerilimiV,
                        auxiliaryPowerKw);
                    double resistanceKn = CalculateResistanceKn(tren, trainWeightTon, tren.AnlikHiz);
                    double forceLimitedAcceleration = SimulationFormulas.AccelerationMps2(
                        availableEffortKn,
                        resistanceKn,
                        effectiveTrainMassTon);
                    double requestedAcceleration = Math.Min(
                        maximumAcceleration,
                        Math.Min(forceLimitedAcceleration, speedDiff / 3.6));
                    double appliedAcceleration = SimulationFormulas.JerkLimitedAccelerationMps2(
                        tren.Ivme,
                        requestedAcceleration,
                        jerkLimit,
                        dt);
                    ApplyAcceleration(tren, appliedAcceleration, dt);
                    tren.Durum = "HIZLANIYOR (CER GÜCÜ ÇEKİYOR)";
                }
                else if (speedDiff < -SpeedControlHysteresisKmh)
                {
                    double requestedDeceleration = Math.Min(
                        maximumDeceleration,
                        Math.Max(
                            MinimumBrakingAccelerationMps2,
                            Math.Abs(speedDiff) / 3.6));
                    double appliedAcceleration = SimulationFormulas.JerkLimitedAccelerationMps2(
                        tren.Ivme,
                        -requestedDeceleration,
                        jerkLimit,
                        dt);
                    ApplyAcceleration(tren, appliedAcceleration, dt);
                    tren.Durum = "FRENLİYOR (REJENERATİF FREN)";
                }
                else
                {
                    double resistanceKn = CalculateResistanceKn(tren, trainWeightTon, tren.AnlikHiz);
                    double coastingAccelerationMps2 = -resistanceKn / effectiveTrainMassTon;
                    double previousSpeed = tren.AnlikHiz;
                    tren.AnlikHiz = Math.Max(
                        0,
                        tren.AnlikHiz +
                        SimulationFormulas.SpeedRateKmhPerSecond(coastingAccelerationMps2) * dt);
                    tren.Ivme = Math.Round(
                        SimulationFormulas.AccelerationFromSpeedChangeMps2(
                            tren.AnlikHiz - previousSpeed,
                            dt),
                        2);
                    tren.Durum = "BOŞTA SEYİR (COASTING)";
                }

                // Position update
                double deltaDistance = SimulationFormulas.DistanceMeters(tren.AnlikHiz, dt);
                tren.KatEdilenMesafeKm += SimulationFormulas.MetersToKilometers(deltaDistance);
                if (tren.Yon == "ileri")
                {
                    tren.Konum += deltaDistance;
                    if (tren.Konum >= maxLine)
                    {
                        tren.Konum = maxLine;
                        tren.Yon = "geri";
                        tren.HatTipi = "H2";
                        tren.AnlikHiz = 0;
                        tren.IstasyonBeklemeSayaci = 8.0;
                    }
                }
                else
                {
                    tren.Konum -= deltaDistance;
                    if (tren.Konum <= 0)
                    {
                        tren.Konum = 0;
                        tren.Yon = "ileri";
                        tren.HatTipi = "H1";
                        tren.AnlikHiz = 0;
                        tren.IstasyonBeklemeSayaci = 8.0;
                    }
                }
            }

            // 2. Solve train power and the shared DC supply voltages together.
            double nominalVoltage = _data.CerKatener.YuksuzDcBaraGerilimi > 0
                ? _data.CerKatener.YuksuzDcBaraGerilimi
                : 1620.0;
            double maximumOperatingVoltage = _data.Tren.MaksGerilim > 0
                ? _data.Tren.MaksGerilim
                : 1800.0;
            double minimumOperatingVoltage = _data.Tren.MinIsletmeGerilimi > 0
                ? _data.Tren.MinIsletmeGerilimi
                : (_data.Tren.MinGerilim > 0 ? _data.Tren.MinGerilim : 1000.0);
            double maximumElectricalVoltage = _data.Tren.RegenerativeVoltageCutoffV(maximumOperatingVoltage);
            double efficiency = _data.Tren.TrenVerimi > 0
                ? SimulationFormulas.PercentToRatio(_data.Tren.TrenVerimi)
                : 0.85;
            var electricalStates = _data.Trenler
                .Where(train => train.IsDispatched)
                .Select(train => new TrainElectricalState(
                    train,
                    GetSubstationFeeds(train),
                    CreatePowerDemand(train, trainWeightTon, effectiveTrainMassTon, auxiliaryPowerKw)))
                .ToArray();
            foreach (var substation in _data.TrafoMerkezleri)
            {
                if (!_outOfServiceSubstations.Contains(substation.Ad) &&
                    !_dcBusVoltagesV.ContainsKey(substation))
                {
                    _dcBusVoltagesV[substation] = nominalVoltage;
                }
            }

            var busVoltages = _data.TrafoMerkezleri
                .Where(substation => !_outOfServiceSubstations.Contains(substation.Ad))
                .ToDictionary(
                    substation => substation,
                    substation => _dcBusVoltagesV[substation]);
            ElectricalSolution solution = SolveElectricalNetwork(
                electricalStates,
                busVoltages,
                maximumElectricalVoltage,
                efficiency);

            double elapsedElectricalTime = 0;
            while (elapsedElectricalTime < dt)
            {
                double electricalStep = Math.Min(
                    MaximumElectricalStepSeconds,
                    dt - elapsedElectricalTime);
                foreach (var substation in _data.TrafoMerkezleri)
                {
                    if (_outOfServiceSubstations.Contains(substation.Ad))
                    {
                        continue;
                    }

                    double ratedRectifierPowerKw = _data.CerKatener.DogrultucuGucu > 0
                        ? _data.CerKatener.DogrultucuGucu
                        : 3000.0;
                    double maximumRectifierCurrentA = ratedRectifierPowerKw * 1000.0 /
                                                      Math.Max(1, nominalVoltage);
                    double transformerResistanceMilliOhm = substation.Direnc > 0
                        ? substation.Direnc
                        : _data.CerKatener.TrafoIcDirenci;
                    double sourceResistanceOhm = Math.Max(
                        0.000001,
                        SimulationFormulas.SourceResistanceOhm(
                            transformerResistanceMilliOhm,
                            substation.FiderKabloDirenci,
                            substation.GeriDonusKabloDirenci));
                    double busVoltage = SimulationFormulas.AdvanceDiodeRectifierBusVoltage(
                        busVoltages[substation],
                        solution.SubstationCurrents.GetValueOrDefault(substation),
                        electricalStep,
                        _data.CerKatener.DcBaraEsdegerKapasitansiFarad,
                        nominalVoltage,
                        sourceResistanceOhm,
                        maximumRectifierCurrentA,
                        maximumElectricalVoltage);
                    busVoltages[substation] = busVoltage;
                    _dcBusVoltagesV[substation] = busVoltage;
                }

                foreach (var state in electricalStates)
                {
                    TrainPowerFlow flow = solution.PowerFlows[state.Train];
                    state.Train.ToplamTuketilenEnerjiKwh += SimulationFormulas.EnergyKwh(
                        Math.Max(0, flow.NetPowerKw),
                        electricalStep);
                    state.Train.ToplamRejeneratifEnerjiKwh += SimulationFormulas.EnergyKwh(
                        flow.RegenerativePowerKw,
                        electricalStep);
                }

                elapsedElectricalTime += electricalStep;
                solution = SolveElectricalNetwork(
                    electricalStates,
                    busVoltages,
                    maximumElectricalVoltage,
                    efficiency);
            }

            foreach (var state in electricalStates)
            {
                var train = state.Train;
                var flow = solution.PowerFlows[train];
                double voltage = solution.TrainVoltages[train];
                double pantographCurrent = SimulationFormulas.CurrentA(flow.NetPowerKw, voltage);
                double signedTractionPower = SimulationFormulas.SignedTractionPowerKw(
                    flow.TractionPowerKw,
                    flow.RegenerativePowerKw);
                double signedTractionCurrent = SimulationFormulas.CurrentA(signedTractionPower, voltage);

                train.KatenerGerilimiV = Math.Round(voltage, 1);
                train.AnlikCekisKuvvetiKn = flow.TractionEffortKn;
                train.AnlikRejeneratifFrenKuvvetiKn = flow.RegenerativeEffortKn;
                train.CekilenGucKw = Math.Round(signedTractionPower, 1);
                train.CekilenAkimA = Math.Round(signedTractionCurrent, 1);
                train.AnlikPantografGucuKw = Math.Round(flow.NetPowerKw, 1);
                train.AnlikPantografAkimiA = Math.Round(pantographCurrent, 1);
                train.IsletimDurumu = DetermineOperatingMode(state, flow);
                UpdateTrainLimitStatus(train, minimumOperatingVoltage, maximumOperatingVoltage);
            }

            // 3. Publish transformer output using the solved current and voltage.
            foreach (var substation in _data.TrafoMerkezleri)
            {
                if (_outOfServiceSubstations.Contains(substation.Ad))
                {
                    substation.AnlikGucKw = 0;
                    substation.BeslenenTrenSayisi = 0;
                    substation.AnlikAkimA = 0;
                    substation.YuklenmeYuzdesi = 0;
                    substation.AnlikGerilimV = 0;
                    substation.AnlikDogrultucuGucKw = 0;
                    substation.AnlikDogrultucuAkimiA = 0;
                    substation.AnlikKopruDogrultucuGucuKw = 0;
                    substation.AnlikKopruDogrultucuAkimiA = 0;
                    substation.AnlikDcDalgalanmaFrekansiHz = 0;
                    substation.AnlikRejeneratifDcGucKw = 0;
                    substation.AnlikAcGucKw = 0;
                    substation.AnlikAcGorunurGucKva = 0;
                    substation.AnlikAcAkimiA = 0;
                    substation.TrafoYuklenmeYuzdesi = 0;
                    substation.AnlikDogrultucuKaybiKw = 0;
                    substation.AnlikTrafoKaybiKw = 0;
                    substation.Durum = "DEVRE DIŞI";
                    substation.IsOutOfSpec = false;
                    substation.NeedsLimitReview = false;
                    substation.LimitStatus = "DEVRE DIŞI";
                    _over150PercentLoadSeconds[substation] = 0;
                    _over300PercentLoadSeconds[substation] = 0;
                    continue;
                }

                double current = solution.SubstationCurrents.GetValueOrDefault(substation);
                double voltage = busVoltages.GetValueOrDefault(substation, nominalVoltage);
                double powerKw = SimulationFormulas.DcPowerKw(current, voltage);
                double transformerResistanceMilliOhm = substation.Direnc > 0
                    ? substation.Direnc
                    : _data.CerKatener.TrafoIcDirenci;
                double sourceResistanceOhm = Math.Max(
                    0.000001,
                    SimulationFormulas.SourceResistanceOhm(
                        transformerResistanceMilliOhm,
                        substation.FiderKabloDirenci,
                        substation.GeriDonusKabloDirenci));
                double ratedRectifierPowerKw = _data.CerKatener.DogrultucuGucu > 0
                    ? _data.CerKatener.DogrultucuGucu
                    : 3000.0;
                double maximumRectifierCurrentA = ratedRectifierPowerKw * 1000.0 /
                                                  Math.Max(1, nominalVoltage);
                double rectifierOutputCurrentA = SimulationFormulas.DiodeRectifierOutputCurrentA(
                    nominalVoltage,
                    voltage,
                    sourceResistanceOhm,
                    maximumRectifierCurrentA);
                double rectifierOutputPowerKw = SimulationFormulas.DcPowerKw(
                    rectifierOutputCurrentA,
                    voltage);
                (double bridgePowerKw, double bridgeCurrentA) =
                    SimulationFormulas.ParallelBridgeOutput(
                        rectifierOutputPowerKw,
                        rectifierOutputCurrentA,
                        bridgeCount: 2);
                double regenerativeDcPowerKw = Math.Max(0, -powerKw);
                int fedCount = electricalStates.Count(state =>
                    Math.Abs(solution.PowerFlows[state.Train].NetPowerKw) > 1 &&
                    state.Feeds.Any(feed =>
                        feed.Substation == substation && feed.Weight > 0.15));
                double ratedTransformerPowerKva = _data.CerKatener.TrafoGucu > 0
                    ? _data.CerKatener.TrafoGucu
                    : 3300.0;
                double rectifierEfficiency = SimulationFormulas.PercentToRatio(
                    _data.CerKatener.DogrultucuVerimiYuzde);
                double transformerEfficiency = SimulationFormulas.PercentToRatio(
                    _data.CerKatener.TrafoVerimiYuzde);
                double rectifierInputPowerKw = SimulationFormulas.ConverterInputPowerKw(
                    rectifierOutputPowerKw,
                    rectifierEfficiency);
                double acInputPowerKw = SimulationFormulas.ConverterInputPowerKw(
                    rectifierInputPowerKw,
                    transformerEfficiency);
                double acApparentPowerKva = SimulationFormulas.ThreePhaseApparentPowerKva(
                    acInputPowerKw,
                    _data.CerKatener.DogrultucuGucFaktoru);
                double acLineVoltageV = _data.CerKatener.TrafoPrimerGerilimiKv * 1000.0;
                double acInputCurrentA = SimulationFormulas.ThreePhaseLineCurrentA(
                    acApparentPowerKva,
                    acLineVoltageV);
                double rectifierLoadPercent = SimulationFormulas.LoadPercent(
                    rectifierOutputPowerKw,
                    ratedRectifierPowerKw);
                double transformerLoadPercent = SimulationFormulas.RatedLoadPercent(
                    acApparentPowerKva,
                    ratedTransformerPowerKva);
                double maximumEquipmentLoadPercent = Math.Max(rectifierLoadPercent, transformerLoadPercent);

                substation.AnlikGucKw = Math.Round(powerKw, 1);
                substation.BeslenenTrenSayisi = fedCount;
                substation.AnlikAkimA = Math.Round(current, 1);
                substation.AnlikGerilimV = Math.Round(voltage, 1);
                substation.AnlikDogrultucuGucKw = Math.Round(rectifierOutputPowerKw, 1);
                substation.AnlikDogrultucuAkimiA = Math.Round(rectifierOutputCurrentA, 1);
                substation.AnlikKopruDogrultucuGucuKw = Math.Round(bridgePowerKw, 1);
                substation.AnlikKopruDogrultucuAkimiA = Math.Round(bridgeCurrentA, 1);
                substation.AnlikDcDalgalanmaFrekansiHz =
                    SimulationFormulas.TwelvePulseRippleFrequencyHz(
                        _data.CerKatener.AcFrekansiHz);
                substation.AnlikRejeneratifDcGucKw = Math.Round(regenerativeDcPowerKw, 1);
                substation.AnlikAcGucKw = Math.Round(acInputPowerKw, 1);
                substation.AnlikAcGorunurGucKva = Math.Round(acApparentPowerKva, 1);
                substation.AnlikAcAkimiA = Math.Round(acInputCurrentA, 1);
                substation.TrafoYuklenmeYuzdesi = Math.Round(transformerLoadPercent, 1);
                substation.AnlikDogrultucuKaybiKw = Math.Round(
                    Math.Max(0, rectifierInputPowerKw - rectifierOutputPowerKw),
                    1);
                substation.AnlikTrafoKaybiKw = Math.Round(
                    Math.Max(0, acInputPowerKw - rectifierInputPowerKw),
                    1);
                substation.YuklenmeYuzdesi = Math.Round(rectifierLoadPercent, 1);
                UpdateSubstationLimitStatus(
                    substation,
                    voltage,
                    maximumEquipmentLoadPercent,
                    dt,
                    minimumOperatingVoltage,
                    maximumOperatingVoltage);
                substation.Durum = powerKw switch
                {
                    < -5 => "REJENERATİF",
                    _ when maximumEquipmentLoadPercent > 300 => "AŞIRI YÜK",
                    _ when maximumEquipmentLoadPercent > 150 => "YÜKSEK YÜK (DUTY SINIRI)",
                    _ when maximumEquipmentLoadPercent > 100 => "ANMA GÜCÜ ÜSTÜ",
                    _ when maximumEquipmentLoadPercent > 5 => "NORMAL",
                    _ => "BOŞTA"
                };
            }
        }

        private void UpdateTrainLimitStatus(
            TrenKonumu train,
            double minimumOperatingVoltage,
            double maximumOperatingVoltage)
        {
            if (!train.IsDispatched)
            {
                train.IsOutOfSpec = false;
                train.NeedsLimitReview = false;
                train.LimitStatus = "SEFER BEKLİYOR";
                return;
            }

            var violations = new List<string>();
            double minimumAcceleration = _data.Tren.MaksIvmelenme > 0 ? _data.Tren.MaksIvmelenme : 1.1;
            double maximumDeceleration = _data.Tren.MaksFrenlemeIvmesi > 0 ? _data.Tren.MaksFrenlemeIvmesi : 1.1;
            double maximumOperatingSpeed = _data.Tren.MaksIsletmeHizi > 0 ? _data.Tren.MaksIsletmeHizi : 80.0;

            if (train.KatenerGerilimiV < minimumOperatingVoltage)
            {
                violations.Add("İŞLETME GERİLİMİ DÜŞÜK");
            }
            if (train.KatenerGerilimiV > maximumOperatingVoltage)
            {
                violations.Add("AZAMİ GERİLİM AŞILDI");
            }
            if (train.AnlikHiz > maximumOperatingSpeed)
            {
                violations.Add("İŞLETME HIZI AŞILDI");
            }
            if (train.Ivme > minimumAcceleration || train.Ivme < -maximumDeceleration)
            {
                violations.Add("İVME SINIRI AŞILDI");
            }

            train.IsOutOfSpec = violations.Count > 0;
            train.NeedsLimitReview = false;
            train.LimitStatus = train.IsOutOfSpec
                ? string.Join("; ", violations)
                : "UYGUN";
        }

        private void UpdateSubstationLimitStatus(
            TrafoMerkezi substation,
            double voltage,
            double loadPercent,
            double dt,
            double minimumOperatingVoltage,
            double maximumOperatingVoltage)
        {
            double elapsed = Math.Max(0, dt);
            double over150Seconds = loadPercent > 150
                ? _over150PercentLoadSeconds.GetValueOrDefault(substation) + elapsed
                : 0;
            double over300Seconds = loadPercent > 300
                ? _over300PercentLoadSeconds.GetValueOrDefault(substation) + elapsed
                : 0;
            _over150PercentLoadSeconds[substation] = over150Seconds;
            _over300PercentLoadSeconds[substation] = over300Seconds;

            var violations = new List<string>();
            if (voltage < minimumOperatingVoltage)
            {
                violations.Add("BARA GERİLİMİ DÜŞÜK");
            }
            if (voltage > maximumOperatingVoltage)
            {
                violations.Add("AZAMİ BARA GERİLİMİ AŞILDI");
            }
            if (over300Seconds > 60)
            {
                violations.Add("TRAFO/DOĞRULTUCU %300 YÜK SINIRI 1 DAKİKADAN UZUN AŞILDI");
            }
            if (over150Seconds > 7200)
            {
                violations.Add("TRAFO/DOĞRULTUCU %150 YÜK SINIRI 2 SAATTEN UZUN AŞILDI");
            }

            substation.IsOutOfSpec = violations.Count > 0;
            substation.NeedsLimitReview = !substation.IsOutOfSpec &&
                (loadPercent > 100 || over150Seconds > 0 || over300Seconds > 0);

            if (substation.IsOutOfSpec)
            {
                substation.LimitStatus = string.Join("; ", violations);
            }
            else if (over300Seconds > 0)
            {
                substation.LimitStatus = "DUTY SINIRI: %300 YÜKTE 1 DAKİKA";
            }
            else if (over150Seconds > 0)
            {
                substation.LimitStatus = "DUTY SINIRI: %150 YÜKTE 2 SAAT";
            }
            else if (loadPercent > 100)
            {
                substation.LimitStatus = "ANMA GÜCÜ ÜSTÜ; RMS/DUTY KONTROLÜ";
            }
            else
            {
                substation.LimitStatus = "UYGUN";
            }
        }

        private double CalculateResistanceKn(TrenKonumu train, double trainWeightTon, double speedKmh)
        {
            // double davisA = _data.Tren.DavisA > 0 ? _data.Tren.DavisA : 2.5;
            // double davisB = _data.Tren.DavisB > 0 ? _data.Tren.DavisB : 0.03;
            // double davisC = _data.Tren.DavisC > 0 ? _data.Tren.DavisC : 0.004;
            double davisA = _data.Tren.DavisA > 0 ? _data.Tren.DavisA : 0.0;
            double davisB = _data.Tren.DavisB > 0 ? _data.Tren.DavisB : 0.0;
            double davisC = _data.Tren.DavisC > 0 ? _data.Tren.DavisC : 0.0;
            double davisKn = SimulationFormulas.DavisResistanceKn(
                trainWeightTon,
                speedKmh,
                davisA,
                davisB,
                davisC);

            var localGradient = _data.HatEgimleri.FirstOrDefault(gradient =>
                gradient.HatTipi == train.HatTipi &&
                train.Konum >= gradient.Baslangic &&
                train.Konum <= gradient.Bitis);
            double gradientKn = localGradient is null
                ? 0
                : SimulationFormulas.GradeForceKn(
                    trainWeightTon,
                    localGradient.EgimYuzdesi,
                    train.Yon);

            var localCurve = _data.HatKurplari.FirstOrDefault(curve =>
                curve.HatTipi == train.HatTipi &&
                train.Konum >= curve.Baslangic &&
                train.Konum <= curve.Bitis);
            double curveKn = localCurve is not null && localCurve.Yaricap > 55
                ? SimulationFormulas.CurveResistanceKn(trainWeightTon, localCurve.RocklDirenci)
                : 0;

            return SimulationFormulas.TotalResistanceKn(davisKn, gradientKn, curveKn);
        }

        private static void ApplyAcceleration(TrenKonumu train, double accelerationMps2, double elapsedSeconds)
        {
            double previousSpeed = train.AnlikHiz;
            train.AnlikHiz = Math.Max(
                0,
                previousSpeed +
                SimulationFormulas.SpeedRateKmhPerSecond(accelerationMps2) * elapsedSeconds);
            train.Ivme = SimulationFormulas.AccelerationFromSpeedChangeMps2(
                train.AnlikHiz - previousSpeed,
                elapsedSeconds);
        }

        private TrainPowerDemand CreatePowerDemand(
            TrenKonumu train,
            double trainWeightTon,
            double effectiveTrainMassTon,
            double auxiliaryPowerKw)
        {
            double resistanceKn = CalculateResistanceKn(train, trainWeightTon, train.AnlikHiz);

            if (train.Durum.StartsWith("BOŞTA SEYİR", StringComparison.Ordinal))
            {
                return new TrainPowerDemand(0, 0, auxiliaryPowerKw);
            }

            if (train.Ivme < -0.05)
            {
                return new TrainPowerDemand(
                    0,
                    train.AnlikHiz < MechanicalBrakeTransitionSpeedKmh
                        ? 0
                        : SimulationFormulas.BrakingEffortKn(effectiveTrainMassTon, train.Ivme),
                    auxiliaryPowerKw);
            }

            if (train.Ivme > 0.05)
            {
                double accelerationEffortKn = SimulationFormulas.InertialForceKn(
                    effectiveTrainMassTon,
                    Math.Max(0, train.Ivme));
                return new TrainPowerDemand(
                    SimulationFormulas.RequiredTractionEffortKn(accelerationEffortKn, resistanceKn),
                    0,
                    auxiliaryPowerKw);
            }

            return new TrainPowerDemand(0, 0, auxiliaryPowerKw);
        }

        private static string DetermineOperatingMode(
            TrainElectricalState state,
            TrainPowerFlow flow)
        {
            TrenKonumu train = state.Train;
            if (train.Durum.StartsWith("İSTASYONDA", StringComparison.Ordinal))
            {
                return "İSTASYONDA";
            }

            if (train.Durum.StartsWith("FRENLİYOR", StringComparison.Ordinal))
            {
                if (train.AnlikHiz < MechanicalBrakeTransitionSpeedKmh)
                {
                    return "MEKANİK FREN";
                }

                if (flow.RegenerativePowerKw > 0.1)
                {
                    return flow.NetPowerKw < -0.1
                        ? "REJENERATİF FREN (KATENERE VERİYOR)"
                        : flow.NetPowerKw > 0.1
                            ? "REJENERATİF FREN + GÜÇ ÇEKİŞİ"
                            : "REJENERATİF FREN";
                }

                return "MOTOR FRENİ";
            }

            if (state.Feeds.Count == 0)
            {
                return "BESLEME YOK";
            }

            if (train.Durum.StartsWith("BOŞTA SEYİR", StringComparison.Ordinal))
            {
                return flow.NetPowerKw > 0.1
                    ? "COASTING + AUX GÜÇ"
                    : "COASTING";
            }

            if (flow.TractionEffortKn > 0.1)
            {
                return "KATENERDEN GÜÇ ÇEKİYOR";
            }

            return flow.NetPowerKw > 0.1
                ? "AUX GÜÇ İÇİN KATENERDEN ÇEKİYOR"
                : "COASTING";
        }

        private TrainPowerFlow CalculatePowerFlow(
            TrainElectricalState state,
            double voltageV,
            double efficiency)
        {
            if (state.Feeds.Count == 0 || voltageV <= 0)
            {
                return new TrainPowerFlow(0, 0, 0, 0, 0);
            }

            double speedMs = SimulationFormulas.SpeedMetersPerSecond(state.Train.AnlikHiz);
            double tractionForceKn = SimulationFormulas.LimitedEffortKn(
                state.Demand.TractionEffortKn,
                _data.Tren.MaximumTractionForceKn(
                    state.Train.AnlikHiz,
                    voltageV,
                    state.Demand.AuxiliaryPowerKw));
            double regenerativeForceKn = SimulationFormulas.LimitedEffortKn(
                state.Demand.RegenerativeEffortKn,
                _data.Tren.MaximumRegenerativeBrakeForceKn(state.Train.AnlikHiz, voltageV));

            double tractionPowerKw = speedMs > 0
                ? SimulationFormulas.TractionElectricalPowerKw(
                    tractionForceKn,
                    state.Train.AnlikHiz,
                    efficiency)
                : 0;
            double regenerativePowerKw = SimulationFormulas.RegenerativeElectricalPowerKw(
                regenerativeForceKn,
                state.Train.AnlikHiz,
                efficiency);
            double netPowerKw = SimulationFormulas.NetPantographPowerKw(
                tractionPowerKw,
                state.Demand.AuxiliaryPowerKw,
                regenerativePowerKw);
            return new TrainPowerFlow(
                tractionForceKn,
                regenerativeForceKn,
                tractionPowerKw,
                regenerativePowerKw,
                netPowerKw);
        }

        private IReadOnlyList<SubstationFeed> GetSubstationFeeds(TrenKonumu train)
        {
            double lineResistanceOhmPerMeter = SimulationFormulas.LineResistanceOhmPerMeter(
                _data.CerKatener.RijitKatenerKmDirenci,
                _data.CerKatener.NormalRayKmDirenci);
            var candidates = new List<(TrafoMerkezi Substation, double SourceResistance, double LineResistance, double Conductance)>();

            foreach (var substation in _data.TrafoMerkezleri)
            {
                if (_outOfServiceSubstations.Contains(substation.Ad))
                {
                    continue;
                }

                double substationPosition = train.HatTipi == "H2"
                    ? substation.DilasKonumuH2
                    : substation.DilasKonumuH1;
                double distanceM = Math.Abs(substationPosition - train.Konum);
                if (distanceM >= 3800)
                {
                    continue;
                }

                double transformerResistanceMilliOhm = substation.Direnc > 0
                    ? substation.Direnc
                    : _data.CerKatener.TrafoIcDirenci;
                double sourceResistanceOhm = SimulationFormulas.SourceResistanceOhm(
                    transformerResistanceMilliOhm,
                    substation.FiderKabloDirenci,
                    substation.GeriDonusKabloDirenci);
                double lineResistanceOhm = SimulationFormulas.LineResistanceOhm(
                    lineResistanceOhmPerMeter,
                    distanceM);
                candidates.Add((
                    substation,
                    sourceResistanceOhm,
                    lineResistanceOhm,
                    SimulationFormulas.BranchConductance(sourceResistanceOhm, lineResistanceOhm)));
            }

            double totalConductance = candidates.Sum(candidate => candidate.Conductance);
            return totalConductance <= 0
                ? Array.Empty<SubstationFeed>()
                : candidates.Select(candidate => new SubstationFeed(
                    candidate.Substation,
                    SimulationFormulas.FeedWeight(candidate.Conductance, totalConductance),
                    candidate.SourceResistance,
                    candidate.LineResistance)).ToArray();
        }

        private ElectricalSolution SolveElectricalNetwork(
            IReadOnlyList<TrainElectricalState> states,
            IReadOnlyDictionary<TrafoMerkezi, double> busVoltages,
            double maximumVoltage,
            double efficiency)
        {
            var trainVoltages = states.ToDictionary(
                state => state.Train,
                state => state.Feeds.Count == 0
                    ? 0
                    : state.Feeds.Sum(feed =>
                        feed.Weight * busVoltages.GetValueOrDefault(feed.Substation)));
            Dictionary<TrenKonumu, TrainPowerFlow> powerFlows = new();
            Dictionary<TrafoMerkezi, double> substationCurrents = new();

            for (int iteration = 0; iteration < 30; iteration++)
            {
                powerFlows = states.ToDictionary(
                    state => state.Train,
                    state => CalculatePowerFlow(state, trainVoltages[state.Train], efficiency));
                substationCurrents = CalculateSubstationCurrents(
                    states,
                    powerFlows,
                    trainVoltages);

                double maximumVoltageChange = 0;
                foreach (var state in states)
                {
                    double nextVoltage = CalculateTrainVoltage(
                        state,
                        powerFlows[state.Train],
                        trainVoltages[state.Train],
                        busVoltages,
                        maximumVoltage);
                    double relaxedVoltage = SimulationFormulas.RelaxedVoltageV(
                        trainVoltages[state.Train],
                        nextVoltage,
                        0.5);
                    maximumVoltageChange = Math.Max(
                        maximumVoltageChange,
                        Math.Abs(relaxedVoltage - trainVoltages[state.Train]));
                    trainVoltages[state.Train] = relaxedVoltage;
                }

                if (maximumVoltageChange < 0.05)
                {
                    break;
                }
            }

            powerFlows = states.ToDictionary(
                state => state.Train,
                state => CalculatePowerFlow(state, trainVoltages[state.Train], efficiency));
            substationCurrents = CalculateSubstationCurrents(
                states,
                powerFlows,
                trainVoltages);
            return new ElectricalSolution(powerFlows, trainVoltages, substationCurrents);
        }

        private Dictionary<TrafoMerkezi, double> CalculateSubstationCurrents(
            IReadOnlyList<TrainElectricalState> states,
            IReadOnlyDictionary<TrenKonumu, TrainPowerFlow> powerFlows,
            IReadOnlyDictionary<TrenKonumu, double> trainVoltages)
        {
            var currents = _data.TrafoMerkezleri
                .Where(substation => !_outOfServiceSubstations.Contains(substation.Ad))
                .ToDictionary(substation => substation, _ => 0.0);

            foreach (var state in states)
            {
                double voltage = trainVoltages[state.Train];
                if (voltage <= 0)
                {
                    continue;
                }

                double trainCurrent = SimulationFormulas.CurrentA(
                    powerFlows[state.Train].NetPowerKw,
                    voltage);
                foreach (var feed in state.Feeds)
                {
                    currents[feed.Substation] += trainCurrent * feed.Weight;
                }
            }

            return currents;
        }

        private static double CalculateTrainVoltage(
            TrainElectricalState state,
            TrainPowerFlow powerFlow,
            double previousVoltage,
            IReadOnlyDictionary<TrafoMerkezi, double> substationVoltages,
            double maximumVoltage)
        {
            if (state.Feeds.Count == 0)
            {
                return 0;
            }

            double trainCurrent = SimulationFormulas.CurrentA(powerFlow.NetPowerKw, previousVoltage);
            return SimulationFormulas.TrainVoltageV(
                state.Feeds
                    .Select(feed => (
                        feed.Weight,
                        substationVoltages.GetValueOrDefault(feed.Substation),
                        feed.LineResistanceOhm))
                    .ToArray(),
                trainCurrent,
                maximumVoltage);
        }
    }
}
