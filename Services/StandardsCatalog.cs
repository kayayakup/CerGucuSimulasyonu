using System.Collections.Generic;
using CerGucuSimulasyonu.Models;

namespace CerGucuSimulasyonu.Services;

public static class StandardsCatalog
{
    public static IReadOnlyList<StandardReference> CreateDefault()
    {
        return new[]
        {
            new StandardReference(
                "EN 50163",
                "Baski/yil girilecek",
                "Madde girilecek",
                "Minimum hat/tren gerilimi ve esik gerilim sureleri",
                "Minimum tren gerilimi",
                ">= 1000 V veya proje limiti",
                "Kaynak baskisi ve madde numarasi kullanici tarafindan dogrulanmalidir.",
                "MinimumTrainVoltage"),
            new StandardReference(
                "EN 50122-1",
                "Baski/yil girilecek",
                "Madde girilecek",
                "Ray-toprak ve dokunma gerilimi degerlendirmesi",
                "Ray-toprak gerilimi",
                "Proje limitine gore",
                "Bu surumde ray-toprak zaman serisi kaydi icin alan ayrilmistir.",
                "RailEarthVoltage"),
            new StandardReference(
                "EN 50122-2",
                "Baski/yil girilecek",
                "Madde girilecek",
                "Kacak akim degerlendirmesi",
                "Kacak akim",
                "Proje limitine gore",
                "Kacak akim sensorlugu ve limit degeri tanimlanmalidir.",
                "StrayCurrent"),
            new StandardReference(
                "EN 50388",
                "Baski/yil girilecek",
                "Madde girilecek",
                "Kullanilabilir gerilim ve cer gucu sistemi degerlendirmesi",
                "Maksimum trafo yuklenmesi",
                "Ekipman ve proje limitine gore",
                "Trafo/fider limitleri proje tasarim verisi ile eslestirilmelidir.",
                "MaximumSubstationLoad"),
            new StandardReference(
                "EN 50641",
                "Baski/yil girilecek",
                "Madde girilecek",
                "Demiryolu cer gucu simulasyon yazilimi dogrulama baglami",
                "Model dogrulama kaniti",
                "Dokumantasyon ve dogrulama kaydi",
                "Bu uygulama sertifikasyon iddiasi uretmez; kanit alani bilerek REVIEW'dur.",
                "ValidationEvidence")
        };
    }
}
