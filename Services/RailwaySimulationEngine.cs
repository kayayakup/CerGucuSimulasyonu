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
        private readonly SimulationData _data;
        private readonly IReadOnlySet<string> _outOfServiceSubstations;
        private readonly Random _rand = new();

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
            // 1. Kinematic update for each train (same as original AdvanceSimulationStep)
            double maxLine = _data.HatUzunlugu > 0 ? _data.HatUzunlugu : 15350;
            foreach (var tren in _data.Trenler)
            {
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

                // Determine target speed
                double targetSpeed = _data.Tren.MaksIsletmeHizi;
                if (distToNext < 400 && distToNext > 10)
                {
                    targetSpeed = Math.Max(15, (distToNext / 400.0) * _data.Tren.MaksIsletmeHizi);
                }
                var applicableHizLimiti = _data.HizLimitleri
                    .FirstOrDefault(h => h.HatTipi == tren.HatTipi && tren.Konum >= h.Baslangic && tren.Konum <= h.Bitis);
                if (applicableHizLimiti != null)
                {
                    targetSpeed = Math.Min(targetSpeed, applicableHizLimiti.Limit);
                }
                tren.HedefHiz = targetSpeed;

                // Acceleration / braking
                double speedDiff = targetSpeed - tren.AnlikHiz;
                double maxAcc = _data.Tren.MaksIvmelenme * 3.6; // km/h per second
                double maxDec = _data.Tren.MaksFrenlemeIvmesi * 3.6;
                if (speedDiff > 0.5)
                {
                    double dV = Math.Min(speedDiff, maxAcc * dt);
                    tren.AnlikHiz += dV;
                    tren.Ivme = Math.Round(dV / (dt * 3.6), 2);
                    tren.Durum = "HIZLANIYOR (CER GÜCÜ ÇEKİYOR)";
                }
                else if (speedDiff < -0.5)
                {
                    double dV = Math.Min(Math.Abs(speedDiff), maxDec * dt);
                    tren.AnlikHiz -= dV;
                    tren.Ivme = -Math.Round(dV / (dt * 3.6), 2);
                    tren.Durum = "FRENLİYOR (REJENERATİF FREN)";
                }
                else
                {
                    tren.Ivme = 0;
                    tren.Durum = "SABİT HIZLA HAREKET HALİNDE";
                }

                // Position update
                double vMs = tren.AnlikHiz / 3.6;
                double deltaDistance = vMs * dt;
                tren.KatEdilenMesafeKm += deltaDistance / 1000.0;
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

            // 2. Power calculation – fixed voltage at 1500 V
            double vNominal = 1500; // V
            double auxPowerKw = 150.0;
            double maxTrainTractionKw = 1750.0;
            double maxTrainRegenKw = 1400.0;
            double trainWeightTon = 210.0;

            double totalTractionKw = 0;
            double totalRegenKw = 0;

            double rLineM = ((
                _data.CerKatener.RijitKatenerKmDirenci +
                _data.CerKatener.NormalRayKmDirenci) / 1000.0) / 1000.0;
            if (rLineM <= 0) rLineM = 0.000036;

            foreach (var tren in _data.Trenler)
            {
                double vMs = tren.AnlikHiz / 3.6;
                double vKmh = tren.AnlikHiz;
                double cerPowerKw = 0;

                double davisKn = (2.5 * trainWeightTon + 0.03 * trainWeightTon * vKmh + 0.004 * Math.Pow(vKmh, 2)) / 1000.0;

                var localEgim = _data.HatEgimleri.FirstOrDefault(e => e.HatTipi == tren.HatTipi && tren.Konum >= e.Baslangic && tren.Konum <= e.Bitis);
                double gradeKn = 0;
                if (localEgim != null)
                {
                    gradeKn = trainWeightTon * 9.81 * (localEgim.EgimYuzdesi / 100.0);
                    if (tren.Yon == "geri") gradeKn = -gradeKn;
                }

                var localKurp = _data.HatKurplari.FirstOrDefault(k => k.HatTipi == tren.HatTipi && tren.Konum >= k.Baslangic && tren.Konum <= k.Bitis);
                double curveKn = 0;
                if (localKurp != null && localKurp.Yaricap > 55)
                {
                    curveKn = trainWeightTon * 9.81 * (650.0 / (localKurp.Yaricap - 55.0)) / 1000.0;
                }

                if (tren.Durum.StartsWith("HIZLANIYOR") || (tren.Ivme > 0.05 && vKmh > 1))
                {
                    double fAccKn = trainWeightTon * 1.08 * Math.Max(0.05, tren.Ivme);
                    double fTotalKn = Math.Max(0, fAccKn + davisKn + gradeKn + curveKn);
                    double pMechKw = (fTotalKn * vMs) / (_data.Tren.TrenVerimi > 0 ? (_data.Tren.TrenVerimi / 100.0) : 0.88);
                    pMechKw = Math.Min(maxTrainTractionKw, pMechKw);
                    cerPowerKw = pMechKw + auxPowerKw;
                    totalTractionKw += cerPowerKw;
                    tren.ToplamTuketilenEnerjiKwh += (cerPowerKw * (dt / 3600.0));
                }
                else if (tren.Durum.StartsWith("FRENLİYOR") || (tren.Ivme < -0.05 && vKmh > 2))
                {
                    double fDecKn = trainWeightTon * Math.Abs(tren.Ivme);
                    double pBrakeMechKw = fDecKn * vMs * 0.72;
                    double pRegenKw = Math.Min(maxTrainRegenKw, pBrakeMechKw);
                    cerPowerKw = -pRegenKw + auxPowerKw;
                    totalRegenKw += pRegenKw;
                    tren.ToplamRejeneratifEnerjiKwh += (pRegenKw * (dt / 3600.0));
                }
                else if (vKmh > 0)
                {
                    double fCruisKn = Math.Max(0, davisKn + gradeKn + curveKn);
                    double pCruisKw = (fCruisKn * vMs) / 0.88;
                    pCruisKw = Math.Min(600.0, pCruisKw);
                    cerPowerKw = pCruisKw + auxPowerKw;
                    totalTractionKw += cerPowerKw;
                    tren.ToplamTuketilenEnerjiKwh += (cerPowerKw * (dt / 3600.0));
                }
                else
                {
                    cerPowerKw = auxPowerKw;
                    totalTractionKw += auxPowerKw;
                    tren.ToplamTuketilenEnerjiKwh += (auxPowerKw * (dt / 3600.0));
                }

                tren.CekilenGucKw = Math.Round(cerPowerKw, 1);
                tren.CekilenAkimA = Math.Round((cerPowerKw * 1000.0) / vNominal, 1);
                tren.KatenerGerilimiV = 1500; // constant voltage
            }

            // 3. Transformer loading – constant voltage
            foreach (var tm in _data.TrafoMerkezleri)
            {
                if (_outOfServiceSubstations.Contains(tm.Ad))
                {
                    tm.AnlikGucKw = 0;
                    tm.BeslenenTrenSayisi = 0;
                    tm.AnlikAkimA = 0;
                    tm.YuklenmeYuzdesi = 0;
                    tm.AnlikGerilimV = 0;
                    tm.Durum = "DEVRE DIŞI";
                    continue;
                }

                double tmTotalKw = 0;
                int fedCount = 0;
                foreach (var tren in _data.Trenler)
                {
                    if (tren.CekilenGucKw <= 0) continue;
                    double distM = Math.Abs(tm.DilasKonumuH1 - tren.Konum);
                    if (distM < 3800)
                    {
                        double gTm = 1.0 / (Math.Max(5.0, tm.Direnc) + (distM * rLineM * 1000.0));
                        double sumG = 0;
                        foreach (var otherTm in _data.TrafoMerkezleri)
                        {
                            if (_outOfServiceSubstations.Contains(otherTm.Ad))
                            {
                                continue;
                            }

                            double otherDist = Math.Abs(otherTm.DilasKonumuH1 - tren.Konum);
                            if (otherDist < 3800)
                            {
                                sumG += 1.0 / (Math.Max(5.0, otherTm.Direnc) + (otherDist * rLineM * 1000.0));
                            }
                        }
                        if (sumG > 0)
                        {
                            double weight = gTm / sumG;
                            tmTotalKw += tren.CekilenGucKw * weight;
                            if (weight > 0.15) fedCount++;
                        }
                    }
                }
                tm.AnlikGucKw = Math.Round(tmTotalKw, 1);
                tm.BeslenenTrenSayisi = fedCount;
                tm.AnlikAkimA = Math.Round((tmTotalKw * 1000.0) / vNominal, 1);
                double trafoMaxKw = _data.CerKatener.DogrultucuGucu > 0 ? _data.CerKatener.DogrultucuGucu : 3000;
                tm.YuklenmeYuzdesi = Math.Round((tmTotalKw / trafoMaxKw) * 100.0, 1);
                tm.AnlikGerilimV = 1500; // constant voltage
                tm.Durum = tm.YuklenmeYuzdesi switch
                {
                    > 100 => "AŞIRI YÜK (% KAPASİTE AŞILDI)",
                    > 75 => "YÜKSEK YÜK",
                    > 5 => "NORMAL",
                    _ => "BOŞTA"
                };
            }
        }
    }
}
