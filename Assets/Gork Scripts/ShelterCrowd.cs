using System.Collections.Generic;
using UnityEngine;

/// <summary>Persistent, one-person-per-occupant visual crowd. Never owned by temporary disaster effects.</summary>
public class ShelterCrowd : MonoBehaviour
{
    Building building;
    Transform crowd;
    CityLife life;
    float refresh;
    readonly List<ShelterResident> people = new List<ShelterResident>();

    public static ShelterCrowd For(Building shelter)
    {
        var component = shelter.GetComponent<ShelterCrowd>();
        return component != null ? component : shelter.gameObject.AddComponent<ShelterCrowd>();
    }
    void Awake() { building = GetComponent<Building>(); EnsureRoot(); }
    void EnsureRoot()
    {
        if (crowd != null) return;
        crowd = new GameObject("ShelteredResidents").transform; crowd.SetParent(transform, false);
    }
    Vector3 Slot(int index)
    {
        int capacity = Mathf.Max(1, building.definition.emergencyCapacity);
        int columns = Mathf.CeilToInt(Mathf.Sqrt(capacity));
        int rows = Mathf.CeilToInt(capacity / (float)columns);
        float width = building.Plot != null ? building.Plot.size * .65f : 6f;
        float spacing = Mathf.Min(1.05f, width / Mathf.Max(columns, rows));
        return transform.TransformPoint(new Vector3((index % columns - (columns - 1) * .5f) * spacing,
            .32f, (index / columns - (rows - 1) * .5f) * spacing));
    }
    void Update()
    {
        refresh -= Time.deltaTime;
        if (refresh > 0 || building == null || building.definition == null) return;
        refresh = .4f; EnsureRoot(); people.RemoveAll(p => p == null);
        int target = Mathf.Max(0, building.shelteredResidents);
        // Rebuild a saved or externally changed population without fabricating logical residents.
        while (people.Count < target)
        {
            var resident = Spawn(people.Count);
            if (resident == null) break;
            people.Add(resident); resident.WaitAt(Slot(people.Count - 1));
        }
        while (people.Count > target)
        {
            var last = people[people.Count - 1]; people.RemoveAt(people.Count - 1);
            if (last != null) Destroy(last.gameObject);
        }
    }
    ShelterResident Spawn(int slot)
    {
        if (life == null) life = FindAnyObjectByType<CityLife>();
        if (life == null || life.peoplePrefabs.Count == 0) return null;
        EnsureRoot();
        var prefab = life.peoplePrefabs[Random.Range(0, life.peoplePrefabs.Count)];
        if (prefab == null) return null;
        var go = new GameObject("ShelterResident"); go.transform.SetParent(crowd, false);
        var animator = prefab.GetComponentInChildren<Animator>();
        WalkAnimator walk;
        if (animator != null && animator.isHuman)
        {
            Instantiate(prefab, go.transform); walk = go.AddComponent<ProceduralWalk>();
        }
        else
        {
            var body = new GameObject("Body").transform; body.SetParent(go.transform, false);
            var model = Instantiate(prefab, body); model.transform.localPosition = Vector3.zero;
            var bob = go.AddComponent<BobWalk>(); bob.model = body; walk = bob;
        }
        walk.amount = 0; CityMap.SetLayerRecursive(go, 2);
        var resident = go.AddComponent<ShelterResident>(); resident.walk = walk;
        return resident;
    }
    public void Admit(Plot from, int count)
    {
        people.RemoveAll(p => p == null);
        for (int i = 0; i < count; i++)
        {
            var resident = Spawn(people.Count);
            if (resident == null) break;
            Vector3 destination = Slot(people.Count); people.Add(resident);
            var path = Route(from, building.Plot);
            path.Insert(0, Entrance(from)); path.Add(destination);
            resident.Travel(path, i * .09f, null);
        }
    }
    public void Depart(Building housing, int count)
    {
        people.RemoveAll(p => p == null);
        for (int i = 0; i < count && people.Count > 0; i++)
        {
            var resident = people[people.Count - 1]; people.RemoveAt(people.Count - 1);
            if (resident == null) continue;
            var manager = DisasterManager.Instance;
            resident.transform.SetParent(manager != null ? manager.transform : null, true);
            var path = Route(building.Plot, housing.Plot);
            path.Insert(0, resident.transform.position); path.Add(Entrance(housing.Plot));
            resident.Travel(path, i * .06f, housing);
        }
    }
    public void Dismiss(int count)
    {
        people.RemoveAll(p => p == null);
        for (int i = 0; i < count && people.Count > 0; i++)
        {
            var person = people[people.Count - 1]; people.RemoveAt(people.Count - 1);
            if (person != null) Destroy(person.gameObject);
        }
    }
    static Vector3 Entrance(Plot plot)
    {
        if (plot == null) return Vector3.zero;
        var manager = DisasterManager.Instance; Vector3 front = Vector3.back;
        if (manager != null && manager.map != null && plot.district != null)
            manager.map.GetPlotAccess(plot, out front, out _);
        return plot.transform.position + front * plot.size * .38f + Vector3.up * .15f;
    }
    static List<Vector3> Route(Plot from, Plot to)
    {
        if (DisasterManager.Instance != null && from != null && to != null)
            return DisasterManager.Instance.RoadPath(from, to);
        return new List<Vector3> { Entrance(from), Entrance(to) };
    }
}
