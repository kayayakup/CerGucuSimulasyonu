# Cer Gücü Simülasyonu - Teknik Denetim Raporu

## Kapsam ve sonuç

Bu çalışma, simülasyonun tren hareketi ve fren kuvveti hesabındaki belirgin hata kaynaklarını düzeltti; senaryo koşusu, canlı özetler, seksiyon göstergeleri, raporlama ve uygunluk etiketlerini bu davranışla tutarlı hale getirdi. Kaynak dokümanlardan alınan parametre değerleri değiştirilmedi.

Doğru bir elektriksel düğüm çözümü ve ray-toprak/VLD hesabı için gerekli bağlantı ve ölçüm verileri mevcut olmadığından bu özellikler sonuç üretiyormuş gibi gösterilmemektedir. Elektrik ağı halen yaklaşık, iletkenlik-ağırlıklı besleme yöntemini kullanır.

## Düzeltilen hesaplar

### Tren hareketi, cer ve fren

Hareket direnci; Davis, eğim ve kurp bileşenlerinden oluşur. Kullanılan etkin kütle döner kütleyi içerir. İstenen net cer kuvveti:

`F_gerekli = m_etkin × a_istenen + R_toplam`

Pozitif sonuç cer motoru kuvveti, negatif sonuç fren kuvveti olarak ayrılır. Bu nedenle hız hedefi korunurken trenin hareket direncini karşılamak için gereken cer kuvveti artık sıfırlanmaz.

Elektrik çözümünden sonra hareket, gerçekten kullanılabilen kuvvetlerle güncellenir:

`a_gerçek = (F_cer - F_rejeneratif - F_mekanik - R_toplam) / m_etkin`

İstenen fren kuvvetinin rejeneratif fren eğrisi/akım sınırıyla karşılanamayan kısmı mekanik fren kuvveti olarak ayrı yayımlanır. Mekanik fren değeri tren kartına ve kayıt anlık görüntülerine eklendi.

### Senaryolar ve zaman

- Sefer aralığı artık senaryo tanımından alınır; global işletme aralığı senaryo koşusunu ezmez.
- Koşu son adımı kalan süreye göre kısaltılır; örneğin 0,6 s adımla 1 s’lik koşu tam 1,0 s’de biter.
- Motor ve kayıt API’leri sıfır, negatif veya sonlu olmayan zaman adımlarını/sürelerini açıkça reddeder.
- Senaryo başlangıcında trenlerin hız, kuvvet, güç ve enerji durumu temizlenir; önceki canlı koşudan kalan değerler senaryoya taşınmaz.
- Tek TM arıza senaryoları mevcut trafo merkezi listesinden türetilir; TM-7 de otomatik dahil olur.

### Göstergeler ve kayıt

- Genel cer/rejeneratif güç toplamları yardımcı yükten ayrı motor gücünü; pantograf net güç toplamı yardımcı yük dahil tren pantograf değerlerini gösterir.
- Gerilim ortalaması ve minimumu yalnızca seferde ve gerilim bulunan trenler üzerinden hesaplanır. Bekleyen trenler minimum gerilimi yapay olarak düşürmez.
- Seksiyon değerleri, tanımlı konum aralığındaki trenlerin pantograf net güç/akım cebirsel toplamı ve ortalama gerilimidir. Aralık başlangıcı dahil, bitişi hariçtir (hat sonu dahildir). Bunlar gerçek seksiyon fider akımı değildir; UI bu ayrımı belirtir.
- “En yakın trafo” göstergesi, konumda en yüksek iletkenlik ağırlığına sahip baskın besleyen TM olarak açıklanır.
- Trafo/doğrultucu kapasite metni artık sabit etiket yerine modeldeki değerlerden gelir. Tren konumunun metre-kilometre dönüşümü de gerçek bir hesaplanan binding kullanır.
- Tüketim “pantograf net tüketimi”, frenleme enerjisi ise “tren rejeneratif üretimi” olarak etiketlenir. Bu üretimin başka trenlerce veya AC şebekesince kullanıldığı kanıtlanmış sayılmaz.

## Uygunluk değerlendirmesi

EN 50163 için yalnızca minimum gerilime bakarak PASS/FAIL vermek kaldırıldı. Baskı, madde, eşik gerilim zaman koşulları ve proje uygulama koşulları doğrulanmadığı sürece sonuç REVIEW’dur. Trafo Duty Class ve proje limitleri eksik olan değerlendirmeler de otomatik uygunluk iddiası üretmez.

## Kaynak verileri ve korunmuş değerler

Bu değişiklikte `SimulasyonVerileri.json` ve dokümanlardan alınan fiziksel/ekipman parametreleri değiştirilmedi. Kod ve arayüzde kullanılan kaynak değerleri arasında 15 mΩ/km katener, 21 mΩ/km normal ray, 24 mΩ/km sertleştirilmiş ray, 15 mΩ trafo iç direnci, 1620 V yüksüz DC bara, 3300 kVA trafo ve 3000 kW doğrultucu değerleri bulunur. Tren kaynağındaki AW0 152 t, AW3 236 t, döner kütle %8,75, yardımcı güç 300 kW ve %50 kullanım oranı da olduğu gibi korunmuştur.

AC gerilimi, trafo/doğrultucu verimleri, güç faktörü, frekans ve DC bara kapasitansı gibi kodda düzenlenebilir olarak sunulan varsayımlar kaynak dokümanlardan geliyormuş gibi kabul edilmemelidir. JSON’daki sıfır Davis katsayıları da değiştirilmedi; bu durumda Davis hareket direnci hesabı sıfır katkı verir ve doğru araç direnç verileri sağlanmadan sonuçlar bu yönden sınırlıdır.

## Hesaplanmayanlar ve model sınırları

1. **Elektrik ağı:** Gerçek KCL/KVL düğüm çözümü değildir. İletkenlik ağırlıklarına göre besleme bölüşümü yapılır; katener ve ray direnci birleştirilmiş yaklaşık dal olarak kullanılır. Seksiyon ayırıcıları ve ray paralelleme bağlantıları canlı elektrik çözüm topolojisine dahil değildir. Bunları eklemek için besleme bağlantı şeması, her iki hat için seksiyon sınırları ve anahtarlama durumları gerekir.
2. **Ray-toprak ve VLD:** Ray-toprak gerilimi, kaçak akım ve VLD çalışma/trip durumu hesaplanmaz. 150 değerinin birimi/ölçüm koşulu ile ray-toprak bağlantı geometrisi, elektrot/uzak toprak tanımı ve geçerli VLD eşikleri doğrulanmadan sonuç türetilemez. VLD erişilebilir gerilim sınırı pantograf gerilimi olarak yorumlanmamalıdır.
3. **Enerji dengesi:** Diyot doğrultucu AC tarafına rejeneratif güç vermez. Tren rejeneratif üretimi ayrı kaydedilir; diğer trenlerin bunu tüketmesi, DC bara/şebeke kayıpları ve tam kaynak-yük enerji dengesi doğrulanmış bir geri kazanım hesabı değildir. DC bara kapasitansı ayarlanabilir model varsayımıdır.
4. **Standart uygunluğu:** Referans baskısı ve proje şartları doğrulanmadığı için ölçümlere dayalı PASS/FAIL iddiası sınırlıdır; rapor değerlendirmeleri REVIEW olarak ele alınmalıdır.

## Doğrulama

Odaklı testler geçersiz zaman adımlarının reddini, tren direnç kuvvetinin sabit hızda karşılanmasını, düşük hızda mekanik freni, seksiyon sınırını, TM-7 senaryosunu, senaryo headway ve tam bitiş zamanını ve doğrulanmamış gerilim kriterinde PASS verilmemesini kapsar.

Çalışma zamanı/build doğrulaması: `dotnet build CerGucuSimulasyonu.csproj --no-restore`  
Test doğrulaması: `dotnet test Tests\CerGucuSimulasyonu.Tests.csproj`
