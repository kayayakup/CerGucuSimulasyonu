namespace CerGucuSimulasyonu.Models;

public static class DocumentTrackProfile
{
    // Source: CER GÜCÜ SİSTEMİ SİMÜLASYONU GİRİŞ VERİLERİ, Tables 1, 2, and 5.
    private static readonly (double Start, double End, double Grade)[] H1Grades =
    {
        (0, 520, 0), (608, 1265, 4.40), (1353, 1539, 0), (1687, 3149, 2.28),
        (3274, 3458, 0), (3609, 4109, -3.61), (4218, 4511, 0), (4601, 5598, 4.50),
        (5733, 5919, 0), (6054, 6616, 4.50), (6691, 6804, 2), (6864, 6969, 3.40),
        (7071, 7261, 0), (7422, 8045, 2.69), (8207, 8357, 0), (8501, 8892, -4.10),
        (9015, 9207, 0), (9304, 10079, 2.43), (10201, 10690, 0), (10796, 10954, 3.02),
        (11042, 11522, 0.50), (11610, 11921, 3.03), (12036, 12178, 0), (12277, 12870, -3.30),
        (13069, 13332, 3.35), (13402, 13550, 1), (13620, 13750, 4.50), (13840, 14027, 0),
        (14163, 15007, 0.91), (15122, 15241, -1), (15341, 15394, 0)
    };

    private static readonly (double Start, double End, double Grade)[] H2Grades =
    {
        (0, 511, 0), (601, 1240, 4.50), (1330, 1521, 0), (1670, 3130, 2.28),
        (3256, 3431, 0), (3572, 4066, -3.63), (4189, 4490, 0), (4610, 5614, 4.50),
        (5704, 5878, 0), (6012, 6558, 4.45), (6661, 6781, 2), (6841, 6944, 3.44),
        (7047, 7222, 0), (7388, 7990, 2.76), (8155, 8295, 0), (8424, 8796, -4.30),
        (8925, 9126, 0), (9248, 10007, 2.44), (10129, 10618, 0), (10718, 10893, 2.86),
        (10976, 11474, 0.50), (11561, 11878, 2.99), (11992, 12145, 0), (12244, 12837, -3.30),
        (13041, 13281, 3.50), (13356, 13509, 1), (13579, 13709, 4.50), (13799, 13981, 0),
        (14118, 14961, 0.91), (15076, 15195, -1), (15295, 15347, 0)
    };

    private static readonly (double Start, double End, double Radius)[] H1Curves =
    {
        (471, 592, 375), (1048, 1265, 600), (1575, 1816, 2000), (2514, 2746, 3000),
        (3517, 3751, 600), (3889, 3989, 600), (4116, 4189, 700), (4552, 4614, 650),
        (4787, 5081, 350), (5302, 5667, 350), (6203, 6600, 500), (6842, 6888, 1000),
        (6994, 7040, 1000), (7269, 7359, 1000), (7532, 8107, 400), (8471, 8821, 400),
        (9256, 9595, 600), (9956, 10066, 1500), (10143, 10209, 2000), (10826, 11143, 375),
        (11707, 11977, 800), (12256, 12368, 500), (13006, 13287, 706), (13605, 13630, 800),
        (13754, 13814, 1300), (14060, 14313, 1530), (15106, 15177, 2000), (15238, 15309, 2000)
    };

    private static readonly (double Start, double End, double Radius)[] H2Curves =
    {
        (441, 576, 375), (1097, 1227, 500), (1555, 1795, 2000), (2513, 2745, 3000),
        (3501, 3717, 563), (3863, 4058, 593), (4528, 4607, 650), (4769, 5070, 350),
        (5276, 5640, 350), (6147, 6544, 500), (6820, 6865, 1000), (6971, 7016, 1000),
        (7246, 7341, 1032), (7522, 8049, 375), (8401, 8717, 375), (9171, 9511, 600),
        (9952, 10017, 2500), (10072, 10138, 2000), (10764, 11099, 391), (11637, 11917, 824),
        (12238, 12357, 516), (12483, 12543, 20700), (12603, 12663, 45150), (12982, 13305, 800),
        (13539, 13585, 1200), (13723, 13769, 1200), (14063, 14311, 1500), (15060, 15130, 2000),
        (15192, 15262, 2000)
    };

    private static readonly (double H1, double H2)[] ParallelPositions =
    {
        (288, 288), (1429, 1407), (1925, 1907), (2626, 2607), (3867, 3834),
        (4313, 4289), (4445, 4421), (5834, 5790), (6111, 6068), (6339, 6308),
        (6552, 6528), (6748, 6726), (6945, 6922), (8287, 8225), (8672, 8596),
        (9123, 9032), (9393, 9311), (9623, 9553), (9852, 9782), (10082, 10011),
        (10299, 10227), (10732, 10661), (10962, 10900), (11190, 11141), (11428, 11379),
        (11662, 11614), (11888, 11845), (12114, 12073), (12285, 12245), (12515, 12481),
        (12730, 12696), (12970, 12936), (13186, 13148), (13480, 13438), (13717, 13674),
        (14230, 14185), (14470, 14424), (14714, 14668)
    };

    /// <summary>Replaces default track geometry with the report's Tables 1, 2, and 5.</summary>
    public static void Apply(SimulationData data)
    {
        data.HatUzunlugu = 15394;

        data.HatEgimleri.Clear();
        AddGrades(data, H1Grades, "H1");
        AddGrades(data, H2Grades, "H2");

        data.HatKurplari.Clear();
        AddCurves(data, H1Curves, "H1");
        AddCurves(data, H2Curves, "H2");

        data.RayParalellemeleri.Clear();
        for (int i = 0; i < ParallelPositions.Length; i++)
        {
            data.RayParalellemeleri.Add(new RayParalellemesi
            {
                No = i + 1,
                H1BaglantiKm = ParallelPositions[i].H1,
                H2BaglantiKm = ParallelPositions[i].H2
            });
        }

        data.HizLimitleri.Clear();
        data.HizLimitleri.Add(new HizLimiti
        {
            Baslangic = 0,
            Bitis = 15394,
            Limit = 80,
            HatTipi = "H1"
        });
        data.HizLimitleri.Add(new HizLimiti
        {
            Baslangic = 0,
            Bitis = 15347,
            Limit = 80,
            HatTipi = "H2"
        });
    }

    private static void AddGrades(
        SimulationData data,
        IEnumerable<(double Start, double End, double Grade)> grades,
        string track)
    {
        foreach (var grade in grades)
        {
            data.HatEgimleri.Add(new HatEgimi
            {
                Baslangic = grade.Start,
                Bitis = grade.End,
                EgimYuzdesi = grade.Grade,
                HatTipi = track
            });
        }
    }

    private static void AddCurves(
        SimulationData data,
        IEnumerable<(double Start, double End, double Radius)> curves,
        string track)
    {
        foreach (var curve in curves)
        {
            data.HatKurplari.Add(new HatKurbu
            {
                Baslangic = curve.Start,
                Bitis = curve.End,
                Yaricap = curve.Radius,
                HatTipi = track,
                IcHat = track,
                Yon = "Belirsiz"
            });
        }
    }
}
