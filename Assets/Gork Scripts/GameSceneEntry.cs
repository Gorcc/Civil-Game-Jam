using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>RuntimeInitialize runs only for the first scene; also initialize systems when entering from the menu.</summary>
public static class GameSceneEntry
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindAnyObjectByType<CityMap>() == null) return;
        var ui = Object.FindAnyObjectByType<CityUI>();
        var host = ui != null ? ui.gameObject : new GameObject("CityRuntimeSystems");
        Ensure<Workforce>(host);
        Ensure<DisasterManager>(host);
        Ensure<DisasterScheduler>(host);
        Ensure<Research>(host);
        Ensure<CityExpansion>(host);
        Ensure<DayNightCycle>(host);
        Ensure<DrainageNetwork>(host);
        Ensure<ElectionManager>(host);
        Ensure<SwampWorks>(host);
        Ensure<Breakwaters>(host);
        Ensure<LoanOffer>(host);
        var map = Object.FindAnyObjectByType<CityMap>();
        if (Object.FindAnyObjectByType<SeaDressing>() == null) map.gameObject.AddComponent<SeaDressing>();
        var economy = Object.FindAnyObjectByType<CityEconomy>();
        if (economy != null) Ensure<MunicipalEventManager>(economy.gameObject);
    }

    static void Ensure<T>(GameObject host) where T : Component
    {
        if (Object.FindAnyObjectByType<T>() == null) host.AddComponent<T>();
    }
}
