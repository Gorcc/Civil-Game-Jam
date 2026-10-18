public enum TutorialStep { Welcome, Camera, DisasterDamage, Happiness, Budget, Population, Housing, Safety, Maintenance, FireService, Decision, Finish }

/// <summary>Short, action-led onboarding copy. Translations live in the shared language catalog.</summary>
public static class TutorialSteps
{
    public const int Count = 12;
    public static string Title(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.Welcome: return "Sayın Başkan, hoş geldiniz!";
            case TutorialStep.Camera: return "Şehrini tanı";
            case TutorialStep.DisasterDamage: return "Burası bir afet bölgesi";
            case TutorialStep.Happiness: return "Halkın desteğini koru";
            case TutorialStep.Budget: return "Bütçeni yönet";
            case TutorialStep.Housing: return "İlk konutunu inşa et";
            case TutorialStep.Population: return "İnsanlar ve işler";
            case TutorialStep.Safety: return "Güvenli büyü";
            case TutorialStep.Maintenance: return "Küçük ihmalleri önle";
            case TutorialStep.FireService: return "Hizmetin yeri önemli";
            case TutorialStep.Decision: return "İlk başkanlık kararın";
            default: return "Şehir artık sana emanet";
        }
    }
    public static string Body(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.Welcome: return "Afetlere açık bu şehrin yeni seçilmiş belediye başkanısın. Halkın desteğini korurken bütçeyi yönetmeli ve şehri tehlikelere hazırlamalısın.";
            case TutorialStep.Camera: return "WASD ile hareket et, Q/E ile dön ve fare tekerleğiyle yakınlaş. Şehri incelemek için kamerayı biraz hareket ettir.";
            case TutorialStep.DisasterDamage: return "Bu şehir deprem, sel ve yangına açık. Sağdaki yüzdeler afetin gerçekleşme ihtimalini değil, gerçekleştiğinde şehrin ne kadar ağır hasar alabileceğini gösterir. Yüksek değer, daha savunmasız bir şehir demek. Afetlere dayanıklı bir şehir kurmazsan tüm emeğin bir felaketle boşa gidebilir. Deprem, sel veya yangın göstergesine tıkla ve hasar oranının kaynaklarını incele.";
            case TutorialStep.Happiness: return "Halkın memnuniyeti oy oranını gösterir. Bir sonraki seçimde oy oranın %50'nin altındaysa seçimi kaybedersin. Memnuniyet göstergesine tıkla ve hangi etkilerin desteği değiştirdiğini gör.";
            case TutorialStep.Budget: return "Binalar gelir getirir, hizmetlerin günlük giderleri vardır. Bütçe göstergesine tıklayarak gelir ve gider dökümünü aç.";
            case TutorialStep.Housing: return "İşaretli arsaya tıkla ve bir konut inşa et. Apartman nüfus, müstakil dayanıklılık, villa ise yüksek kira ve memnuniyet sağlar. İnşaatın tamamlanmasını bekle.";
            case TutorialStep.Population: return "Nüfus göstergesine tıkla; insanların nerede yaşadığını ve çalıştığını incele. Örneğin fabrika kurduğunda yeterli boşta çalışan yoksa tüm kadrosu dolmaz ve geliri azalır. Yeni konutlar iş gücünü artırır; yeni iş yerleri ise bu çalışanları paylaşır.";
            case TutorialStep.Safety: return "İşaretli binaya tıkla; dayanıklılığı ve güçlendirme seçeneğini incele. Ek katlar kapasiteyi artırır ama dayanıklılığı azaltır. Şimdilik para harcaman gerekmiyor.";
            case TutorialStep.Maintenance: return "Sağdaki bakım uyarısında İncele'ye bas, haritadaki kuru otlara tıkla ve ekip gönder. Temizlik tamamlanınca yangın ve sel hasar oranlarındaki ek yük kalkar.";
            case TutorialStep.FireService: return "Bir itfaiye inşa et. Onaydan önce açılan çember, hangi binalara yardım götürebileceğini gösterir. Toplanma alanları da yalnız yakın çevreye hizmet verir.";
            case TutorialStep.Decision: return "Karar gazetesi iki haftada bir gelir. Gazeteyi aç, üç kararın bütçe ve memnuniyet etkilerini karşılaştırıp birini imzala. Popüler olmak ve afetlere hazırlanmak her zaman aynı seçim değildir.";
            default: return "Haftalık haberler yaptıklarını ve şehrin durumunu özetler. Halkı dinle, bütçeyi izle ve afetlere hazırlan. Başarılar, Sayın Başkan!";
        }
    }
}
