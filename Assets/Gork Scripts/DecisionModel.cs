using System;
using System.Collections.Generic;
using UnityEngine;

public enum DecisionCategory
{
    MunicipalServices,        // 1. Belediye Hizmetleri (Günlük Yaşam & Popülarite)
    DisasterPreparedness,     // 2. Afet Hazırlığı & Risk Azaltma
    ControversialPolitics     // 3. Tartışmalı & Siyasi Kararlar (Yüksek Risk / Yüksek Ödül)
}

[Serializable]
public class CityDecision
{
    public string id;
    public string title;
    public string titleEn;
    public DecisionCategory category;
    [TextArea(2, 4)]
    public string description;

    // Etkiler (Bütçe değerleri 100 ile çarpılmıştır)
    public long budgetDelta;          // Anlık bütçe değişimi (TL)
    public float popularityDelta;     // Memnuniyet / Oy oranı değişimi (+5, -3)
    public float earthquakeRiskDelta;
    public float floodRiskDelta;
    public float fireRiskDelta;
    public int dailyIncomeDelta;      // Günlük gelir değişimi (TL/gün)
    public int durationDays;          // Geçici etki süresi (0 = kalıcı, 3 = 3 gün)
    public int populationDelta;       // Nüfus artışı
    public string specialEffectText;  // Özel etki metni (Örn: "Yaralanma Oranı: -%15")
    public IconType icon;

    public CityDecision(string id, string title, string titleEn, DecisionCategory category, string description,
        long budgetDelta, float popularityDelta, int dailyIncomeDelta = 0,
        int durationDays = 0, int populationDelta = 0, string specialEffectText = "", IconType icon = IconType.House,
        float earthquakeRiskDelta = 0f, float floodRiskDelta = 0f, float fireRiskDelta = 0f)
    {
        this.id = id;
        this.title = title;
        this.titleEn = titleEn;
        this.category = category;
        this.description = description;
        this.budgetDelta = budgetDelta;
        this.popularityDelta = popularityDelta;
        this.earthquakeRiskDelta = earthquakeRiskDelta;
        this.floodRiskDelta = floodRiskDelta;
        this.fireRiskDelta = fireRiskDelta;
        this.dailyIncomeDelta = dailyIncomeDelta;
        this.durationDays = durationDays;
        this.populationDelta = populationDelta;
        this.specialEffectText = specialEffectText;
        this.icon = icon;
    }
}

/// <summary>
/// Tüm haftalık karar ve politika kartlarının veri tabanı.
/// Bütçe değerlerinin tamamı kullanıcının isteği doğrultusunda 100 ile çarpılmıştır.
/// </summary>
public static class DecisionDatabase
{
    public static readonly List<CityDecision> AllDecisions = new List<CityDecision>
    {
        // =========================================================================
        // 1. BELEDİYE HİZMETLERİ (MUNICIPAL SERVICES - DAILY LIFE & POPULARITY)
        // =========================================================================
        new CityDecision(
            "asphalt_renovation",
            "Asfalt Yenileme Çalışması",
            "Asphalt Renovation",
            DecisionCategory.MunicipalServices,
            "Ana arterlerdeki ve mahalle yollarındaki derin çukurları onararak vatandaşların günlük ulaşım konforunu ve memnuniyetini artırır.",
            budgetDelta: -100_000L,
            popularityDelta: +3f,
            icon: IconType.Hammer
        ),

        new CityDecision(
            "neighborhood_park",
            "Mahalle Parkı Geliştirme",
            "Neighborhood Park Development",
            DecisionCategory.MunicipalServices,
            "Mahalle aralarına banklar, yürüyüş parkurları ve çocuk oyun alanları içeren yeşil parklar inşa ederek yaşam kalitesini yükseltir.",
            budgetDelta: -80_000L,
            popularityDelta: +4f,
            icon: IconType.House
        ),

        new CityDecision(
            "city_cleanliness",
            "Şehir Çapında Temizlik Seferberliği",
            "City-Wide Cleanliness Campaign",
            DecisionCategory.MunicipalServices,
            "Temizlik ekiplerini seferber ederek cadde ve meydanları baştan aşağı yıkar, çöp toplama sıklığını iki katına çıkarır.",
            budgetDelta: -50_000L,
            popularityDelta: +2f,
            icon: IconType.Trash
        ),

        new CityDecision(
            "street_lighting",
            "Sokak Aydınlatması Modernizasyonu",
            "Street Lighting Upgrade",
            DecisionCategory.MunicipalServices,
            "Yerleşim alanlarına modern ve enerji tasarruflu LED sokak lambaları takarak gece güvenliğini artırır, suç oranını düşürür.",
            budgetDelta: -70_000L,
            popularityDelta: +3f,
            icon: IconType.Shield
        ),

        new CityDecision(
            "library_expansion",
            "Halk Kütüphanesi Genişletmesi",
            "Public Library Expansion",
            DecisionCategory.MunicipalServices,
            "Şehir kütüphanesinin kitap koleksiyonlarını günceller, gençlere 7/24 modern etüt salonları ve kafeterya geliri açar.",
            budgetDelta: -90_000L,
            popularityDelta: +2f,
            dailyIncomeDelta: +2_000,
            specialEffectText: "Günlük Gelir: +₺2.000",
            icon: IconType.House
        ),

        new CityDecision(
            "cultural_festival",
            "Toplumsal Kültür Festivali",
            "Community Cultural Festival",
            DecisionCategory.MunicipalServices,
            "Hafta sonu müzik, sanat ve yöresel lezzet festivali düzenleyerek hem halkın moralini hem de yerel çarşı esnafını canlandırır.",
            budgetDelta: -120_000L,
            popularityDelta: +5f,
            dailyIncomeDelta: +5_000,
            durationDays: 3,
            specialEffectText: "3 Gün Boyunca Günlük Gelir +₺5.000",
            icon: IconType.Tent
        ),

        new CityDecision(
            "public_wifi",
            "Ücretsiz Şehir Wi-Fi Ağı",
            "Public Wi-Fi Expansion",
            DecisionCategory.MunicipalServices,
            "Şehrin ana meydanları, parkları ve otobüs duraklarında halkın ücretsiz bağlanabileceği yüksek hızlı kablosuz internet noktaları kurar.",
            budgetDelta: -60_000L,
            popularityDelta: +2f,
            icon: IconType.Wave
        ),

        new CityDecision(
            "stray_animal_care",
            "Sokak Hayvanlarını Koruma Girişimi",
            "Stray Animal Care Initiative",
            DecisionCategory.MunicipalServices,
            "Sokaktaki can dostlarımız için modern barınaklar, otomatik mama/su istasyonları ve ücretsiz veteriner bakım merkezleri inşa eder.",
            budgetDelta: -50_000L,
            popularityDelta: +3f,
            icon: IconType.Heart
        ),

        // =========================================================================
        // 2. AFET HAZIRLIĞI & RİSK AZALTMA (DISASTER PREPAREDNESS & MITIGATION)
        // =========================================================================
        new CityDecision(
            "emergency_backpack",
            "Acil Durum Afet Çantası Zorunluluğu",
            "Emergency Backpack Mandate",
            DecisionCategory.DisasterPreparedness,
            "Tüm hanelerin deprem/afet çantası hazırlamasını zorunlu kılar. Vatandaşlar bu ekstra harcama ve denetimden yakınır.",
            budgetDelta: -150_000L,
            popularityDelta: -3f,
            earthquakeRiskDelta: -3f,
            specialEffectText: "Yaralanma Oranı: -%15 | Afet Riski: -%3",
            icon: IconType.Warning
        ),

        new CityDecision(
            "evacuation_drill",
            "Zorunlu Şehir Tahliye Tatbikatı",
            "Mandatory Evacuation Drill",
            DecisionCategory.DisasterPreparedness,
            "Şehir genelinde sirenlerle tahliye tatbikatı düzenler. Yerel esnaf ve fabrikalar kayıp çalışma saatlerinden şikayet eder.",
            budgetDelta: -60_000L,
            popularityDelta: -2f,
            specialEffectText: "Tahliye Hızı: +%25",
            icon: IconType.People
        ),

        new CityDecision(
            "structural_retrofitting",
            "Bina Güçlendirme Hibesi",
            "Structural Retrofitting Subsidy",
            DecisionCategory.DisasterPreparedness,
            "Dayanıksız ve eski binaların temel/kolon güçlendirmesini yapan bina sahiplerine belediye bütçesinden doğrudan hibe desteği sunar.",
            budgetDelta: -120_000L,
            popularityDelta: -4f,
            earthquakeRiskDelta: -8f,
            specialEffectText: "Afet Riski: -%8 | Temel Direnci Artar",
            icon: IconType.Ground
        ),

        new CityDecision(
            "fire_shift_expansion",
            "İtfaiye Vardiya & Nöbet Artırımı",
            "Fire Department Shift Expansion",
            DecisionCategory.DisasterPreparedness,
            "İtfaiye personeli sayısını ve nöbetçi vardiyalarını artırır, fazla mesai ücretlerini karşılayarak yangınlara yıldırım müdahale sağlar.",
            budgetDelta: -90_000L,
            popularityDelta: 0f,
            dailyIncomeDelta: -5_000,
            specialEffectText: "Yangına Müdahale Hızı: +%25 | Günlük Gider: -₺5.000",
            fireRiskDelta: -5f,
            icon: IconType.Flame
        ),

        new CityDecision(
            "storm_drain_cleaning",
            "Yağmur Suyu & Menfez Temizliği",
            "Storm Drain & Drainage Cleaning",
            DecisionCategory.DisasterPreparedness,
            "Kuvvetli yağış sezonu öncesi tüm dere yatakları, drenaj kanalları ve mazgallardaki birikintileri temizleyerek taşkınları önler.",
            budgetDelta: -65_000L,
            popularityDelta: +1f,
            floodRiskDelta: -2f,
            specialEffectText: "Sel & Su Baskını Hasarı: -%20 | Afet Riski: -%2",
            icon: IconType.Wave
        ),

        new CityDecision(
            "fire_extinguisher_dist",
            "Yangın Tüpü & Dedektör Dağıtımı",
            "Fire Extinguisher Distribution Campaign",
            DecisionCategory.DisasterPreparedness,
            "Şehirdeki tüm apartman bloklarına onaylı yangın söndürme tüpleri ve duman sensörleri ücretsiz olarak dağıtılır.",
            budgetDelta: -80_000L,
            popularityDelta: +1f,
            specialEffectText: "Yangın Hasarı: -%30",
            fireRiskDelta: -6f,
            icon: IconType.Flame
        ),

        new CityDecision(
            "assembly_signage",
            "Acil Toplanma Alanı Donatımı",
            "Emergency Assembly Point Signage",
            DecisionCategory.DisasterPreparedness,
            "Boş arazileri temizleyip aydınlatma, su deposu ve gece parlayan tabelalarla resmi ve güvenli toplanma alanları haline getirir.",
            budgetDelta: -40_000L,
            popularityDelta: +1f,
            specialEffectText: "Yaralanma Oranı: -%10",
            icon: IconType.Tent
        ),

        new CityDecision(
            "geological_survey",
            "Jeolojik Zemin Etüdü ve Fay Taraması",
            "Geological Soil Survey",
            DecisionCategory.DisasterPreparedness,
            "Jeoloji mühendislerini görevlendirerek şehirdeki tüm fay kırıklarını ve zemin sıvılaşma risklerini parsel parsel haritalandırır.",
            budgetDelta: -100_000L,
            popularityDelta: 0f,
            specialEffectText: "Tüm Arsalardaki Gizli Zemin Risklerini Açar",
            icon: IconType.Ground
        ),

        // =========================================================================
        // 3. TARTIŞMALI & SİYASİ KARARLAR (CONTROVERSIAL & POLITICAL CHOICES)
        // =========================================================================
        new CityDecision(
            "urban_zoning_amnesty",
            "İmar Barışı ve Kaçak Yapı Affı",
            "Urban Zoning Amnesty (İmar Affı)",
            DecisionCategory.ControversialPolitics,
            "Ruhsatsız ve kaçak katları ağır harç bedelleri karşılığında affeder. Belediye kasası hızla dolar, halk sevinir ancak deprem riski fırlar!",
            budgetDelta: +150_000L,
            popularityDelta: +5f,
            earthquakeRiskDelta: +12f,
            specialEffectText: "Afet Riski: +%12 | Bina Kırılganlığı Ciddi Artar",
            icon: IconType.Warning
        ),

        new CityDecision(
            "emergency_tax_cut",
            "Acil Belediye Vergi İndirimi",
            "Emergency Tax Cut",
            DecisionCategory.ControversialPolitics,
            "Vatandaşları ve tüccarları yanına çekmek için yerel vergi ve harçları geçici olarak indirir; belediyenin günlük bütçe akışı azalır.",
            budgetDelta: 0L,
            popularityDelta: +4f,
            dailyIncomeDelta: -10_000,
            specialEffectText: "Günlük Belediye Geliri: -₺10.000",
            icon: IconType.Coin
        ),

        new CityDecision(
            "rapid_housing_incentive",
            "Hızlı Konut İnşaat Teşviki",
            "Rapid Housing Incentive",
            DecisionCategory.ControversialPolitics,
            "Nüfusu ve kira gelirlerini hızla katlamak için yüksek yoğunluklu konut projelerine bürokratik güvenlik denetimlerini esneterek izin verir.",
            budgetDelta: -50_000L,
            popularityDelta: 0f,
            earthquakeRiskDelta: +6f,
            dailyIncomeDelta: +12_000,
            populationDelta: +15,
            specialEffectText: "Nüfus: +15 | Günlük Gelir: +₺12.000 | Afet Riski: +%6",
            icon: IconType.House
        ),

        new CityDecision(
            "municipal_austerity",
            "Belediye Tasarruf & Kemer Sıkma Paketi",
            "Municipal Austerity Package",
            DecisionCategory.ControversialPolitics,
            "Kamu hizmeti ve altyapı bakım harcamalarını acımasızca kısarak kasaya hemen nakit kazandırır; halkı ve personeli öfkelendirir.",
            budgetDelta: +100_000L,
            popularityDelta: -5f,
            earthquakeRiskDelta: +2f,
            floodRiskDelta: +2f,
            fireRiskDelta: +2f,
            specialEffectText: "Hizmet Verimi: -%20 (3 Gün) | Afet Riski: +%2",
            icon: IconType.Coin
        ),

        new CityDecision(
            "merchant_support",
            "Yerel Esnaf Can Suyu Destek Paketi",
            "Merchant Support Package",
            DecisionCategory.ControversialPolitics,
            "Ekonomik durgunluk yaşayan çarşı ve dükkan sahiplerine belediye kasasından doğrudan can suyu nakit hibesi dağıtır.",
            budgetDelta: -70_000L,
            popularityDelta: +3f,
            dailyIncomeDelta: -5_000,
            durationDays: 3,
            specialEffectText: "3 Gün Boyunca Günlük Gelir -₺5.000",
            icon: IconType.Heart
        ),

        new CityDecision(
            "billboard_campaign",
            "Acil Reklam ve Billboard Kampanyası",
            "Emergency Billboard Campaign",
            DecisionCategory.ControversialPolitics,
            "Şehrin dört bir yanına belediye başkanlığının başarısını ve sözde kusursuz güvenlik önlemlerini öven dev propaganda afişleri astırır.",
            budgetDelta: -90_000L,
            popularityDelta: +5f,
            icon: IconType.ArrowUp
        ),

        new CityDecision(
            "postpone_inspections",
            "Yapı Denetimlerini Erteleme",
            "Postpone Infrastructure Inspections",
            DecisionCategory.ControversialPolitics,
            "Bu haftaki harcamaları kısmak adına eski ve şüpheli binaların zorunlu statik ve beton denetimlerini belirsiz bir tarihe erteler.",
            budgetDelta: +60_000L,
            popularityDelta: +1f,
            earthquakeRiskDelta: +5f,
            specialEffectText: "Bina Aşınma Hızı Artar | Afet Riski: +%5",
            icon: IconType.Warning
        ),

        new CityDecision(
            "sell_reserve_land",
            "Belediye Rezerv Arazisi Satışı",
            "Sell Municipal Reserve Land",
            DecisionCategory.ControversialPolitics,
            "Gelecekte park veya toplanma alanı olabilecek kamuya ait boş bir rezerv arsayı acil nakit için özel müteahhitlere satar.",
            budgetDelta: +120_000L,
            popularityDelta: -2f,
            specialEffectText: "Gelecekteki Genişleme Alanı: -1",
            icon: IconType.Ground
        ),

        new CityDecision(
            "high_density_waiver",
            "Kat Artırımı ve Yoğunluk İzni",
            "High-Density Permit Waiver",
            DecisionCategory.ControversialPolitics,
            "Müteahhitlerin güvenlik incelemelerine takılmadan binalara ekstra kat çıkmalarına belediye ruhsat geliri karşılığı izin verir.",
            budgetDelta: +80_000L,
            popularityDelta: +2f,
            earthquakeRiskDelta: +7f,
            populationDelta: +10,
            specialEffectText: "Nüfus: +10 | Afet Riski: +%7",
            icon: IconType.Hammer
        )
    };

    public static string CategoryName(DecisionCategory category)
    {
        switch (category)
        {
            case DecisionCategory.MunicipalServices: return "BELEDİYE HİZMETLERİ";
            case DecisionCategory.DisasterPreparedness: return "AFET HAZIRLIĞI & ÖNLEM";
            case DecisionCategory.ControversialPolitics: return "TARTIŞMALI & RİSKLİ KARARLAR";
            default: return "POLİTİKA";
        }
    }

    public static Color CategoryColor(DecisionCategory category)
    {
        switch (category)
        {
            case DecisionCategory.MunicipalServices: return new Color(0.12f, 0.58f, 0.45f); // Teal / Emerald
            case DecisionCategory.DisasterPreparedness: return new Color(0.2f, 0.45f, 0.75f); // Steel Blue
            case DecisionCategory.ControversialPolitics: return new Color(0.85f, 0.28f, 0.24f); // Crimson / Tomato
            default: return UIKit.Ink;
        }
    }
}
