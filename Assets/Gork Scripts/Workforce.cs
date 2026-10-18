using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Kullanılabilir nüfus. Every resident of a home can fill one job; workplaces (factory, beach resort,
/// university, hospital, fire station) hire from the nearest homes, essential services first.
/// Understaffed workplaces earn less; a hospital or fire station below half staff stops working.
/// </summary>
public class Workforce : MonoBehaviour
{
    public static Workforce Instance { get; private set; }

    public struct Flow
    {
        public Building home, work;
        public int count;
    }

    /// <summary>Residents who could work.</summary>
    public int Workers { get; private set; }
    public int Employed { get; private set; }
    public int Jobs { get; private set; }
    /// <summary>Kullanılabilir nüfus: residents without a job.</summary>
    public int Available => Workers - Employed;
    /// <summary>Open positions nobody fills.</summary>
    public int Missing => Jobs - Employed;
    public readonly List<Flow> Flows = new List<Flow>();

    float timer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<Workforce>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("Workforce")).AddComponent<Workforce>();
    }

    void Awake()
    {
        Instance = this;
        Recalculate();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = 0.5f;
        Recalculate();
    }

    // ---------- Allocation ----------

    static bool Standing(Building b) => b != null && b.definition != null && b.Plot != null
        && b.Plot.building == b && !b.Plot.demolishing;

    static int Priority(Building b)
    {
        switch (b.definition.service)
        {
            case ServiceType.Hospital: return 0;
            case ServiceType.FireStation: return 1;
            default: return 2;
        }
    }

    public void Recalculate()
    {
        Flows.Clear();
        var all = FindObjectsByType<Building>().Where(Standing).ToList();
        var homes = all.Where(b => b.definition.service == ServiceType.Housing && b.residents > 0 && !b.burning).ToList();
        var workplaces = all.Where(b => b.definition.workersNeeded > 0)
            .OrderBy(Priority).ThenByDescending(b => b.definition.flatIncome).ThenBy(b => b.GetInstanceID()).ToList();

        var free = new Dictionary<Building, int>();
        int workers = 0;
        foreach (var h in homes)
        {
            free[h] = h.residents;
            workers += h.residents;
        }

        int employed = 0, jobs = 0;
        foreach (var w in workplaces)
        {
            w.workers = 0;
            if (w.burning) continue;   // a burning workplace is closed
            int need = w.definition.workersNeeded;
            jobs += need;
            Vector3 p = w.transform.position;
            foreach (var h in homes.OrderBy(h => (h.transform.position - p).sqrMagnitude))
            {
                if (need <= 0) break;
                int take = Mathf.Min(need, free[h]);
                if (take <= 0) continue;
                free[h] -= take;
                need -= take;
                w.workers += take;
                Flows.Add(new Flow { home = h, work = w, count = take });
            }
            employed += w.workers;
        }

        Workers = workers;
        Employed = employed;
        Jobs = jobs;
    }
}
