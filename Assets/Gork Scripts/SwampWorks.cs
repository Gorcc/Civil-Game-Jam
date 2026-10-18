using System.Collections;
using UnityEngine;

/// <summary>
/// Reclaiming the swamp. Clicking it offers two expensive projects:
/// a clean wetland park (stays a 4-plot natural area, big happiness bonus) or draining it into four
/// ordinary plots. Either way the swamp is gone, so the plots around it get solid ground again.
/// </summary>
public class SwampWorks : MonoBehaviour
{
    public static SwampWorks Instance { get; private set; }

    [Tooltip("Turn the swamp into a clean wetland park.")]
    public long pondCost = 1_000_000;
    [Tooltip("Drain and fill the swamp into four plots.")]
    public long drainCost = 1_500_000;
    [Tooltip("Target happiness added by each clean wetland park.")]
    public float pondHappiness = 8f;
    [Tooltip("Park width as a share of the block's inner area (inside the sidewalk ring).")]
    public float pondFootprint = 1.01f;
    [Tooltip("Height of the park's ground slab bottom above the block (keeps it clear of the sidewalk ring).")]
    public float groundLift = 0.02f;

    public int Ponds { get; private set; }
    public float HappinessBonus => Ponds * pondHappiness;
    public bool Busy { get; private set; }

    CityMap map;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<SwampWorks>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("SwampWorks")).AddComponent<SwampWorks>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start() => map = FindAnyObjectByType<CityMap>();

    public static District DistrictOf(TerrainFeature f) =>
        f == null ? null : f.district != null ? f.district : f.GetComponentInParent<District>();

    public bool IsReclaimable(TerrainFeature f)
    {
        var d = DistrictOf(f);
        return d != null && d.hasSwamp && !f.cleanPond;
    }

    public bool CanReclaim(TerrainFeature f, bool pond) => !Busy && map != null && IsReclaimable(f)
        && (!pond || map.cleanWetlandPrefab != null)
        && (CityEconomy.Instance == null || CityEconomy.Instance.CanAfford(pond ? pondCost : drainCost));

    public bool Reclaim(TerrainFeature f, bool pond)
    {
        if (!CanReclaim(f, pond)) return false;
        if (CityEconomy.Instance != null
            && !CityEconomy.Instance.TrySpend(pond ? pondCost : drainCost, pond ? "Temiz sulak alan" : "Bataklık kurutma"))
            return false;
        StartCoroutine(pond ? MakePond(f) : Drain(f));
        return true;
    }

    IEnumerator MakePond(TerrainFeature f)
    {
        Busy = true;
        var d = DistrictOf(f);
        var old = d.swamp;
        Vector3 pos = old != null ? old.localPosition : new Vector3(0f, 0.02f, 0f);
        Quaternion rot = map.cleanWetlandPrefab.transform.localRotation;
        // Keep the swamp's quarter turn but use the park model's own import rotation.
        // Older maps put the model directly under the block; newer ones under an unrotated root.
        var oldModel = old == null ? null : old.childCount > 0 && old.GetComponent<Renderer>() == null ? old.GetChild(0) : old;
        if (oldModel != null && map.swampPrefab != null)
            rot = oldModel.localRotation * Quaternion.Inverse(map.swampPrefab.transform.localRotation) * rot;

        yield return ClearSwamp(d);

        // Unrotated root so the park can be stretched along world X/Z to fill the square block exactly.
        var pondGo = new GameObject("CleanWetland");
        pondGo.transform.SetParent(d.transform, false);
        var model = Building.SpawnPrefab(map.cleanWetlandPrefab, pondGo.transform, Vector3.zero, rot);
        model.transform.localScale = map.cleanWetlandPrefab.transform.localScale;
        CityMap.FitToSquare(pondGo.transform, d.transform.position, map.WetlandSize * pondFootprint / 1.01f, groundLift);
        pos = pondGo.transform.localPosition;
        CityMap.SetLayerRecursive(pondGo, 2);
        d.swamp = pondGo.transform;
        d.hasSwamp = false;
        d.hasPond = true;
        Redecorate(d);

        f.title = "Temiz Sulak Alan";
        f.kicker = "DOĞAL PARK";
        f.terrain = PlotTerrain.Normal;
        f.cleanPond = true;
        f.note = "Kuşlar ve nilüferlerle dolu bir doğa parkı. Şehrin en sevilen gezi alanı.";
        f.outlineTarget = pondGo.transform;

        yield return Rise(pondGo.transform, pos);
        Ponds++;
        Finish("DOĞA KAZANDI: Belediye bataklığı temizledi, şehre kuşlarla dolu bir sulak alan parkı kazandırıldı.", 3f, "Temiz sulak alan");
    }

    IEnumerator Drain(TerrainFeature f)
    {
        Busy = true;
        var d = DistrictOf(f);
        yield return ClearSwamp(d);

        Destroy(f.gameObject);
        d.swamp = null;
        d.hasSwamp = false;
        map.AddDistrictPlots(d);
        d.SetLocked(false);
        foreach (var p in d.plots)
        {
            p.terrain = PlotTerrain.Normal;
            p.Refresh();
        }
        Redecorate(d);

        Vector3 rest = d.transform.localPosition;
        CityFX.DustBurst(d.transform.position + Vector3.up * 0.5f, 14f, 50, 1.2f);
        yield return Rise(d.transform, rest);
        Finish("BATAKLIK KURUTULDU: Doldurulan alanda dört yeni arsa imara açıldı, çevre arsaların zemini sağlamlaştı.", 1f, "Bataklık kurutuldu");
    }

    /// <summary>Removes the swamp model and its marsh floor with a cloud of dust.</summary>
    IEnumerator ClearSwamp(District d)
    {
        var rig = FindAnyObjectByType<CityCameraController>();
        if (rig != null) rig.FocusOn(d.transform.position);
        CityFX.DustBurst(d.transform.position + Vector3.up, 12f, 70, 1.5f);
        yield return new WaitForSeconds(0.35f);
        if (d.swamp != null) Destroy(d.swamp.gameObject);
        d.swamp = null;
        var decor = d.transform.Find("Decor");
        if (decor != null) Destroy(decor.gameObject);
        yield return new WaitForSeconds(0.25f);
    }

    static IEnumerator Rise(Transform t, Vector3 rest)
    {
        float e = 0f;
        while (e < 0.7f)
        {
            e += Time.deltaTime;
            t.localPosition = rest + Vector3.down * Mathf.LerpUnclamped(2.5f, 0f, Ease.OutBack(Mathf.Clamp01(e / 0.7f)));
            yield return null;
        }
        t.localPosition = rest;
    }

    /// <summary>Plots around the old swamp get solid ground; their blocks are redecorated to match.</summary>
    void Finish(string news, float happiness, string reason)
    {
        foreach (var other in map.RefreshSwampTerrain()) Redecorate(other);
        CivilGameJam.UI.NewsTicker.Instance?.EnqueueNews(news);
        CityEconomy.Instance?.AddHappiness(happiness, reason);
        CityEconomy.Instance?.RefreshStats();
        Busy = false;
    }

    void Redecorate(District d)
    {
        var decorator = map.GetComponent<CityDecorator>();
        if (decorator == null) return;
        var old = d.transform.Find("Decor");
        if (old != null) { old.name = "Decor_Old"; Destroy(old.gameObject); }
        foreach (var p in d.plots)
            if (p != null && p.wild != null) Destroy(p.wild.gameObject);
        decorator.DecorateDistrict(map, d);
    }
}
