using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CerGucuSimulasyonu.Models
{
    public sealed record TrainForceCurvePoint(double SpeedKmh, double ForceKn);

    public sealed record TrainCurrentCurvePoint(
        double VoltageV,
        double TractionCurrentA,
        double RegenerativeCurrentA);

    public partial class HatEgimi : ObservableObject
    {
        [ObservableProperty] private double _baslangic;
        [ObservableProperty] private double _bitis;
        [ObservableProperty] private double _egimYuzdesi;
        [ObservableProperty] private string _hatTipi = "H1";

        public double Uzunluk => SimulationFormulas.Length(Baslangic, Bitis);
        public string EgimTipi => EgimYuzdesi > 0 ? "Tırmanış / Çıkış (+)" : (EgimYuzdesi < 0 ? "İniş / Eğim (-)" : "Düz Hat");
        public double EgimBinde => SimulationFormulas.GradePermille(EgimYuzdesi);
        public double EkEgitimKuvvetiKn => SimulationFormulas.ReferenceGradeForceKn(EgimYuzdesi);
    }

    public partial class HatKurbu : ObservableObject
    {
        [ObservableProperty] private double _baslangic;
        [ObservableProperty] private double _bitis;
        [ObservableProperty] private double _yaricap = 300;
        [ObservableProperty] private string _hatTipi = "H1";

        public double Uzunluk => SimulationFormulas.Length(Baslangic, Bitis);
        public double RocklDirenci => SimulationFormulas.CurveResistanceCoefficient(Yaricap);
        public double MaksHiz => SimulationFormulas.CurveSpeedLimitKmh(Yaricap);
    }

    public partial class CerKatenerParametreleri : ObservableObject
    {
        [ObservableProperty] private double _rijitKatenerKmDirenci = 15;
        [ObservableProperty] private double _normalRayKmDirenci = 21;
        [ObservableProperty] private double _sertlestirilmisRayKmDirenci = 24;
        [ObservableProperty] private double _trafoIcDirenci = 15;
        [ObservableProperty] private double _yuksuzDcBaraGerilimi = 1620;
        [ObservableProperty] private double _trafoGucu = 3300;
        [ObservableProperty] private double _dogrultucuGucu = 3000;
        [ObservableProperty] private double _rayToprakArasiDirenc = 150;
    }

    public partial class TrafoMerkezi : ObservableObject
    {
        [ObservableProperty] private string _ad = "TM";
        [ObservableProperty] private string _istasyon = "";
        [ObservableProperty] private double _direnc = 15;
        [ObservableProperty] private double _dilasKonumuH1 = 1500;
        [ObservableProperty] private double _dilasKonumuH2 = 1500;
        [ObservableProperty] private double _fiderKabloDirenci = 2;
        [ObservableProperty] private double _geriDonusKabloDirenci = 1.5;

        // Canlı Simülasyon Verileri
        [ObservableProperty] private double _anlikGucKw = 0;
        [ObservableProperty] private double _anlikAkimA = 0;
        [ObservableProperty] private double _anlikGerilimV = 1620;
        [ObservableProperty] private double _yuklenmeYuzdesi = 0;
        [JsonIgnore] public bool IsOutOfSpec { get; set; }
        [JsonIgnore] public bool NeedsLimitReview { get; set; }
        [JsonIgnore] public string LimitStatus { get; set; } = "KONTROL BEKLİYOR";
        [ObservableProperty] private string _durum = "NORMAL";
        [ObservableProperty] private int _beslenenTrenSayisi = 0;
    }

    public partial class RayParalellemesi : ObservableObject
    {
        [ObservableProperty] private int _no = 1;
        [ObservableProperty] private double _h1BaglantiKm = 1500;
        [ObservableProperty] private double _h2BaglantiKm = 1500;

        public string Ad => $"P{No} Ray Paralellemesi";
        public string Durum => "AKTİF";
        public string BagliHatlar => "Hat 1 (H1) ↔ Hat 2 (H2)";
    }

    public partial class IsletmeParametreleri : ObservableObject
    {
        [ObservableProperty] private int _toplamTrenSayisi = 4;
        [ObservableProperty] private double _seferAraligiHeadwaySaniye = 90.0;
        [ObservableProperty] private double _minimumTrenMesafesiMetre = 400.0;
        [ObservableProperty] private double _ilkTrenBaslangicKonumuMetre = 0.0;
        [ObservableProperty] private double _en50122MinGerilim = 1000.0;
        [ObservableProperty] private double _en50122NominalGerilim = 1500.0;
        [ObservableProperty] private double _en50122MaksGerilim = 1800.0;
    }

    public partial class TrenParametreleri : ObservableObject
    {
        [ObservableProperty] private double _maksTasarimHizi = 90;
        [ObservableProperty] private double _maksIsletmeHizi = 80;
        [ObservableProperty] private double _maksIvmelenme = 1.1;
        [ObservableProperty] private double _maksFrenlemeIvmesi = 1.1;
        [ObservableProperty] private double _jerkLimiti = 1.0;
        [ObservableProperty] private double _aw0BosAgirlik = 152;
        [ObservableProperty] private double _aw3DoluAgirlik = 236;
        [ObservableProperty] private double _donerKutle = 8.75;
        [ObservableProperty] private double _yardimciGuc = 300;
        [ObservableProperty] private double _maksGerilim = 1800;
        [ObservableProperty] private double _minGerilim = 1000;
        [ObservableProperty] private double _minIsletmeGerilimi = 1050;
        [ObservableProperty] private double _trenUzunlugu = 90;
        [ObservableProperty] private double _trenVerimi = 85;
        [ObservableProperty] private int _aracSayisi = 4;
        [ObservableProperty] private double _yardimciGucKullanimOrani = 0.5;

        // Davis values are configurable estimates; they are not taken from the supplied reports.
        [ObservableProperty] private double _maksCekisKuvvetiKn = 290.0;
        [ObservableProperty] private double _motorGucuKw = 1800.0;
        [ObservableProperty] private double _davisA = 2.5;
        [ObservableProperty] private double _davisB = 0.03;
        [ObservableProperty] private double _davisC = 0.004;
        // Force curves and current limits describe the complete four-car train set.
        [ObservableProperty] private ObservableCollection<TrainForceCurvePoint> _tractionEffortCurve = new()
        {
            new(0, 290), new(35, 290), new(40, 250), new(45, 225), new(50, 200),
            new(55, 165), new(60, 138), new(65, 115), new(70, 100), new(75, 85),
            new(80, 75)
        };
        [ObservableProperty] private ObservableCollection<TrainForceCurvePoint> _regenerativeBrakeEffortCurve = new()
        {
            new(0, 280), new(68, 280), new(80, 230)
        };
        [ObservableProperty] private ObservableCollection<TrainCurrentCurvePoint> _currentLimitCurve = new()
        {
            new(1000, 0, -2750),
            new(1350, 2300, -2750),
            new(1500, 2300, -2750),
            new(1700, 2030, -2750),
            new(1800, 1900, -2350),
            new(1920, 0, 0),
            new(2000, 0, 0)
        };

        public double MaximumTractionCurrentA(double voltageV) =>
            SimulationFormulas.InterpolateCurrent(
                CurrentLimitCurve,
                voltageV,
                point => point.TractionCurrentA);

        public double MaximumRegenerativeCurrentA(double voltageV) =>
            Math.Abs(SimulationFormulas.InterpolateCurrent(
                CurrentLimitCurve,
                voltageV,
                point => point.RegenerativeCurrentA));

        public double RegenerativeVoltageCutoffV(double maximumOperatingVoltageV)
            => SimulationFormulas.RegenerativeVoltageCutoffV(CurrentLimitCurve, maximumOperatingVoltageV);

        public double TractionEffortAtSpeedKn(double speedKmh)
            => SimulationFormulas.TractionEffortAtSpeedKn(
                TractionEffortCurve,
                MaksCekisKuvvetiKn,
                speedKmh);

        public double RegenerativeBrakeEffortAtSpeedKn(double speedKmh) =>
            SimulationFormulas.InterpolateForce(RegenerativeBrakeEffortCurve, speedKmh);

        public double MaximumTractionForceKn(
            double speedKmh,
            double voltageV,
            double auxiliaryPowerKw = 0)
        {
            double efficiency = TrenVerimi > 0
                ? SimulationFormulas.PercentToRatio(TrenVerimi)
                : 0.85;
            return SimulationFormulas.MaximumTractionForceKn(
                TractionEffortCurve,
                CurrentLimitCurve,
                MaksCekisKuvvetiKn,
                speedKmh,
                voltageV,
                efficiency,
                auxiliaryPowerKw);
        }

        public double MaximumRegenerativeBrakeForceKn(double speedKmh, double voltageV)
        {
            double efficiency = TrenVerimi > 0
                ? SimulationFormulas.PercentToRatio(TrenVerimi)
                : 0.85;
            return SimulationFormulas.MaximumRegenerativeBrakeForceKn(
                RegenerativeBrakeEffortCurve,
                CurrentLimitCurve,
                speedKmh,
                voltageV,
                efficiency);
        }

        /// <summary>
        /// Sabit çekme kuvvetinden sabit güç bölgesine geçiş hızını merkezi formülden alır.
        /// </summary>
        public double TemelHizKmh =>
            SimulationFormulas.BaseSpeedKmh(
                MotorGucuKw,
                SimulationFormulas.PercentToRatio(TrenVerimi),
                MaksCekisKuvvetiKn);
    }

    public partial class Istasyon : ObservableObject
    {
        [ObservableProperty] private int _istasyonId;
        [ObservableProperty] private string _ad = "İstasyon";
        [ObservableProperty] private string _kisaAd = "IST";
        [ObservableProperty] private double _h1OrtaNokta;
        [ObservableProperty] private double _h2OrtaNokta;
        [ObservableProperty] private double _uzunluk = 120;
        
        // Canlı Simülasyon Verileri
        [ObservableProperty] private int _bekleyenYolcu = 45;
        [ObservableProperty] private string _yaklasanTrenler = "Bekleniyor...";
        [ObservableProperty] private string _durum = "NORMAL";
    }

    public partial class HizLimiti : ObservableObject
    {
        [ObservableProperty] private double _baslangic;
        [ObservableProperty] private double _bitis;
        [ObservableProperty] private double _limit = 80;
        [ObservableProperty] private string _hatTipi = "H1";
    }

    public partial class TrenKonumu : ObservableObject
    {
        [ObservableProperty] private int _id;
        [ObservableProperty] private string _trenAdi = "Tren 01";
        [ObservableProperty] private double _konum = 0;
        [ObservableProperty] private string _hatTipi = "H1"; // H1 veya H2
        [ObservableProperty] private string _yon = "ileri";  // ileri veya geri
        [ObservableProperty] private double _anlikHiz = 0;

        // Sefer ve Headway Bilgileri
        [ObservableProperty] private bool _isDispatched = true;
        [ObservableProperty] private double _dispatchTimeSeconds = 0;

        // Canlı Simülasyon & Cer Gücü Dinamik Verileri
        [ObservableProperty] private string _durum = "HAREKET HALİNDE"; // HAREKET HALİNDE, İSTASYONDA, HIZLANIYOR, FRENLİYOR
        [ObservableProperty] private double _hedefHiz = 70;
        [ObservableProperty] private double _ivme = 0; // m/s²
        [ObservableProperty] private double _cekilenGucKw = 0;
        [ObservableProperty] private double _cekilenAkimA = 0;
        [ObservableProperty] private double _anlikPantografGucuKw = 0;
        [ObservableProperty] private double _anlikPantografAkimiA = 0;
        [ObservableProperty] private double _katenerGerilimiV = 1620;
        [JsonIgnore] public bool IsOutOfSpec { get; set; }
        [JsonIgnore] public bool NeedsLimitReview { get; set; }
        [JsonIgnore] public string LimitStatus { get; set; } = "KONTROL BEKLİYOR";
        [ObservableProperty] private int _yolcuSayisi = 120;
        [ObservableProperty] private string _sonrakiIstasyon = "";
        [ObservableProperty] private double _sonrakiIstasyonMesafe = 0;
        [ObservableProperty] private double _istasyonBeklemeSayaci = 0; // Saniye cinsinden
        [ObservableProperty] private string _enYakinTrafo = "";

        // Enerji ve İstatistikler
        [ObservableProperty] private double _toplamTuketilenEnerjiKwh = 0;
        [ObservableProperty] private double _toplamRejeneratifEnerjiKwh = 0;
        [ObservableProperty] private double _katEdilenMesafeKm = 0;
    }

    public partial class KatenerSeksiyonu : ObservableObject
    {
        [ObservableProperty] private int _seksiyonNo = 1;
        [ObservableProperty] private string _ad = "Seksiyon 1";
        [ObservableProperty] private double _baslangicKm;
        [ObservableProperty] private double _bitisKm;
        [ObservableProperty] private string _hatTipi = "H1";
        [ObservableProperty] private string _besleyenTrafolar = "TM-1";
        [ObservableProperty] private string _montajEtaplari = "Etap 01-07";
        [ObservableProperty] private double _anlikToplamGucKw = 0;
        [ObservableProperty] private double _anlikToplamAkimA = 0;
        [ObservableProperty] private double _ortalamaGerilimV = 1620;
        [ObservableProperty] private int _aktifTrenSayisi = 0;

        public double Uzunluk => SimulationFormulas.Length(BaslangicKm, BitisKm);
        public string KonumAraligi => $"{BaslangicKm:0} m – {BitisKm:0} m ({BaslangicKm/1000.0:0.000} – {BitisKm/1000.0:0.000} km)";
    }

    public partial class SeksiyonAyirici : ObservableObject
    {
        [ObservableProperty] private int _no = 1;
        [ObservableProperty] private string _ad = "İzoleli Overlap";
        [ObservableProperty] private double _konum = 514;
        [ObservableProperty] private string _hatTipi = "H1";
        [ObservableProperty] private string _tip = "İzoleli Overlap (PE 938)";
        [ObservableProperty] private string _aciklama = "Katener Seksiyon Ayırıcı";
    }

// KatenerEtap class removed

    // Ana model container
    public partial class SimulationData : ObservableObject
    {
        [ObservableProperty] private double _hatUzunlugu = 15350;
        [ObservableProperty] private CerKatenerParametreleri _cerKatener = new();
        [ObservableProperty] private TrenParametreleri _tren = new();
        [ObservableProperty] private IsletmeParametreleri _isletme = new();
        [ObservableProperty] private ObservableCollection<HatEgimi> _hatEgimleri = new();
        [ObservableProperty] private ObservableCollection<HatKurbu> _hatKurplari = new();
        [ObservableProperty] private ObservableCollection<TrafoMerkezi> _trafoMerkezleri = new();
        [ObservableProperty] private ObservableCollection<RayParalellemesi> _rayParalellemeleri = new();
        [ObservableProperty] private ObservableCollection<KatenerSeksiyonu> _katenerSeksiyonlari = new();
// KatenerEtap collection removed
        [ObservableProperty] private ObservableCollection<SeksiyonAyirici> _seksiyonAyiricilar = new();
        [ObservableProperty] private ObservableCollection<Istasyon> _istasyonlar = new();
        [ObservableProperty] private ObservableCollection<HizLimiti> _hizLimitleri = new();
        [ObservableProperty] private ObservableCollection<TrenKonumu> _trenler = new();
    }
}
