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
        private RailwaySimulationEngine _engine;

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
        private bool _isResetting;

        [ObservableProperty]
        private SimulationRunResult? _lastRunResult;

        [ObservableProperty]
        private IReadOnlyList<ComplianceResult> _complianceResults = Array.Empty<ComplianceResult>();

        [ObservableProperty]
        private int _selectedReportSecond;

        [ObservableProperty]
        private string _reportHoursText = "00";

        [ObservableProperty]
        private string _reportMinutesText = "00";

        [ObservableProperty]
        private string _reportSecondsText = "00";

        [ObservableProperty]
        private string _reportTimeInputStatus = "";

        [ObservableProperty]
        private SimulationSnapshot? _selectedSnapshot;

        [ObservableProperty]
        private IReadOnlyList<SimulationRunResult> _scenarioResults = Array.Empty<SimulationRunResult>();

        [ObservableProperty]
        private double _simulationSpeed = 1.0;

        partial void OnSimulationSpeedChanged(double value)
        {
            double boundedSpeed = double.IsFinite(value)
                ? Math.Clamp(value, 0.1, 50.0)
                : 1.0;
            if (value != boundedSpeed)
            {
                SimulationSpeed = boundedSpeed;
            }
        }

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
        private double _ortalamaKatenerGerilimiV = 1620;

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
        private bool _isUpdatingReportTime;
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

        public int MaximumReportSecond => LastRunResult?.DurationSeconds ??
                                          SimulationRecorder.MaximumDurationSeconds;
        public string SelectedReportTimeString =>
            FormatSimulationTime(TimeSpan.FromSeconds(SelectedReportSecond));

        partial void OnLastRunResultChanged(SimulationRunResult? value)
        {
            OnPropertyChanged(nameof(MaximumReportSecond));
            ReportTimeInputStatus = "";
            SelectedReportSecond = 0;
            UpdateReportTimeInputs(0);
            SelectedSnapshot = value?.Snapshots.FirstOrDefault();
        }

        partial void OnSelectedReportSecondChanged(int value)
        {
            OnPropertyChanged(nameof(SelectedReportTimeString));
            UpdateReportTimeInputs(value);
            if (LastRunResult is null || LastRunResult.Snapshots.Count == 0)
            {
                SelectedSnapshot = null;
                return;
            }

            SelectedSnapshot = FindNearestSnapshot(LastRunResult.Snapshots, value);
        }

        private static SimulationSnapshot FindNearestSnapshot(
            IReadOnlyList<SimulationSnapshot> snapshots,
            int second)
        {
            int low = 0;
            int high = snapshots.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                int snapshotSecond = snapshots[middle].Second;
                if (snapshotSecond == second)
                {
                    return snapshots[middle];
                }

                if (snapshotSecond < second)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            if (low >= snapshots.Count)
            {
                return snapshots[^1];
            }
            if (high < 0)
            {
                return snapshots[0];
            }

            return second - snapshots[high].Second <= snapshots[low].Second - second
                ? snapshots[high]
                : snapshots[low];
        }

        partial void OnReportHoursTextChanged(string value) => UpdateReportTimeFromInputs();

        partial void OnReportMinutesTextChanged(string value) => UpdateReportTimeFromInputs();

        partial void OnReportSecondsTextChanged(string value) => UpdateReportTimeFromInputs();

        private void UpdateReportTimeFromInputs()
        {
            if (_isUpdatingReportTime)
            {
                return;
            }

            if (!int.TryParse(ReportHoursText, out int hours) ||
                !int.TryParse(ReportMinutesText, out int minutes) ||
                !int.TryParse(ReportSecondsText, out int seconds) ||
                hours < 0 ||
                minutes is < 0 or > 59 ||
                seconds is < 0 or > 59)
            {
                ReportTimeInputStatus = "Saat, dakika ve saniye için geçerli tam sayılar girin.";
                return;
            }

            long requestedSeconds = (long)hours * 3600 + minutes * 60 + seconds;
            int boundedSeconds = (int)Math.Min(requestedSeconds, MaximumReportSecond);
            ReportTimeInputStatus = requestedSeconds > MaximumReportSecond
                ? $"Koşu süresi {FormatSimulationTime(TimeSpan.FromSeconds(MaximumReportSecond))} ile sınırlı."
                : "";
            SelectedReportSecond = boundedSeconds;
        }

        private static string FormatSimulationTime(TimeSpan time) =>
            $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";

        private void UpdateReportTimeInputs(int totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            _isUpdatingReportTime = true;
            try
            {
                ReportHoursText = ((int)time.TotalHours).ToString("00");
                ReportMinutesText = time.Minutes.ToString("00");
                ReportSecondsText = time.Seconds.ToString("00");
            }
            finally
            {
                _isUpdatingReportTime = false;
            }
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
                    else
                    {
                        StartTrainsAtDepot();
                    }
                    UpdateUiMetricsAndCharts();
                    return;
                }
                catch
                {
                    // Fallback
                }
            }

            // Reported route lengths: H1 ends at 15,394 m and H2 at 15,347 m.
            Data.HatUzunlugu = 15394;

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

            // Katener Seksiyonları (EK-1 Montaj Karnesi Belgesine Göre Seksiyonlama)
            Data.KatenerSeksiyonlari.Clear();
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 1, Ad = "Seksiyon 1 (Darıca Sahil TM-1 Bölgesi)", BaslangicKm = 0, BitisKm = 514, BesleyenTrafolar = "TM-1 (Darıca Sahil)", MontajEtaplari = "Etap 01-07" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 2, Ad = "Seksiyon 2 (Darıca Cumhuriyet TM-2 Bölgesi)", BaslangicKm = 514, BitisKm = 1433, BesleyenTrafolar = "TM-2 (Darıca Cumhuriyet)", MontajEtaplari = "Etap 09-11" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 3, Ad = "Seksiyon 3 (Farabi - Marmaray Gar TM-3 Bölgesi)", BaslangicKm = 1433, BitisKm = 4711, BesleyenTrafolar = "TM-3 (Gebze Marmaray Gar)", MontajEtaplari = "Etap 13-35" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 4, Ad = "Seksiyon 4 (Fatih - Kent Meydanı Bölgesi)", BaslangicKm = 4711, BitisKm = 7032, BesleyenTrafolar = "TM-3 / TM-4", MontajEtaplari = "Etap 37-53" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 5, Ad = "Seksiyon 5 (Stadyum - Akse Sapağı TM-4 Bölgesi)", BaslangicKm = 7032, BitisKm = 9389, BesleyenTrafolar = "TM-4 (Akse Sapağı)", MontajEtaplari = "Etap 55-65" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 6, Ad = "Seksiyon 6 (Adliye - Mutlukent TM-5 Bölgesi)", BaslangicKm = 9389, BitisKm = 12186, BesleyenTrafolar = "TM-5 (Mutlukent)", MontajEtaplari = "Etap 67-85" });
            Data.KatenerSeksiyonlari.Add(new KatenerSeksiyonu { SeksiyonNo = 7, Ad = "Seksiyon 7 (OSB - Depo TM-6 & TM-7 Bölgesi)", BaslangicKm = 12186, BitisKm = Data.HatUzunlugu, BesleyenTrafolar = "TM-6 (OSB) / TM-7 (Depo)", MontajEtaplari = "Etap 87+" });

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

            DocumentTrackProfile.Apply(Data);

            // Başlangıç Trenleri
            GenerateRandomTrains();
            UpdateUiMetricsAndCharts();
        }

        #region Canlı Simülasyon Motoru (Simulation Engine)

        [RelayCommand]
        public void ToggleSimulation()
        {
            if (IsResetting)
            {
                return;
            }

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
            if (IsResetting)
            {
                return;
            }

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
        public async Task ResetSimulation()
        {
            if (IsResetting)
            {
                return;
            }

            bool wasRunning = IsSimulationRunning;
            _simTimer?.Stop();
            IsSimulationRunning = false;
            IsResetting = true;
            StatusText = "🔄 Simülasyon sıfırlanıyor...";
            await Task.Yield();

            try
            {
                _elapsedSimTime = TimeSpan.Zero;
                SimulationTimeString = "00:00:00";

                PowerChart?.Clear();
                VoltageChart?.Clear();
                SelectedTrainSpeedChart?.Clear();
                TrafoLoadChart?.Clear();

                LastRunResult = null;
                ComplianceResults = Array.Empty<ComplianceResult>();
                ScenarioResults = Array.Empty<SimulationRunResult>();
                SelectedSnapshot = null;
                SelectedReportSecond = 0;

                LoadInitialMockData();
                _engine = new RailwaySimulationEngine(Data);
                BeginRecording();
                if (wasRunning)
                {
                    IsResetting = false;
                    StartSimulation();
                    StatusText = "🔄 Simülasyon sıfırlandı ve yeniden başlatıldı.";
                }
                else
                {
                    StatusText = "🔄 Simülasyon sıfırlandı.";
                }
            }
            finally
            {
                IsResetting = false;
            }
        }

        private void SimTimer_Tick(object? sender, EventArgs e)
        {
            double dt = 0.2 * SimulationSpeed; // saniye
            _elapsedSimTime = _elapsedSimTime.Add(TimeSpan.FromSeconds(dt));
            SimulationTimeString = FormatSimulationTime(_elapsedSimTime);

            _engine.AdvanceOneStep(dt);
            UpdateUiMetricsAndCharts();

            _simulationRecorder.CaptureIfDue(Data, _elapsedSimTime.TotalSeconds, dt);
            if (_simulationRecorder.IsComplete)
            {
                LastRunResult = _simulationRecorder.Complete();
                ComplianceResults = EvaluateCompliance(LastRunResult);
                StopSimulation();
                StatusText = "Simülasyon 1 haftalık kayıt sınırına ulaştı.";
            }
        }

        private void UpdateUiMetricsAndCharts()
        {
            double totalTractionKw = Data.Trenler
                .Where(t => t.AnlikPantografGucuKw > 0)
                .Sum(t => t.AnlikPantografGucuKw);
            double totalRegenKw = Data.Trenler
                .Where(t => t.CekilenGucKw < 0)
                .Sum(t => Math.Abs(t.CekilenGucKw));

            ToplamCekilenGucKw = Math.Round(totalTractionKw, 1);
            ToplamRejeneratifGucKw = Math.Round(totalRegenKw, 1);
            NetSistemGucuKw = Math.Round(
                Data.Trenler.Sum(t => t.AnlikPantografGucuKw),
                1);

            ToplamTuketilenEnerjiKwh = Math.Round(Data.Trenler.Sum(t => t.ToplamTuketilenEnerjiKwh), 3);
            ToplamGeriKazanilanEnerjiKwh = Math.Round(Data.Trenler.Sum(t => t.ToplamRejeneratifEnerjiKwh), 3);

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
                "SELECT" => "Mod: Seç & Canlı İzle | Hat üzerindeki öğelere tıklayarak canlı verilerini inceleyin.",
                "DELETE" => "Mod: Sil | Silmek istediğiniz harita öğesine tıklayın ve işlemi onaylayın. Sağ tık menüsü de kullanılabilir.",
                "STATION" => "Mod: İstasyon Ekle | Haritaya tıklayın, önizlemeyi inceleyip onaylayın.",
                "TRAFO" => "Mod: Trafo Merkezi Ekle | Haritaya tıklayın, önizlemeyi inceleyip onaylayın.",
                "SPEED" => "Mod: Hız Limiti Ekle | Sürükleyerek kesimi belirleyin; önizlemeyi onaylayarak ekleyin.",
                "EGIM" => "Mod: Hat Eğimi Ekle | Sürükleyerek kesimi seçin; önizlemede açı ve H1 yönündeki yukarı/aşağı yönü belirleyin.",
                "KURP" => "Mod: Kurp Ekle | Yatayda aralığı seçin; yukarı sürükleme H1'i, aşağı sürükleme H2'yi iç hatta alır. Yarıçapı R alanına yazıp önizlemeyi onaylayın.",
                "PARALLEL" => "Mod: Ray Paralellemesi Ekle | Haritaya tıklayın, önizlemeyi inceleyip onaylayın.",
                _ => $"Mod: {mode}"
            };
        }

        [RelayCommand]
        public void GenerateRandomTrains()
        {
            Data.Trenler.Clear();
            int trainCount = _rand.Next(5, 8);
            double depotPosition = GetDepotStartPosition();
            double headwaySeconds = Data.Isletme.SeferAraligiHeadwaySaniye > 0
                ? Data.Isletme.SeferAraligiHeadwaySaniye
                : 90;

            for (int i = 0; i < trainCount; i++)
            {
                Data.Trenler.Add(new TrenKonumu
                {
                    Id = i + 1,
                    TrenAdi = $"Tren {i + 1:00}",
                    Konum = depotPosition,
                    HatTipi = "H2",
                    Yon = "geri",
                    AnlikHiz = 0,
                    IsDispatched = false,
                    DispatchTimeSeconds = i * headwaySeconds,
                    YolcuSayisi = _rand.Next(80, 250),
                    Durum = "DEPO SAHASINDA - SEFER BEKLİYOR"
                });
            }

            if (Data.Trenler.Count > 0)
            {
                SelectedItem = Data.Trenler[0];
            }

            StatusText = $"🎲 {trainCount} tren Depo sahasına yerleştirildi; H2 üzerinden {headwaySeconds:0} sn sefer aralığıyla Darıca yönünde çıkacak.";
        }

        [RelayCommand]
        public void AddSingleTrain()
        {
            int newId = Data.Trenler.Count + 1;
            double headwaySeconds = Data.Isletme.SeferAraligiHeadwaySaniye > 0
                ? Data.Isletme.SeferAraligiHeadwaySaniye
                : 90;
            double dispatchTime = _elapsedSimTime.TotalSeconds +
                Math.Max(1, Data.Trenler.Count) * headwaySeconds;
            var train = new TrenKonumu
            {
                Id = newId,
                TrenAdi = $"Tren {newId:00}",
                Konum = GetDepotStartPosition(),
                HatTipi = "H2",
                Yon = "geri",
                AnlikHiz = 0,
                IsDispatched = false,
                DispatchTimeSeconds = dispatchTime,
                YolcuSayisi = 120,
                Durum = "DEPO SAHASINDA - SEFER BEKLİYOR"
            };
            Data.Trenler.Add(train);
            SelectedItem = train;
            StatusText = $"🚃 Tren #{newId} Depo sahasına eklendi; planlanan çıkış: {dispatchTime:0} sn.";
        }

        private double GetDepotStartPosition()
        {
            double lineLength = Data.HatUzunlugu > 0 ? Data.HatUzunlugu : 15394;
            double depotStationPosition = Data.Istasyonlar
                .Where(station => station.Ad.Contains("depo", StringComparison.OrdinalIgnoreCase))
                .Select(station => station.H2OrtaNokta)
                .DefaultIfEmpty(lineLength)
                .Last();
            return Math.Clamp(depotStationPosition, 0, lineLength);
        }

        private void StartTrainsAtDepot()
        {
            double depotPosition = GetDepotStartPosition();
            double headwaySeconds = Data.Isletme.SeferAraligiHeadwaySaniye > 0
                ? Data.Isletme.SeferAraligiHeadwaySaniye
                : 90;

            for (int i = 0; i < Data.Trenler.Count; i++)
            {
                TrenKonumu train = Data.Trenler[i];
                train.Konum = depotPosition;
                train.HatTipi = "H2";
                train.Yon = "geri";
                train.AnlikHiz = 0;
                train.IsDispatched = false;
                train.DispatchTimeSeconds = _elapsedSimTime.TotalSeconds + i * headwaySeconds;
                train.Durum = "DEPO SAHASINDA - SEFER BEKLİYOR";
                train.CekilenGucKw = 0;
                train.CekilenAkimA = 0;
                train.AnlikPantografGucuKw = 0;
                train.AnlikPantografAkimiA = 0;
            }
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
                        foreach (var train in Data.Trenler)
                        {
                            train.CekilenGucKw = 0;
                            train.CekilenAkimA = 0;
                            train.AnlikPantografGucuKw = 0;
                            train.AnlikPantografAkimiA = 0;
                        }
                        StartTrainsAtDepot();
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
