# Playtest Raporu: Kumkent (Claude)

**Sonuç: İlk seçim kaybedildi.** 91. gün, oy oranı %48.4 (ekranda "%50 ile kaybettiniz" yazdı).

| | Başlangıç | En iyi an | Bitiş |
|---|---|---|---|
| Bütçe | ₺1.000.000 | ₺1.617.000 (42. gün) | **−₺4.300** |
| Günlük net | +₺44K | +₺67K (27. gün) | **−₺13.5K** |
| Nüfus | 112 | 124 | **11** |
| Mutluluk (oy) | %55 | hedef 64 (43. gün) | **%48.4** |

Bitiş ekranı: 90 gün görevde · 7 bina inşa · 8 afet · **7 yıkılan bina · 118 can kaybı**.

**Koşullar**
- Menüden "Oyuna Başla" ile girildi; şehir "Kumkent", başkan "Tester Claude".
- Tutorial atlandı, çünkü PlayerPrefs'te daha önce tamamlanmış görünüyordu.
- Oyunun çoğu 4x hızda oynandı. Bütün kararlar bana aitti, test butonlarına basılmadı.
- Bataklık kaldırılmadı.
- Test paneli oyun sırasında ekrandaydı (`showTestButtons` açık).
- **Başlangıç parası ₺1M.** Sahnede `money: 1000000` duruyor; ekonomi düzenlemesindeki ₺600K değil, biri değiştirmiş olabilir.

---

## Zaman çizelgesi

| Gün | Olay / Kararım | Para sonrası |
|---|---|---|
| 1 | Başlangıç: 5 eski konut (R 34–53). Mutluluk hedefi 48 → bir şey yapmazsam seçimi kaybederim. | ₺1.00M |
| 3 | **Mahalle Parkı** (bataklık arsası, ₺102K) + **Müstakil Ev** (₺200K) + 4 katlı konutu **güçlendirme** (₺160K, R 34→49) | ₺626K |
| 11 | 2. **Müstakil Ev** (₺200K) + 2. 4 katlı konutu güçlendirme (₺160K) + 2 bakım işi | ₺616K |
| 14 | 1. gazete: **Menfez Temizliği** (₺65K, sel −1 gün) | ₺904K |
| 22 | Kıyı arsası (₺300K) + **Plaj Tesisi** (₺440K) → net +₺67K/gün, hedef mutluluk 58 | ₺364K |
| 28 | Gazete: **Rezerv Arazi Satışı** (+₺120K, −2 mutluluk) | ₺883K |
| 29 | **Deprem M5.1**: 8 hasar, 0 yıkım, 3 can | – |
| 38 | Tüm hasarları onar (~₺24K) + 2 katlı iki konutu güçlendir (₺80K ×2) | ₺1.35M |
| 42 | **Fırtına** + sel: 5 hasar, 1 can. Gazete: Toplanma Alanı Donatımı (₺40K) | – |
| 42 | Arsa (₺200K) + **Afet Araştırma Merkezi** (₺1.2M) → hedef mutluluk 64 | ₺177K |
| 47 | **Deprem M5.6**: bataklıktaki güçlendirilmiş konut yıkıldı → **18 can** | ₺293K |
| **52** | **DEPREM M6.8: 4 konut + Plaj Tesisi yıkıldı → 91 can**, mutluluk −24. Nüfus 102 → 11 | ₺553K |
| 53 | Hayatta kalanları onar (~₺66K; araştırma merkezi onarımı tek başına ₺50K) | ₺473K |
| 56 | 2 Müstakil Ev (₺400K). Gazete: **Kemer Sıkma** (+₺100K) — iflası önlemek için | ₺139K |
| ~60 | **Deprem M5.1** (0 kayıp) + **Yangın**: 1 müstakil ev yıkıldı, 5 can | – |
| 70 | Fırtına. Gazete: **Yapı Denetimlerini Erteleme** (+₺60K) | ₺175K |
| 84 | Gazete: **Mahalle Parkı Geliştirme** (₺80K, +4) — seçim öncesi oy için | ₺44K |
| 90–91 | **Yangın** (itfaiye kapsamı dışında): 1 müstakil daha gitti. Bütçe eksiye düştü. **Seçim kaybedildi.** | −₺4K |

**İnşa ettiklerim (7):**
- Mahalle Parkı
- Müstakil Ev ×4 (biri ikinci dalgada)
- Plaj Tesisi
- Afet Araştırma Merkezi

**Yıkılanlar (7):**
- Eski konutların hepsi (5 adet)
- Plaj Tesisi
- Yangında 2 müstakil ev

**Sağ kalanlar:**
- 2 müstakil ev
- Mahalle Parkı
- Araştırma Merkezi (yarım personelle)

---

## Bulgular (önem sırasıyla)

### 1. Büyük deprem başlangıç şehrini kurtarılamaz biçimde siliyor (kritik)
- 52. gündeki M6.8, nüfusun **~%75'ini** tek seferde öldürdü (91 / 121).
- Eski 4 katlı konutlara 1 kademe güçlendirme yaptırdım (R 49–51), yine de çöktüler.
- Kurallara göre (`DisasterRules`) M6.8'de güvenle ayakta kalmak için R ≈ 60–70 gerekiyor; bu da **2 kademe = 4 katlı bina başına ₺320K** demek.
- 5 bina için ₺1M+ gerekiyor, başlangıç parası ise ₺1M. İlk 50 günde hem şehri büyütüp hem bu kadar güçlendirme yapmak mümkün değil.
- **Öneri:**
  - Ya ilk büyük depremi daha geç getirin (ilk seçimden sonra),
  - ya güçlendirmenin etkisini kademe başına +15'ten +20–25'e çıkarın,
  - ya da başlangıç binalarını biraz daha sağlam yapın.

### 2. Afetten sonra yoksulluk tuzağı (kritik)
- Sakinler ölünce kira geliri kayboluyor ama bakım giderleri sürüyor:
  - Araştırma merkezi −₺10K/gün
  - Park −₺5K/gün
- Net gelir eksiye düştü. ₺200K'lık bir evi bile alacak para hiç birikmedi; 91. günde bütçe eksiye indi.
- Para kazanmanın tek yolu gazetedeki "kasaya para" kararları oldu.
- **Öneri:**
  - Afet sonrası devlet yardımı ya da kredi.
  - Personelsiz binaların bakım giderinin düşmesi.
  - "Binayı kapat / mothball" seçeneği.

### 3. Deprem uyarısını kaçırdım (yüksek)
- Büyük deprem planlıydı; uyarı büyük ihtimalle 21–41. günler arasında TV'de verildi.
- 4x hızda oynarken haber TV'si ve kayan yazı gelip geçiyor, kalıcı bir gösterge yok.
- **Öneri:** HUD'da kalıcı bir **"DEPREM UYARISI · tahmini N gün"** rozeti. Uyarı gelince oyun bir kez otomatik dursun.

### 4. Gazete kararlarındaki "risk" değişimleri göstermelik (yüksek, istismar)
- `earthquake/flood/fireRiskDelta` sadece HUD'daki "Afet Hasar Riski" yüzdesini değiştiriyor (`CityRisk.cs`). Gerçek deprem, yangın veya sel hasarına hiç etkisi yok.
- Bu yüzden:
  - **"Yapı Denetimlerini Erteleme"** (+₺60K, +1 mutluluk) bedava para.
  - **"Kemer Sıkma"** (+₺100K) neredeyse cezasız.
  - **"Bina Güçlendirme Hibesi"** (₺120K, −4 mutluluk) hiçbir işe yaramıyor.
- Gerçek etkisi olan tek karar **Menfez Temizliği**; onu da FloodController kimliğine bakarak kontrol ediyor.
- **Öneri:** risk değişimlerini gerçek çarpanlara bağlayın: deprem hasarı ×, yangın yayılma şansı ×, sel günü ±.

### 5. Güçlendirme sürerken onarım yapılamıyor (orta)
- Güçlendirme 3 gün sürüyor ve o sırada onarım kilitli.
- Sonra onarmayı unuttum; hasarlı kalan bataklıktaki konut M5.6'da çöktü (18 can).
- **Öneri:** güçlendirme mevcut hasarı da onarsın, ya da bina kartında "Hasarlı – onar" uyarısı belirgin olsun.

### 6. Bataklık zemini çok cezalı (orta)
- 2x deprem çarpanı ile −10 dayanıklılık birleşince güçlendirilmiş 2 katlı konut bile orta bir depremde (M5.6) çöktü.
- Arsa %50 ucuz olsa da üstüne konut yapmak tuzak; mantıklı tek kullanım park.
- Oyuncu bunu ancak acı tecrübeyle öğreniyor.

### 7. Mutluluk sistemi (orta)
- Mutluluk her gün hedefe olan farkın %12'si kadar yaklaşıyor. Bu yüzden:
  - Afetin anlık cezası (−24) yaklaşık 10 günde siliniyor.
  - Gazetedeki "+4 memnuniyet" de aynı hızla kayboluyor.
  - Kararlar fiilen sadece para üzerine.
  - Seçimi belirleyen şey son 1–2 haftada ne olduğu.
- **Bakım işleri sürekli çıkıyor** (3–5 günde bir), her biri −0.6/gün mutluluk.
  - 4x hızda köstebek oyunu gibi.
  - Seçimden hemen önce birikip yangınla birleşince oyu 50'nin altına itti.
- **Öneri:**
  - Bakım işleri daha seyrek ama daha anlamlı olsun.
  - "Otomatik bakım ekibi" araştırması ya da binası eklenebilir.

### 8. Araştırma Merkezi pahalı ve kırılgan (orta)
- ₺1.2M + ₺200K arsaya kurdum; +6 mutluluk verdi.
- Hiç araştırma başlatamadım: araştırmalar ₺450–600K, depremden sonra para kalmadı.
- 25 çalışan istiyor. Nüfus 18'e düşünce %72 personelle kaldı, ama bakım parası tam kesilmeye devam etti.
- Onarımı da çok pahalı: 14 hasar için ₺50K. Formül bina maliyetine bağlı.

### 9. Gelir dengesi (bilgi)

| Bina | Maliyet | Net/gün | Geri ödeme |
|---|---|---|---|
| Plaj Tesisi | ₺400K (+₺300K arsa) | +₺16K | **25 gün** (+3 mutluluk) |
| Fabrika | ₺700K | +₺20K | 35 gün (−3 mutluluk, 40 işçi) |
| Müstakil Ev | ₺200K | +₺4.2K | 48 gün (+2 mutluluk) |
| Lüks Villa | ₺600K | +₺12K | 50 gün |
| Konut (2 kat) | ₺400K | +₺6K | **67 gün** |

- Konut seçim döngüsü (90 gün) içinde zor amorti ediyor. Plaj Tesisi açık ara en iyisi ama sadece 8 kıyı arsası var.
- Başlangıçta para kazanmak kolay (+₺44–67K/gün). Afetten sonra ise imkânsızlaşıyor.

### 10. Yangın acımasız (orta)
- İtfaiye kapsamı dışındaki iki yangın birer müstakil ev yıktı. Yakında toplanma alanı yoksa içindekilerin hepsi ölüyor.
- İtfaiye ₺500K + ₺14K/gün; bu ekonomide ancak çok erken kurulursa olur.
- İkinci yangın tam seçim günü geldi ve sonucu belirledi.

### 11. Küçük UI / metin sorunları
- **Seçim yenilgi ekranı "%50 oyla kaybettiniz" diyor.** Kazanma şartı ≥%50, gerçek oy %48.4. Ondalık gösterilmeli ya da aşağı yuvarlanmalı.
- Fırtına ile gazete aynı anda açıldı. Gazete oyunu durdurduğu için fırtına sırasında zaman donuyor; tuhaf ama zararsız.
- Gazete 14 günde bir geliyor. 7. gün beklerken gelmeyince hata sandım. Haftalık mı, iki haftalık mı, belli olmalı.
- Rapor kartı yığını iyi çalıştı. Ama 8 rapor birikince "+5 rapor sırada" çıktı, hepsini kapatmak uğraştırıcı. Bir "hepsini okundu say" butonu iyi olur.

---

## Ne iyi çalıştı
- İlk 40 gün tatmin edici: park, ev ve plaj kurup gelirin ve mutluluğun yükseldiğini görmek net ve hızlı.
- İnşaat ve onarım tepkileri anlık, raporlar okunaklı.
- Sel kısa ve yönetilebilir oldu; menfez kararı fark yarattı.
- Plaj tesisi + müstakil ev erken oyunda iyi bir kombinasyon.

## En önemli 3 öneri
1. **İlk büyük depremi ilk seçimden sonraya çekin** ya da güçlendirmeyi güçlendirin. Şu an kaçınılmaz bir katliam.
2. **Afet sonrası toparlanma mekanizması:** yardım fonu, kredi, personelsiz binaya düşük bakım.
3. **Gazete risk değerlerini gerçek etkiye bağlayın.** Şu an "riski artır, para al" bedava.
