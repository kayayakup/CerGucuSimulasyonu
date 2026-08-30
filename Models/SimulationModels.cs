using System.Collections.ObjectModel;

namespace CerGucuSimulasyonu.Models
{
    public class HatEgimi
    {
        public double Baslangic { get; set; }
        public double Bitis { get; set; }
        public double EgimYuzdesi { get; set; }
        public string HatTipi { get; set; } = "H1";
    }

    public class HatKurbu
    {
        public double Baslangic { get; set; }
        public double Bitis { get; set; }
        public double Yaricap { get; set; }
        public string HatTipi { get; set; } = "H1";
    }

    public class CerKatenerParametreleri
    {
        public double RijitKatenerKmDirenci { get; set; } = 15;
        public double NormalRayKmDirenci { get; set; } = 21;
        public double SertlestirilmisRayKmDirenci { get; set; } = 24;
        public double TrafoIcDirenci { get; set; } = 15;
        public double YuksuzDcBaraGerilimi { get; set; } = 1620;
        public double TrafoGucu { get; set; } = 3300;
        public double DogrultucuGucu { get; set; } = 3000;
        public double RayToprakArasiDirenc { get; set; } = 150;
    }

    public class TrafoMerkezi
    {
        public string Ad { get; set; } = "";
        public string Istasyon { get; set; } = "";
        public double Direnc { get; set; } = 15;
        public double DilasKonumuH1 { get; set; }
        public double DilasKonumuH2 { get; set; }
        public double FiderKabloDirenci { get; set; } = 2;
        public double GeriDonusKabloDirenci { get; set; } = 1.5;
    }

    public class RayParalellemesi
    {
        public int No { get; set; }
        public double H1BaglantiKm { get; set; }
        public double H2BaglantiKm { get; set; }
    }

    public class TrenParametreleri
    {
        public double MaksTasarimHizi { get; set; } = 90;
        public double MaksIsletmeHizi { get; set; } = 80;
        public double MaksIvmelenme { get; set; } = 1.1;
        public double MaksFrenlemeIvmesi { get; set; } = 1.1;
        public double JerkLimiti { get; set; } = 1.0;
        public double Aw0BosAgirlik { get; set; } = 152;
        public double Aw3DoluAgirlik { get; set; } = 236;
        public double DonerKutle { get; set; } = 8.75;
        public double YardimciGuc { get; set; } = 300;
        public double MaksGerilim { get; set; } = 1800;
        public double MinGerilim { get; set; } = 1000;
        public double MinIsletmeGerilimi { get; set; } = 1050;
        public double TrenUzunlugu { get; set; } = 90;
        public double TrenVerimi { get; set; } = 85;
    }

    public class Istasyon
    {
        public string Ad { get; set; } = "";
        public string KisaAd { get; set; } = "";
        public double H1OrtaNokta { get; set; }
        public double H2OrtaNokta { get; set; }
        public double Uzunluk { get; set; } = 100;
    }

    public class HizLimiti
    {
        public double Baslangic { get; set; }
        public double Bitis { get; set; }
        public double Limit { get; set; }
        public string HatTipi { get; set; } = "H1";
    }
    
    // Ana model container
    public class SimulationData
    {
        public ObservableCollection<HatEgimi> HatEgimleri { get; set; } = new();
        public ObservableCollection<HatKurbu> HatKurplari { get; set; } = new();
        public CerKatenerParametreleri CerKatener { get; set; } = new();
        public ObservableCollection<TrafoMerkezi> TrafoMerkezleri { get; set; } = new();
        public ObservableCollection<RayParalellemesi> RayParalellemeleri { get; set; } = new();
        public TrenParametreleri Tren { get; set; } = new();
        public ObservableCollection<Istasyon> Istasyonlar { get; set; } = new();
        public ObservableCollection<HizLimiti> HizLimitleri { get; set; } = new();
    }
}
