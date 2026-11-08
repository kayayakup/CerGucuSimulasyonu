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
            double TractionPowerKw,
            double RegenerativePowerKw,
            double NetPowerKw);

        private readonly SimulationData _data;
        private readonly IReadOnlySet<string> _outOfServiceSubstations;
        private readonly Random _rand = new();
        private readonly Dictionary<TrafoMerkezi, double> _over150PercentLoadSeconds = new();
        private readonly Dictionary<TrafoMerkezi, double> _over300PercentLoadSeconds = new();

        private double _simTotalElapsed = 0;

        public void ResetTime()
        {
            _simTotalElapsed = 0;
            _over150PercentLoadSeconds.Clear();
            _over300PercentLoadSeconds.Clear();
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
            _simTotalElapsed += dt;
            double maxLine = _data.HatUzunlugu > 0 ? _data.HatUzunlugu : 15350;
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
                        tren.Durum = $"SEFER BEKLİYOR (~{Math.Max(0, tren.DispatchTimeSeconds - _simTotalElapsed):0} sn)";
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
                    if (distToNext <= 25 && tren.AnlikHiz <= 25)
                    {
                        tren.Konum = nextStation.H1OrtaNokta;
                        tren.AnlikHiz = 0;
                        tren.Ivme = 0;
                        tren.IstasyonBeklemeSayaci = 6.0;
                        tren.Durum = "İSTASYONDA (YOLCU ALIYOR)";
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
                if (distToNext < 400 && distToNext > 10)
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
                bool wasBraking = tren.Durum.StartsWith("FRENLİYOR", StringComparison.Ordinal);
                double maxAcc = SimulationFormulas.SpeedRateKmhPerSecond(_data.Tren.MaksIvmelenme);
                double maxDec = SimulationFormulas.SpeedRateKmhPerSecond(_data.Tren.MaksFrenlemeIvmesi);
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
                    maxAcc = Math.Min(
                        maxAcc,
                        SimulationFormulas.SpeedRateKmhPerSecond(forceLimitedAcceleration));
                    double dV = SimulationFormulas.SpeedChangeKmh(speedDiff, maxAcc, dt);
                    tren.AnlikHiz += dV;
                    tren.Ivme = Math.Round(
                        SimulationFormulas.AccelerationFromSpeedChangeMps2(dV, dt),
                        2);
                    tren.Durum = "HIZLANIYOR (CER GÜCÜ ÇEKİYOR)";
                }
                else if (speedDiff < -SpeedControlHysteresisKmh ||
                         (wasBraking && speedDiff <= SpeedControlHysteresisKmh))
                {
                    double requestedDeltaV = SimulationFormulas.RequestedBrakingSpeedChangeKmh(
                        speedDiff,
                        SpeedControlHysteresisKmh,
                        MinimumBrakingAccelerationMps2,
                        dt);
                    double dV = Math.Min(
                        tren.AnlikHiz,
                        SimulationFormulas.SpeedChangeKmh(requestedDeltaV, maxDec, dt));
                    tren.AnlikHiz -= dV;
                    tren.Ivme = -Math.Round(
                        SimulationFormulas.AccelerationFromSpeedChangeMps2(dV, dt),
                        2);
                    tren.Durum = "FRENLİYOR (REJENERATİF FREN)";
                }
                else
                {
                    tren.Ivme = 0;
                    tren.Durum = "SABİT HIZLA HAREKET HALİNDE";
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
            var trainVoltages = electricalStates.ToDictionary(
                state => state.Train,
                state => state.Feeds.Count == 0 ? 0 : nominalVoltage);

            Dictionary<TrenKonumu, TrainPowerFlow> powerFlows = new();
            Dictionary<TrafoMerkezi, double> substationCurrents = new();
            Dictionary<TrafoMerkezi, double> substationVoltages = new();

            for (int iteration = 0; iteration < 30; iteration++)
            {
                powerFlows = electricalStates.ToDictionary(
                    state => state.Train,
                    state => CalculatePowerFlow(state, trainVoltages[state.Train], efficiency));
                CalculateSubstationState(
                    electricalStates,
                    powerFlows,
                    trainVoltages,
                    nominalVoltage,
                    maximumElectricalVoltage,
                    out substationCurrents,
                    out substationVoltages);

                double maximumVoltageChange = 0;
                foreach (var state in electricalStates)
                {
                    double nextVoltage = CalculateTrainVoltage(
                        state,
                        powerFlows[state.Train],
                        trainVoltages[state.Train],
                        substationVoltages,
                        maximumElectricalVoltage);
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

            powerFlows = electricalStates.ToDictionary(
                state => state.Train,
                state => CalculatePowerFlow(state, trainVoltages[state.Train], efficiency));
            CalculateSubstationState(
                electricalStates,
                powerFlows,
                trainVoltages,
                nominalVoltage,
                maximumElectricalVoltage,
                out substationCurrents,
                out substationVoltages);

            foreach (var state in electricalStates)
            {
                var train = state.Train;
                var flow = powerFlows[train];
                double voltage = CalculateTrainVoltage(
                    state,
                    flow,
                    trainVoltages[train],
                    substationVoltages,
                    maximumElectricalVoltage);
                double pantographCurrent = SimulationFormulas.CurrentA(flow.NetPowerKw, voltage);
                double signedTractionPower = SimulationFormulas.SignedTractionPowerKw(
                    flow.TractionPowerKw,
                    flow.RegenerativePowerKw);
                double signedTractionCurrent = SimulationFormulas.CurrentA(signedTractionPower, voltage);

                train.KatenerGerilimiV = Math.Round(voltage, 1);
                train.CekilenGucKw = Math.Round(signedTractionPower, 1);
                train.CekilenAkimA = Math.Round(signedTractionCurrent, 1);
                train.AnlikPantografGucuKw = Math.Round(flow.NetPowerKw, 1);
                train.AnlikPantografAkimiA = Math.Round(pantographCurrent, 1);
                train.ToplamTuketilenEnerjiKwh += SimulationFormulas.EnergyKwh(
                    Math.Max(0, flow.NetPowerKw),
                    dt);
                train.ToplamRejeneratifEnerjiKwh += SimulationFormulas.EnergyKwh(
                    flow.RegenerativePowerKw,
                    dt);
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
                    substation.Durum = "DEVRE DIŞI";
                    substation.IsOutOfSpec = false;
                    substation.NeedsLimitReview = false;
                    substation.LimitStatus = "DEVRE DIŞI";
                    _over150PercentLoadSeconds[substation] = 0;
                    _over300PercentLoadSeconds[substation] = 0;
                    continue;
                }

                double current = substationCurrents.GetValueOrDefault(substation);
                double voltage = substationVoltages.GetValueOrDefault(substation, nominalVoltage);
                double powerKw = SimulationFormulas.DcPowerKw(current, voltage);
                int fedCount = electricalStates.Count(state =>
                    Math.Abs(powerFlows[state.Train].NetPowerKw) > 1 &&
                    state.Feeds.Any(feed =>
                        feed.Substation == substation && feed.Weight > 0.15));
                double rectifierPowerKw = _data.CerKatener.DogrultucuGucu > 0
                    ? _data.CerKatener.DogrultucuGucu
                    : 3000.0;

                substation.AnlikGucKw = Math.Round(powerKw, 1);
                substation.BeslenenTrenSayisi = fedCount;
                substation.AnlikAkimA = Math.Round(current, 1);
                substation.AnlikGerilimV = Math.Round(voltage, 1);
                substation.YuklenmeYuzdesi = Math.Round(
                    SimulationFormulas.LoadPercent(Math.Max(0, powerKw), rectifierPowerKw),
                    1);
                UpdateSubstationLimitStatus(
                    substation,
                    voltage,
                    SimulationFormulas.LoadPercent(Math.Max(0, powerKw), rectifierPowerKw),
                    dt,
                    minimumOperatingVoltage,
                    maximumOperatingVoltage);
                substation.Durum = powerKw switch
                {
                    < -5 => "REJENERATİF",
                    _ when substation.YuklenmeYuzdesi > 300 => "AŞIRI YÜK",
                    _ when substation.YuklenmeYuzdesi > 150 => "YÜKSEK YÜK (DUTY SINIRI)",
                    _ when substation.YuklenmeYuzdesi > 100 => "ANMA GÜCÜ ÜSTÜ",
                    _ when substation.YuklenmeYuzdesi > 5 => "NORMAL",
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
                violations.Add("DOĞRULTUCU %300 YÜK SINIRI 1 DAKİKADAN UZUN AŞILDI");
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

        private TrainPowerDemand CreatePowerDemand(
            TrenKonumu train,
            double trainWeightTon,
            double effectiveTrainMassTon,
            double auxiliaryPowerKw)
        {
            double speedMs = SimulationFormulas.SpeedMetersPerSecond(train.AnlikHiz);
            double resistanceKn = CalculateResistanceKn(train, trainWeightTon, train.AnlikHiz);

            if (train.Durum.StartsWith("FRENLİYOR", StringComparison.Ordinal) ||
                train.Ivme < -0.05)
            {
                return new TrainPowerDemand(
                    0,
                    SimulationFormulas.BrakingEffortKn(effectiveTrainMassTon, train.Ivme),
                    auxiliaryPowerKw);
            }

            if (train.Durum.StartsWith("HIZLANIYOR", StringComparison.Ordinal) ||
                train.Ivme > 0.05)
            {
                double accelerationEffortKn = SimulationFormulas.InertialForceKn(
                    effectiveTrainMassTon,
                    Math.Max(0, train.Ivme));
                return new TrainPowerDemand(
                    SimulationFormulas.RequiredTractionEffortKn(accelerationEffortKn, resistanceKn),
                    0,
                    auxiliaryPowerKw);
            }

            return new TrainPowerDemand(
                speedMs > 0 ? Math.Max(0, resistanceKn) : 0,
                0,
                auxiliaryPowerKw);
        }

        private TrainPowerFlow CalculatePowerFlow(
            TrainElectricalState state,
            double voltageV,
            double efficiency)
        {
            if (state.Feeds.Count == 0 || voltageV <= 0)
            {
                return new TrainPowerFlow(0, 0, 0);
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
                ? SimulationFormulas.MechanicalPowerKw(tractionForceKn, state.Train.AnlikHiz) / efficiency
                : 0;
            double regenerativePowerKw =
                SimulationFormulas.MechanicalPowerKw(regenerativeForceKn, state.Train.AnlikHiz) * efficiency;
            double netPowerKw = SimulationFormulas.NetPantographPowerKw(
                tractionPowerKw,
                state.Demand.AuxiliaryPowerKw,
                regenerativePowerKw);
            return new TrainPowerFlow(tractionPowerKw, regenerativePowerKw, netPowerKw);
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

        private void CalculateSubstationState(
            IReadOnlyList<TrainElectricalState> states,
            IReadOnlyDictionary<TrenKonumu, TrainPowerFlow> powerFlows,
            IReadOnlyDictionary<TrenKonumu, double> trainVoltages,
            double nominalVoltage,
            double maximumVoltage,
            out Dictionary<TrafoMerkezi, double> substationCurrents,
            out Dictionary<TrafoMerkezi, double> substationVoltages)
        {
            substationCurrents = _data.TrafoMerkezleri
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
                    substationCurrents[feed.Substation] += trainCurrent * feed.Weight;
                }
            }

            substationVoltages = new Dictionary<TrafoMerkezi, double>();
            foreach (var substation in _data.TrafoMerkezleri)
            {
                if (_outOfServiceSubstations.Contains(substation.Ad))
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
                substationVoltages[substation] = SimulationFormulas.SubstationVoltageV(
                    nominalVoltage,
                    substationCurrents.GetValueOrDefault(substation),
                    sourceResistanceOhm,
                    maximumVoltage);
            }
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
