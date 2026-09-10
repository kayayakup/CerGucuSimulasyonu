using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Win32;
using CerGucuSimulasyonu.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CerGucuSimulasyonu.Services;
using CerGucuSimulasyonu.Reporting;

namespace CerGucuSimulasyonu.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        [ObservableProperty]
        private SimulationData _data = new();
        private readonly RailwaySimulationEngine _engine;

        [ObservableProperty]
        private string _activeToolMode = "SELECT";

        [ObservableProperty]
        private double _zoomLevel = 1.0;

        [ObservableProperty]
        private string _statusText = "Hazır | Sol panelden parametreleri belirleyebilir, simülasyonu başlatıp canlı izleyebilirsiniz.";

        [ObservableProperty]
        private object? _selectedItem;

        // Canlı Simülasyon Kontrol Özellikleri
        [ObservableProperty]
        private bool _isSimulationRunning = false;

        [ObservableProperty]
        private SimulationRunResult? _lastRunResult;

        [ObservableProperty]
        private IReadOnlyList<ComplianceResult> _complianceResults = Array.Empty<ComplianceResult>();

        [ObservableProperty]
        private int _selectedReportSecond;

        [ObservableProperty]
        private SimulationSnapshot? _selectedSnapshot;

        [ObservableProperty]
        private IReadOnlyList<SimulationRunResult> _scenarioResults = Array.Empty<SimulationRunResult>();

        [ObservableProperty]
        private double _simulationSpeed = 1.0; // 1x, 2x, 5x

        [ObservableProperty]
        private string _simulationTimeString = "00:00:00";

        // Global Cer Gücü & Enerji Metrikleri (Rapor ve Grafikler İçin)
        [ObservableProperty]
        private double _toplamCekilenGucKw = 0;

        [ObservableProperty]
        private double _toplamRejeneratifGucKw = 0;

        [ObservableProperty]
        private double _netSistemGucuKw = 0;

        [ObservableProperty]
        private double _toplamTuketilenEnerjiKwh = 0;

        [ObservableProperty]
        private double _toplamGeriKazanilanEnerjiKwh = 0;

        [ObservableProperty]
        private double _enYuksekTrafoYukYuzdesi = 0;

        [ObservableProperty]
        private string _enCokYuklenenTrafoAdi = "-";

        [ObservableProperty]
        private double _ortalamaKatenerGerilimiV = 1500;

        [ObservableProperty]
        private double _minHatGerilimiV = 1620;

        [ObservableProperty]
        private double _toplamTasınanYolcu = 0;

        [ObservableProperty]
        private double _ortalamaTrenHiziKmh = 0;

        // Canlı Grafik Referansları (View tarafından bağlanacak)
        public LiveChartsControl? PowerChart { get; set; }
        public LiveChartsControl? VoltageChart { get; set; }
        public LiveChartsControl? SelectedTrainSpeedChart { get; set; }
        public LiveChartsControl? TrafoLoadChart { get; set; }

        private DispatcherTimer? _simTimer;
        private TimeSpan _elapsedSimTime = TimeSpan.Zero;
        private readonly Random _rand = new();
        private readonly SimulationRecorder _simulationRecorder = new();
        private readonly ScenarioDefinition _activeScenario = new(
            "NORMAL",
            "Normal Isletme",
            "Tum cer gucu trafo merkezleri devrede.",
            90,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public MainViewModel()
        {
            SetupSimulationTimer();
            LoadInitialMockData();
            _engine = new RailwaySimulationEngine(Data);
            BeginRecording();
        }

        public SimulationRecorder SimulationRecorder => _simulationRecorder;

        partial void OnLastRunResultChanged(SimulationRunResult? value)
        {
            SelectedReportSecond = 0;
            SelectedSnapshot = value?.Snapshots.FirstOrDefault();
        }

        partial void OnSelectedReportSecondChanged(int value)
        {
            if (LastRunResult is null || LastRunResult.Snapshots.Count == 0)
            {
                SelectedSnapshot = null;
                return;
            }

            SelectedSnapshot = LastRunResult.Snapshots
                .OrderBy(snapshot => Math.Abs(snapshot.Second - value))
                .First();
        }

        private void BeginRecording()
        {
            _simulationRecorder.Start(_activeScenario);
            _simulationRecorder.CaptureIfDue(Data, _elapsedSimTime.TotalSeconds);
            LastRunResult = null;
            ComplianceResults = Array.Empty<ComplianceResult>();
            SelectedSnapshot = null;
        }

        private void SetupSimulationTimer()
        {
            _simTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // 5 Hz güncelleme
            };
            _simTimer.Tick += SimTimer_Tick;
        }

        public void LoadInitialMockData()
        {
            if (File.Exists("SimulasyonVerileri.json"))
            {
                try
                {
                    LoadData();
                    if (Data.Trenler.Count == 0)
                    {
                        GenerateRandomTrains();
                    }
                    UpdateSimulationCalculations(0.1);
                    return;
                }
                catch
                {
                    // Fallback
                }
            }

            // Fallback varsayılan Gebze-Darıca Metro Hattı (15,350 m - DWG Metrajları)
            Data.HatUzunlugu = 15350;

            // 12 İstasyon (DWG Gerçek Metrajları - Karşılıklı Peronlar Eşit)
            var istasyonListesi = new (int id, string ad, string kisa, double pos)[]
            {
                (1, "Darıca Sahil", "SAHİL", 186.1),
                (2, "Darıca Cumhuriyet Meydanı", "CUMHURİYET", 1407.0),
                (3, "Farabi Devlet Hastanesi", "FARABİ", 3327.8),
                (4, "Gebze Marmaray Gar", "GAR", 4421.1),
                (5, "Fatih Devlet Hastanesi", "FATİH", 5790.6),
                (6, "Gebze Kent Meydanı", "MEYDAN", 7131.8),
                (7, "Gebze Stadyum", "STADYUM", 8225.3),
                (8, "Akse Sapağı", "AKSE", 9032.3),
                (9, "Adliye", "ADLİYE", 10366.9),
                (10, "Mutlukent", "MUTLUKENT", 12073.2),
                (11, "OSB", "OSB", 13891.6),
                (12, "Depo / Portal", "DEPO", 15347.0)
            };

            Data.Istasyonlar.Clear();
            foreach (var item in istasyonListesi)
            {
                Data.Istasyonlar.Add(new Istasyon
                {
                    IstasyonId = item.id,
                    Ad = item.ad,
                    KisaAd = item.kisa,
                    H1OrtaNokta = item.pos,
                    H2OrtaNokta = item.pos,
                    Uzunluk = item.id == 11 ? 115 : 100
                });
            }

            // 7 Cer Trafosu
            var trafoListesi = new (string ad, string ist, double pos)[]
            {
                ("TM-1 (Darıca Sahil)", "Darıca Sahil", 186),
                ("TM-2 (Darıca Cumhuriyet)", "Darıca Cumhuriyet", 1407),
                ("TM-3 (Gebze Marmaray Gar)", "Gebze Marmaray Gar", 4421),
                ("TM-4 (Akse Sapağı)", "Akse Sapağı", 9032),
                ("TM-5 (Mutlukent)", "Mutlukent", 12073),
                ("TM-6 (OSB)", "OSB", 13892),
                ("TM-7 (Depo)", "Depo", 15347)
            };

            Data.TrafoMerkezleri.Clear();
            foreach (var tm in trafoListesi)
            {
                Data.TrafoMerkezleri.Add(new TrafoMerkezi
                {
                    Ad = tm.ad,
                    Istasyon = tm.ist,
                    Direnc = 15,
                    DilasKonumuH1 = tm.pos,
                    DilasKonumuH2 = tm.pos,
                    FiderKabloDirenci = 2,
                    GeriDonusKabloDirenci = 1.5
                });
            }

            // Ray Paralellemeleri
            Data.RayParalellemeleri.Clear();
            double[] rpPos = { 2400, 5100, 7800, 11200, 14600 };
            for (int i = 0; i < rpPos.Length; i++)
            {
                Data.RayParalellemeleri.Add(new RayParalellemesi
                {
                    No = i + 1,
                    H1BaglantiKm = rpPos[i],
                    H2BaglantiKm = rpPos[i]
                });
            }

            // Katener Seksiyonları (EK-1 Montaj Karnesi Belgesine Göre Seksiyonlama)
            Data.KatenerSeksiyonlari.Clear();
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 1, Ad = "Seksiyon 1 (Darıca Sahil TM-1 Bölgesi)", BaslangicKm = 0, BitisKm = 514, BesleyenTrafolar = "TM-1 (Darıca Sahil)", MontajEtaplari = "Etap 01-07" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 2, Ad = "Seksiyon 2 (Darıca Cumhuriyet TM-2 Bölgesi)", BaslangicKm = 514, BitisKm = 1433, BesleyenTrafolar = "TM-2 (Darıca Cumhuriyet)", MontajEtaplari = "Etap 09-11" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 3, Ad = "Seksiyon 3 (Farabi - Marmaray Gar TM-3 Bölgesi)", BaslangicKm = 1433, BitisKm = 4711, BesleyenTrafolar = "TM-3 (Gebze Marmaray Gar)", MontajEtaplari = "Etap 13-35" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 4, Ad = "Seksiyon 4 (Fatih - Kent Meydanı Bölgesi)", BaslangicKm = 4711, BitisKm = 7032, BesleyenTrafolar = "TM-3 / TM-4", MontajEtaplari = "Etap 37-53" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 5, Ad = "Seksiyon 5 (Stadyum - Akse Sapağı TM-4 Bölgesi)", BaslangicKm = 7032, BitisKm = 9389, BesleyenTrafolar = "TM-4 (Akse Sapağı)", MontajEtaplari = "Etap 55-65" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 6, Ad = "Seksiyon 6 (Adliye - Mutlukent TM-5 Bölgesi)", BaslangicKm = 9389, BitisKm = 12186, BesleyenTrafolar = "TM-5 (Mutlukent)", MontajEtaplari = "Etap 67-85" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 7, Ad = "Seksiyon 7 (OSB - Depo TM-6 & TM-7 Bölgesi)", BaslangicKm = 12186, BitisKm = 15350, BesleyenTrafolar = "TM-6 (OSB) / TM-7 (Depo)", MontajEtaplari = "Etap 87+" });

            // Seksiyon Ayırıcılar (İzoleli Overlap - PE 938)
            Data.SeksiyonAyiricilar.Clear();
            double[] isoKms = { 514, 1433, 4711, 7032, 9389, 10300, 12186 };
            for (int i = 0; i < isoKms.Length; i++)
            {
                Data.SeksiyonAyiricilar.Add(new SeksiyonAyirici
                {
                    No = i + 1,
                    Ad = $"İzoleli Overlap ISO-{i + 1}",
                    Konum = isoKms[i],
                    HatTipi = "H1",
                    Tip = "İzoleli Overlap (PE 938)",
                    Aciklama = "Katener Seksiyon Ayırıcı"
                });
            }

            // Hız Limitleri
            Data.HizLimitleri.Clear();
            Data.HizLimitleri.Add(new HizLimiti { Baslangic = 0, Bitis = 15350, Limit = 80, HatTipi = "H1" });
            Data.HizLimitleri.Add(new HizLimiti { Baslangic = 0, Bitis = 15350, Limit = 80, HatTipi = "H2" });

            // Başlangıç Trenleri
            GenerateRandomTrains();
            UpdateSimulationCalculations(0.1);
        }

        #region Canlı Simülasyon Motoru (Simulation Engine)

        [RelayCommand]
        public void ToggleSimulation()
        {
            if (IsSimulationRunning)
            {
                StopSimulation();
            }
            else
            {
                StartSimulation();
            }
        }

        [RelayCommand]
        public void StartSimulation()
        {
            if (LastRunResult is not null)
            {
                BeginRecording();
            }
            IsSimulationRunning = true;
            _simTimer?.Start();
            StatusText = "▶ Simülasyon BAŞLATILDI — Trenler hat üzerinde hareket ediyor, cer gücü ve enerji canlı hesaplanıyor.";
        }

        [RelayCommand]
        public void StopSimulation()
        {
            IsSimulationRunning = false;
            _simTimer?.Stop();
            if (LastRunResult is null && _simulationRecorder.CurrentRun is not null)
            {
                LastRunResult = _simulationRecorder.Complete();
                ComplianceResults = EvaluateCompliance(LastRunResult);
            }
            StatusText = "⏸ Simülasyon DURDURULDU.";
        }

        [RelayCommand]
        public void ExportWordReport()
        {
            if (LastRunResult is null)
            {
                StatusText = "Önce bir simülasyon koşusu tamamlayın.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Word belgesi (*.docx)|*.docx",
                FileName = $"CerGucu_{LastRunResult.Scenario.Id}_{DateTime.Now:yyyyMMdd_HHmm}.docx"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            new WordReportExporter().Export(dialog.FileName, LastRunResult, ComplianceResults);
            StatusText = $"Word raporu oluşturuldu: {dialog.FileName}";
        }

        [RelayCommand]
        public void ExportExcelReport()
        {
            if (LastRunResult is null)
            {
                StatusText = "Önce bir simülasyon koşusu tamamlayın.";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Excel çalışma kitabı (*.xlsx)|*.xlsx",
                FileName = $"CerGucu_{LastRunResult.Scenario.Id}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            new ExcelReportExporter().Export(dialog.FileName, LastRunResult, ComplianceResults);
            StatusText = $"Excel raporu oluşturuldu: {dialog.FileName}";
        }

        [RelayCommand]
        public async Task RunAllScenarios()
        {
            StopSimulation();
            StatusText = "Toplu senaryo koşusu başlatıldı...";
            var runner = new ScenarioRunner();
            ScenarioResults = await runner.RunAsync(Data, ScenarioCatalog.CreateDefault());
            if (ScenarioResults.Count > 0)
            {
                LastRunResult = ScenarioResults[0];
                ComplianceResults = EvaluateCompliance(LastRunResult);
            }
            StatusText = $"Toplu senaryo koşusu tamamlandı: {ScenarioResults.Count} senaryo.";
        }

        private static IReadOnlyList<ComplianceResult> EvaluateCompliance(SimulationRunResult result)
        {
            var evaluator = new ComplianceEvaluator();
            return evaluator.Evaluate(result, StandardsCatalog.CreateDefault());
        }

        [RelayCommand]
        public void ResetSimulation()
        {
            bool wasRunning = IsSimulationRunning;
            StopSimulation();
            _elapsedSimTime = TimeSpan.Zero;
            SimulationTimeString = "00:00:00";

            // Grafikleri temizle
            PowerChart?.Clear();
            VoltageChart?.Clear();
            SelectedTrainSpeedChart?.Clear();
            TrafoLoadChart?.Clear();

            LoadInitialMockData();
            BeginRecording();
            if (wasRunning)
            {
                StartSimulation();
            }
            StatusText = "🔄 Simülasyon sıfırlandı.";
        }

        [RelayCommand]
        public void SetSpeed(string speedMultiplier)
        {
            if (double.TryParse(speedMultiplier, out double mult))
            {
                SimulationSpeed = mult;
                StatusText = $"⏩ Simülasyon Hızı: {mult}x";
            }
        }

        private void SimTimer_Tick(object? sender, EventArgs e)
        {
            double dt = 0.2 * SimulationSpeed; // saniye
            _elapsedSimTime = _elapsedSimTime.Add(TimeSpan.FromSeconds(dt));
            SimulationTimeString = _elapsedSimTime.ToString(@"hh\:mm\:ss");

            _engine.AdvanceOneStep(dt);

            _simulationRecorder.CaptureIfDue(Data, _elapsedSimTime.TotalSeconds);
            if (_simulationRecorder.IsComplete)
            {
                LastRunResult = _simulationRecorder.Complete();
                ComplianceResults = EvaluateCompliance(LastRunResult);
                StopSimulation();
                StatusText = "Simülasyon 1 saatlik kayıt sınırına ulaştı.";
            }
        }

        private void AdvanceSimulationStep(double dt)
        {
            double maxLine = Data.HatUzunlugu > 0 ? Data.HatUzunlugu : 15350;

            foreach (var tren in Data.Trenler)
            {
                // İstasyon kontrolü ve bekleme
                if (tren.IstasyonBeklemeSayaci > 0)
                {
                    tren.IstasyonBeklemeSayaci -= dt;
                    tren.AnlikHiz = 0;
                    tren.Ivme = 0;
                    tren.Durum = "İSTASYONDA (YOLCU ALIYOR)";
                    continue;
                }

                // En yakın ve sonraki istasyon
                Istasyon? nextStation = null;
                if (tren.Yon == "ileri")
                {
                    nextStation = Data.Istasyonlar
                        .Where(s => s.H1OrtaNokta > tren.Konum)
                        .OrderBy(s => s.H1OrtaNokta)
                        .FirstOrDefault();
                }
                else
                {
                    nextStation = Data.Istasyonlar
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

                    // İstasyon yaklaşımı: Eğer 25m içindeyse dur
                    if (distToNext <= 25 && tren.AnlikHiz <= 25)
                    {
                        tren.Konum = nextStation.H1OrtaNokta;
                        tren.AnlikHiz = 0;
                        tren.Ivme = 0;
                        tren.IstasyonBeklemeSayaci = 6.0; // 6 saniye dur
                        tren.Durum = "İSTASYONDA (YOLCU ALIYOR)";

                        // Yolcu iniş binişi
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

                // Hedef Hız & Frenleme / Hızlanma
                double targetSpeed = Data.Tren.MaksIsletmeHizi;

                // İstasyona yaklaşıyorsa frenleme profili
                if (distToNext < 400 && distToNext > 10)
                {
                    targetSpeed = Math.Max(15, (distToNext / 400.0) * Data.Tren.MaksIsletmeHizi);
                }

                // Hız Limitlerini kontrol et
                var applicableHizLimiti = Data.HizLimitleri
                    .FirstOrDefault(h => h.HatTipi == tren.HatTipi && tren.Konum >= h.Baslangic && tren.Konum <= h.Bitis);
                if (applicableHizLimiti != null)
                {
                    targetSpeed = Math.Min(targetSpeed, applicableHizLimiti.Limit);
                }

                tren.HedefHiz = targetSpeed;

                // İvmelenme / Frenleme
                double speedDiff = targetSpeed - tren.AnlikHiz;
                double maxAcc = Data.Tren.MaksIvmelenme * 3.6; // (km/h) / s
                double maxDec = Data.Tren.MaksFrenlemeIvmesi * 3.6;

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

                // Pozisyon güncelleme
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
                        tren.HatTipi = "H2"; // H2 hattına geçiş
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
                        tren.HatTipi = "H1"; // H1 hattına geçiş
                        tren.AnlikHiz = 0;
                        tren.IstasyonBeklemeSayaci = 8.0;
                    }
                }
            }

            // Elektriksel Cer Gücü ve Trafo Hesaplamalarını Yap
            UpdateSimulationCalculations(dt);
        }

        private void UpdateSimulationCalculations(double dt)
        {
            double vNominal = Data.CerKatener.YuksuzDcBaraGerilimi > 0 ? Data.CerKatener.YuksuzDcBaraGerilimi : 1620;
            double auxPowerKw = 150.0; // Gerçekçi yardımcı güç (klima, kompresör, aydınlatma)
            double maxTrainTractionKw = 1750.0; // 4'lü metro dizisi için fiziksel cer invertör gücü sınırı
            double maxTrainRegenKw = 1400.0; // Maksimum elektrikli rejenerasyon gücü
            double trainWeightTon = 210.0; // AW2/AW3 ortalama 4'lü dizi kütlesi

            double totalTractionKw = 0;
            double totalRegenKw = 0;

            // Hat iletkenlik parametresi (Rijit Katener + Ray Direnci)
            double rLineM = ((Data.CerKatener.RijitKatenerKmDirenci + Data.CerKatener.NormalRayKmDirenci) / 1000.0) / 1000.0;
            if (rLineM <= 0) rLineM = 0.000036; // 0.036 Ohm/km

            // 1. TRENLERİN FİZİKSEL ÇEKİŞ / FREN GÜÇLERİ HESAPLANIR
            foreach (var tren in Data.Trenler)
            {
                double vMs = tren.AnlikHiz / 3.6;
                double vKmh = tren.AnlikHiz;
                double cerPowerKw = 0;

                // Davis Sürtünme Direnci (kN)
                double davisKn = (2.5 * trainWeightTon + 0.03 * trainWeightTon * vKmh + 0.004 * Math.Pow(vKmh, 2)) / 1000.0;

                // Yerel Hat Eğimi Direnci (kN)
                var localEgim = Data.HatEgimleri.FirstOrDefault(e => e.HatTipi == tren.HatTipi && tren.Konum >= e.Baslangic && tren.Konum <= e.Bitis);
                double gradeKn = 0;
                if (localEgim != null)
                {
                    gradeKn = trainWeightTon * 9.81 * (localEgim.EgimYuzdesi / 100.0);
                    if (tren.Yon == "geri") gradeKn = -gradeKn;
                }

                // Yerel Kurp Direnci (kN)
                var localKurp = Data.HatKurplari.FirstOrDefault(k => k.HatTipi == tren.HatTipi && tren.Konum >= k.Baslangic && tren.Konum <= k.Bitis);
                double curveKn = 0;
                if (localKurp != null && localKurp.Yaricap > 55)
                {
                    curveKn = trainWeightTon * 9.81 * (650.0 / (localKurp.Yaricap - 55.0)) / 1000.0;
                }

                if (tren.Durum.StartsWith("HIZLANIYOR") || (tren.Ivme > 0.05 && vKmh > 1))
                {
                    // Hızlanma: F_cer = m*(1+rotary)*a + R_davis + F_grade + F_curve
                    double fAccKn = trainWeightTon * 1.08 * Math.Max(0.05, tren.Ivme);
                    double fTotalKn = Math.Max(0, fAccKn + davisKn + gradeKn + curveKn);

                    // Güç = F * v / verim
                    double pMechKw = (fTotalKn * vMs) / (Data.Tren.TrenVerimi > 0 ? (Data.Tren.TrenVerimi / 100.0) : 0.88);

                    // Fiziksel inverter güç limiti ile sınırla
                    pMechKw = Math.Min(maxTrainTractionKw, pMechKw);
                    cerPowerKw = pMechKw + auxPowerKw;
                    totalTractionKw += cerPowerKw;
                    tren.ToplamTuketilenEnerjiKwh += (cerPowerKw * (dt / 3600.0));
                }
                else if (tren.Durum.StartsWith("FRENLİYOR") || (tren.Ivme < -0.05 && vKmh > 2))
                {
                    // Rejeneratif Frenleme: Güç şebekeye geri basılır
                    double fDecKn = trainWeightTon * Math.Abs(tren.Ivme);
                    double pBrakeMechKw = fDecKn * vMs * 0.72; // %72 geri kazanım verimi
                    double pRegenKw = Math.Min(maxTrainRegenKw, pBrakeMechKw);

                    cerPowerKw = -pRegenKw + auxPowerKw;
                    totalRegenKw += pRegenKw;
                    tren.ToplamRejeneratifEnerjiKwh += (pRegenKw * (dt / 3600.0));
                }
                else if (vKmh > 0)
                {
                    // Sabit Hızda Seyir
                    double fCruisKn = Math.Max(0, davisKn + gradeKn + curveKn);
                    double pCruisKw = (fCruisKn * vMs) / 0.88;
                    pCruisKw = Math.Min(600.0, pCruisKw);
                    cerPowerKw = pCruisKw + auxPowerKw;
                    totalTractionKw += cerPowerKw;
                    tren.ToplamTuketilenEnerjiKwh += (cerPowerKw * (dt / 3600.0));
                }
                else
                {
                    // İstasyonda bekleme / duruş
                    cerPowerKw = auxPowerKw;
                    totalTractionKw += auxPowerKw;
                    tren.ToplamTuketilenEnerjiKwh += (auxPowerKw * (dt / 3600.0));
                }

                tren.CekilenGucKw = Math.Round(cerPowerKw, 1);
                tren.CekilenAkimA = Math.Round((cerPowerKw * 1000.0) / vNominal, 1);
            }

            // 2. DC CER BESLEME AĞ ÇÖZÜCÜSÜ (Bilateral DC Traction Power Solver)
            // Her trafonun yükü ve akımı komşuluk ve hat mesafesine göre pürüzsüz paylaştırılır.
            foreach (var tm in Data.TrafoMerkezleri)
            {
                double tmTotalKw = 0;
                int fedCount = 0;

                foreach (var tren in Data.Trenler)
                {
                    if (tren.CekilenGucKw <= 0) continue;

                    double distM = Math.Abs(tm.DilasKonumuH1 - tren.Konum);

                    // Eğer tren bu trafoya 3800m'den yakınsa akım payı hesapla
                    if (distM < 3800)
                    {
                        // Tüm aday trafolar arasındaki elektriksel iletkenlik (1 / R_toplam)
                        double gTm = 1.0 / (Math.Max(5.0, tm.Direnc) + (distM * rLineM * 1000.0));

                        double sumG = 0;
                        foreach (var otherTm in Data.TrafoMerkezleri)
                        {
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

                double trafoMaxKw = Data.CerKatener.DogrultucuGucu > 0 ? Data.CerKatener.DogrultucuGucu : 3000;
                tm.YuklenmeYuzdesi = Math.Round((tmTotalKw / trafoMaxKw) * 100.0, 1);

                // Trafo iç direnci kaynaklı bara gerilim düşümü
                double rTrafoOhm = tm.Direnc / 1000.0;
                tm.AnlikGerilimV = Math.Round(Math.Max(1400, vNominal - (tm.AnlikAkimA * rTrafoOhm)), 1);

                tm.Durum = tm.YuklenmeYuzdesi switch
                {
                    > 100 => "AŞIRI YÜK (% KAPASİTE AŞILDI)",
                    > 75 => "YÜKSEK YÜK",
                    > 5 => "NORMAL",
                    _ => "BOŞTA"
                };
            }

            // 3. TREN KATENER GERİLİMLERİNİ GÜNCELLE
            foreach (var tren in Data.Trenler)
            {
                var nearestTrafo = Data.TrafoMerkezleri
                    .OrderBy(t => Math.Abs(t.DilasKonumuH1 - tren.Konum))
                    .FirstOrDefault();

                if (nearestTrafo != null)
                {
                    tren.EnYakinTrafo = nearestTrafo.Ad;
                    double distM = Math.Abs(nearestTrafo.DilasKonumuH1 - tren.Konum);
                    double rLine = distM * rLineM;
                    double iTrain = Math.Max(0, (tren.CekilenGucKw * 1000.0) / vNominal);
                    double vDrop = iTrain * rLine;

                    // Katener gerilimi
                    double vKatener = nearestTrafo.AnlikGerilimV - vDrop;
                    if (tren.CekilenGucKw < 0)
                    {
                        // Rejeneratif frenlemede gerilim hafif yükselir
                        vKatener = vNominal + Math.Min(120.0, Math.Abs(tren.CekilenGucKw) * 0.08);
                    }
                    tren.KatenerGerilimiV = Math.Round(Math.Max(1100, Math.Min(1780, vKatener)), 1);
                }
            }

            // 4. KATENER SEKSİYONLARI VE MONTAJ ETAPLARI ENERJİ ANALİZİNİ GÜNCELLE
            foreach (var sek in Data.KatenerSeksiyonlari)
            {
                var sekTrains = Data.Trenler
                    .Where(t => t.Konum >= sek.BaslangicKm && t.Konum <= sek.BitisKm)
                    .ToList();

                sek.AktifTrenSayisi = sekTrains.Count;
                double sekKw = sekTrains.Sum(t => Math.Max(0, t.CekilenGucKw));
                sek.AnlikToplamGucKw = Math.Round(sekKw, 1);
                sek.AnlikToplamAkimA = Math.Round((sekKw * 1000.0) / vNominal, 1);
                sek.OrtalamaGerilimV = sekTrains.Count > 0
                    ? Math.Round(sekTrains.Average(t => t.KatenerGerilimiV), 1)
                    : vNominal;
            }

            foreach (var etap in Data.KatenerEtaplari)
            {
                var etapTrains = Data.Trenler
                    .Where(t => t.Konum >= etap.BaslangicKm && t.Konum <= etap.BitisKm)
                    .ToList();

                etap.AktifTrenSayisi = etapTrains.Count;
                double etapKw = etapTrains.Sum(t => Math.Max(0, t.CekilenGucKw));
                etap.AnlikToplamGucKw = Math.Round(etapKw, 1);
                etap.AnlikToplamAkimA = Math.Round((etapKw * 1000.0) / vNominal, 1);
            }

            // 5. GLOBAL METRİKLERİ GÜNCELLE
            ToplamCekilenGucKw = Math.Round(totalTractionKw, 1);
            ToplamRejeneratifGucKw = Math.Round(totalRegenKw, 1);
            NetSistemGucuKw = Math.Round(totalTractionKw - totalRegenKw, 1);
            ToplamTuketilenEnerjiKwh += Math.Round((totalTractionKw * (dt / 3600.0)), 3);
            ToplamGeriKazanilanEnerjiKwh += Math.Round((totalRegenKw * (dt / 3600.0)), 3);

            var peakTrafo = Data.TrafoMerkezleri.OrderByDescending(t => t.YuklenmeYuzdesi).FirstOrDefault();
            EnYuksekTrafoYukYuzdesi = peakTrafo?.YuklenmeYuzdesi ?? 0;
            EnCokYuklenenTrafoAdi = peakTrafo != null && peakTrafo.YuklenmeYuzdesi > 0 ? peakTrafo.Ad : "-";

            if (Data.Trenler.Count > 0)
            {
                OrtalamaKatenerGerilimiV = Math.Round(Data.Trenler.Average(t => t.KatenerGerilimiV), 1);
                MinHatGerilimiV = Math.Round(Data.Trenler.Min(t => t.KatenerGerilimiV), 1);
                OrtalamaTrenHiziKmh = Math.Round(Data.Trenler.Average(t => t.AnlikHiz), 1);
                ToplamTasınanYolcu = Data.Trenler.Sum(t => t.YolcuSayisi);
            }

            // 3. İstasyonlardaki yaklaşan tren ETA bilgilerini güncelle
            foreach (var ist in Data.Istasyonlar)
            {
                var approaching = Data.Trenler
                    .Where(t => (t.Yon == "ileri" && t.Konum <= ist.H1OrtaNokta && (ist.H1OrtaNokta - t.Konum) <= 3000) ||
                                (t.Yon == "geri" && t.Konum >= ist.H1OrtaNokta && (t.Konum - ist.H1OrtaNokta) <= 3000))
                    .OrderBy(t => Math.Abs(t.Konum - ist.H1OrtaNokta))
                    .ToList();

                if (approaching.Any())
                {
                    var trainInfoList = approaching.Select(t =>
                    {
                        double dist = Math.Abs(ist.H1OrtaNokta - t.Konum);
                        int etaSec = t.AnlikHiz > 5 ? (int)(dist / (t.AnlikHiz / 3.6)) : 0;
                        return $"{t.TrenAdi} (~{etaSec} sn | {dist:0}m)";
                    });
                    ist.YaklasanTrenler = string.Join(", ", trainInfoList);
                }
                else
                {
                    ist.YaklasanTrenler = "3 km içinde tren yok";
                }
            }

            // 4. Canlı Grafikleri Besle
            string timeStr = DateTime.Now.ToString("HH:mm:ss");
            PowerChart?.AddPoint(ToplamCekilenGucKw, ToplamRejeneratifGucKw, timeStr);
            VoltageChart?.AddPoint(OrtalamaKatenerGerilimiV, MinHatGerilimiV, timeStr);
            TrafoLoadChart?.AddPoint(EnYuksekTrafoYukYuzdesi, null, timeStr);

            if (SelectedItem is TrenKonumu selTrain)
            {
                SelectedTrainSpeedChart?.AddPoint(selTrain.AnlikHiz, selTrain.CekilenGucKw, timeStr);
            }
        }

        #endregion

        [RelayCommand]
        public void SetToolMode(string mode)
        {
            ActiveToolMode = mode;
            StatusText = mode switch
            {
                "SELECT" => "Mod: Seç & Canlı İzle | Hat üzerindeki trenlere, trafolara ve istasyonlara tıklayarak soldaki panelde canlı verilerini inceleyin.",
                "STATION" => "Mod: İstasyon Ekle | Hat üzerine sol tıklayarak yeni istasyon ekleyin.",
                "TRAFO" => "Mod: Trafo Merkezi Ekle | Hat üzerine sol tıklayarak yeni trafo merkezi ekleyin.",
                "SPEED" => "Mod: Hız Limiti Ekle | Hat üzerinde basılı tutup sürükleyerek hız limiti bölgesi oluşturun.",
                "EGIM" => "Mod: Hat Eğimi Ekle | Hat üzerinde basılı tutup sürükleyerek eğim bölgesi oluşturun.",
                "KURP" => "Mod: Hat Kurbu Ekle | Hat üzerinde basılı tutup sürükleyerek kurp bölgesi oluşturun.",
                "PARALLEL" => "Mod: Ray Paralellemesi Ekle | Hat üzerine sol tıklayarak ray paralellemesi ekleyin.",
                _ => $"Mod: {mode}"
            };
        }

        [RelayCommand]
        public void GenerateRandomTrains()
        {
            Data.Trenler.Clear();
            int trainCount = _rand.Next(5, 8);
            double lineLen = Data.HatUzunlugu > 0 ? Data.HatUzunlugu : 17850;

            for (int i = 0; i < 1; i++)
            {
                double pos = Math.Round((i + 1) * (lineLen / (trainCount + 1)), 1);
                string track = i % 2 == 0 ? "H1" : "H2";
                string dir = track == "H1" ? "ileri" : "geri";
                double speed = _rand.Next(45, 75);

                Data.Trenler.Add(new TrenKonumu
                {
                    Id = i + 1,
                    TrenAdi = $"Tren {i + 1:00}",
                    Konum = pos,
                    HatTipi = track,
                    Yon = dir,
                    AnlikHiz = speed,
                    YolcuSayisi = _rand.Next(80, 250),
                    Durum = "HAREKET HALİNDE"
                });
            }

            if (Data.Trenler.Count > 0)
            {
                SelectedItem = Data.Trenler[0];
            }

            StatusText = $"🎲 {trainCount} adet tren Gebze-Darıca hattına yerleştirildi.";
        }

        [RelayCommand]
        public void AddSingleTrain()
        {
            double lineLen = Data.HatUzunlugu > 0 ? Data.HatUzunlugu : 17850;
            int newId = Data.Trenler.Count + 1;
            var train = new TrenKonumu
            {
                Id = newId,
                TrenAdi = $"Tren {newId:00}",
                Konum = Math.Round(lineLen / 2, 1),
                HatTipi = "H1",
                Yon = "ileri",
                AnlikHiz = 60,
                YolcuSayisi = 120,
                Durum = "HAREKET HALİNDE"
            };
            Data.Trenler.Add(train);
            SelectedItem = train;
            StatusText = $"🚃 Tren #{newId} hatta eklendi.";
        }

        [RelayCommand]
        public void ZoomIn()
        {
            ZoomLevel = Math.Min(5.0, ZoomLevel * 1.25);
            StatusText = $"🔍 Yakınlaştırma: %{(int)(ZoomLevel * 100)}";
        }

        [RelayCommand]
        public void ZoomOut()
        {
            ZoomLevel = Math.Max(0.2, ZoomLevel / 1.25);
            StatusText = $"🔍 Yakınlaştırma: %{(int)(ZoomLevel * 100)}";
        }

        [RelayCommand]
        public void ZoomReset()
        {
            ZoomLevel = 1.0;
            StatusText = "🔍 Yakınlaştırma sıfırlandı (%100)";
        }

        [RelayCommand]
        public void SaveData()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(Data, options);
                File.WriteAllText("SimulasyonVerileri.json", jsonString);
                StatusText = "💾 Simülasyon verileri 'SimulasyonVerileri.json' dosyasına kaydedildi.";
            }
            catch (Exception ex)
            {
                StatusText = $"❌ Kaydetme hatası: {ex.Message}";
            }
        }

        [RelayCommand]
        public void LoadData()
        {
            if (File.Exists("SimulasyonVerileri.json"))
            {
                try
                {
                    string jsonString = File.ReadAllText("SimulasyonVerileri.json");
                    var loadedData = JsonSerializer.Deserialize<SimulationData>(jsonString);
                    if (loadedData != null)
                    {
                        Data = loadedData;
                        if (Data.Trenler.Any())
                        {
                            SelectedItem = Data.Trenler.First();
                        }
                        StatusText = "📂 'SimulasyonVerileri.json' dosyasından veriler yüklendi.";
                    }
                }
                catch (Exception ex)
                {
                    StatusText = $"❌ Yükleme hatası: {ex.Message}";
                }
            }
            else
            {
                StatusText = "⚠️ 'SimulasyonVerileri.json' dosyası bulunamadı.";
            }
        }
    }
}
