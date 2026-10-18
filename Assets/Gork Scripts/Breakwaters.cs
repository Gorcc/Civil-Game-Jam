using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Offshore breakwaters (Breakwater_LowPoly) built when the research center finishes "Dalgakıran".
/// A few detached rubble-mound segments run parallel to the beach; each piece rises out of the sea
/// one after another from the middle outward, with a small white splash.
/// Pieces are authored along local X with their pivot at the +X end (a 12 m pitch); End caps point to -X.
/// </summary>
public class Breakwaters : MonoBehaviour
{
    public static Breakwaters Instance { get; private set; }

    public GameObject straight, end;
    [Tooltip("Number of detached segments along the coast.")]
    public int segments = 3;
    [Tooltip("Straight pieces between the two end caps of a segment.")]
    public int straightsPerSegment = 2;
    [Tooltip("Distance from the city edge (z = 0) out to sea (m).")]
    public float offshore = 46f;

    const float Pitch = 12f;
    const string Folder = "Assets/Assets/Breakwater_LowPoly/Prefabs/";
    const string ResourcesFolder = "Breakwater/";

    CityMap map;
    Transform root;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindAnyObjectByType<Breakwaters>() != null || FindAnyObjectByType<CityMap>() == null) return;
        var ui = FindAnyObjectByType<CityUI>();
        (ui != null ? ui.gameObject : new GameObject("Breakwaters")).AddComponent<Breakwaters>();
    }

    void Awake()
    {
        Instance = this;
        map = FindAnyObjectByType<CityMap>();
        straight = straight != null ? straight : Load("Breakwater_Straight");
        end = end != null ? end : Load("Breakwater_End");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    static GameObject Load(string name)
    {
        var prefab = Resources.Load<GameObject>(ResourcesFolder + name);
#if UNITY_EDITOR
        if (prefab == null) prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
#endif
        return prefab;
    }

    public bool Built => root != null;

    public void Build(bool animate = true)
    {
        if (map == null || straight == null || end == null || root != null) return;
        root = new GameObject("Breakwaters").transform;
        root.SetParent(map.transform, false);

        var pieces = new List<Transform>();
        float width = map.MapSize.x;
        float spacing = width / segments;
        float y = map.seaLevel - 0.3f;
        for (int s = 0; s < segments; s++)
        {
            // Segment from right (+X) to left: mirrored end cap, straights, end cap.
            float length = (straightsPerSegment + 1) * Pitch + 10f;
            float right = spacing * (s + 0.5f) + length * 0.5f - 10f;
            var seg = new GameObject("Segment_" + s).transform;
            seg.SetParent(root, false);
            pieces.Add(Spawn(end, seg, new Vector3(right + 1f, y, -offshore), 180f));
            for (int i = 0; i < straightsPerSegment; i++)
                pieces.Add(Spawn(straight, seg, new Vector3(right - i * Pitch, y, -offshore), 0f));
            pieces.Add(Spawn(end, seg, new Vector3(right - straightsPerSegment * Pitch, y, -offshore), 0f));
        }
        CityMap.SetLayerRecursive(root.gameObject, 2);

        if (!animate) return;
        Vector3 middle = map.transform.TransformPoint(new Vector3(width * 0.5f, 0f, -offshore));
        pieces.Sort((a, b) => Mathf.Abs(a.position.x - middle.x).CompareTo(Mathf.Abs(b.position.x - middle.x)));
        var rig = FindAnyObjectByType<CityCameraController>();
        if (rig != null) rig.FocusOn(middle + Vector3.forward * 20f);
        for (int i = 0; i < pieces.Count; i++)
            StartCoroutine(Rise(pieces[i], 0.6f + i * 0.18f));
    }

    Transform Spawn(GameObject prefab, Transform parent, Vector3 local, float yaw)
    {
        var go = Instantiate(prefab, parent);
        go.transform.localPosition = local;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
        return go.transform;
    }

    /// <summary>Slowly surfaces from under the water, settles with a soft splash.</summary>
    IEnumerator Rise(Transform piece, float delay)
    {
        Vector3 rest = piece.localPosition;
        piece.localPosition = rest + Vector3.down * 3.5f;
        yield return new WaitForSeconds(delay);
        Splash(piece, 14);
        const float duration = 1.6f;
        float e = 0f;
        while (e < duration)
        {
            e += Time.deltaTime;
            piece.localPosition = rest + Vector3.down * 3.5f * (1f - Ease.OutCubic(Mathf.Clamp01(e / duration)));
            yield return null;
        }
        piece.localPosition = rest;
        Splash(piece, 8);
    }

    void Splash(Transform piece, int count)
    {
        // Pieces reach 12 m toward local -X from their pivot; splash at the waterline over the middle.
        Vector3 world = piece.position - piece.right * 6f;
        world.y = map.transform.position.y + map.seaLevel + 0.2f;
        var ps = CityFX.DustBurst(world, 4f, count, 0.7f);
        if (ps == null) return;
        var main = ps.main;
        main.startColor = new Color(0.92f, 0.97f, 1f, 0.75f);
    }
}
