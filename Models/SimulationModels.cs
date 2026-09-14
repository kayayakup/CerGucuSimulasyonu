using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CerGucuSimulasyonu.Models
{
    public partial class HatEgimi : ObservableObject
    {
        [ObservableProperty] private double _baslangic;
        [ObservableProperty] private double _bitis;
        [ObservableProperty] private double _egimYuzdesi;
        [ObservableProperty] private string _hatTipi = "H1";

        public double Uzunluk => Math.Max(0, Bitis - Baslangic);
        public string EgimTipi => EgimYuzdesi > 0 ? "Tırmanış / Çıkış (+)" : (EgimYuzdesi < 0 ? "İniş / Eğim (-)" : "Düz Hat");
        public double EgimBinde => Math.Abs(Math.Round(EgimYuzdesi * 10, 1));
        public double EkEgitimKuvvetiKn => Math.Round(236.0 * 9.81 * (EgimYuzdesi / 100.0), 1);
    }

    public partial class HatKurbu : ObservableObject
    {
        [ObservableProperty] private double _baslangic;
        [ObservableProperty] private double _bitis;
        [ObservableProperty] private double _yaricap = 300;
        [ObservableProperty] private string _hatTipi = "H1";

        public double Uzunluk => Math.Max(0, Bitis - Baslangic);
        public double RocklDirenci => Yaricap > 55 ? Math.Round(650.0 / (Yaricap - 55.0), 2) : 0;
        public double MaksHiz => Math.Round(Math.Sqrt(Math.Max(10, Yaricap) * 11.8 * 0.15) * 3.6, 0);
    }

    public partial class CerKatenerParametreleri : ObservableObject
    {
        [ObservableProperty] private double _rijitKatenerKmDirenci = 15;
        [ObservableProperty] private double _normalRayKmDirenci = 21;
        [ObservableProperty] private double _sertlestirilmisRayKmDirenci = 24;
        [ObservableProperty] private double _trafoIcDirenci = 15;
        [ObservableProperty] private double _yuksuzDcBaraGerilimi = 1500;  // Fixed catenary voltage (V) DC
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
        [ObservableProperty] private double _anlikGerilimV = 1500; // Fixed catenary voltage (V)
        [ObservableProperty] private double _yuklenmeYuzdesi = 0;
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
        [ObservableProperty] private double _trenVerimi = 88;

        // Çekiş Gücü (Traction Effort) & Davis Direnç Formülü Parametreleri
        [ObservableProperty] private double _maksCekisKuvvetiKn = 240.0;
        [ObservableProperty] private double _motorGucuKw = 1800.0;
        [ObservableProperty] private double _davisA = 2.5;
        [ObservableProperty] private double _davisB = 0.03;
        [ObservableProperty] private double _davisC = 0.004;

        /// <summary>
        /// Sabit Çekme Kuvveti Bölgesinden Sabit Güç Bölgesine Geçiş Hızı (Base Speed / Temel Hız)
        /// V_base = (P_max * eta) / F_max * 3.6 (km/h)
        /// </summary>
        public double TemelHizKmh => MaksCekisKuvvetiKn > 0 
            ? Math.Round((MotorGucuKw * (TrenVerimi / 100.0)) / MaksCekisKuvvetiKn * 3.6, 1) 
            : 30.0;
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
        [ObservableProperty] private double _katenerGerilimiV = 1500; // Fixed catenary voltage (V)
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

        public double Uzunluk => Math.Max(0, BitisKm - BaslangicKm);
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

