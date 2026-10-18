using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns construction settings and spawns construction sites and machines.
/// Prefab fields left empty fall back to gray-box placeholders.
/// </summary>
public class ConstructionManager : MonoBehaviour
{
    public static ConstructionManager Instance { get; private set; }

    public CityMap map;
    public Font font;

    [Header("Machines")]
    public GameObject excavatorPrefab;
    public GameObject mixerTruckPrefab;
    public GameObject cranePrefab;
    [Tooltip("Vehicles spawn this far down the road from the plot.")]
    public float spawnDistance = 40f;
    public float vehicleSpeed = 14f;

    [Header("Site props")]
    public GameObject scaffoldPrefab;
    public Vector3 scaffoldSize = new Vector3(4.32f, 4.98f, 1.62f);
    public List<GameObject> materialProps = new List<GameObject>();
    public GameObject conePrefab;
    public GameObject barrierPrefab;

    [Header("Materials")]
    public Material blockMaterial;
    public Material particleMaterial;
    public Material debrisMaterial;

    [Header("Animation")]
    public float floorRevealDelay = 0.15f;
    public float demolishDuration = 1.4f;

    public event Action<Plot, Building> ConstructionCompleted;

    void Awake()
    {
        Instance = this;
        if (map == null) map = FindAnyObjectByType<CityMap>();
        font = UIKit.Font;
        CityFX.blockMaterial = blockMaterial;
        CityFX.particleMaterial = particleMaterial;
        CityFX.debrisMaterial = debrisMaterial;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public ConstructionSite StartConstruction(Plot plot, BuildingDefinition def, int floors)
    {
        var go = new GameObject("ConstructionSite_" + def.displayName);
        go.transform.SetParent(plot.transform, false);
        var site = go.AddComponent<ConstructionSite>();
        site.Begin(this, plot, def, floors);
        return site;
    }

    public ConstructionMachine SpawnMachine(GameObject prefab, string placeholderName)
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, transform);
        }
        else
        {
            go = new GameObject(placeholderName);
            go.transform.SetParent(transform, false);
            CityFX.Block("Body", go.transform, Vector3.zero, new Vector3(2.4f, 1.8f, 5f), new Color(0.55f, 0.55f, 0.55f));
            CityFX.Block("Cab", go.transform, new Vector3(0f, 1.8f, 1.2f), new Vector3(2f, 1.2f, 1.6f), new Color(0.45f, 0.45f, 0.48f));
        }

        var machine = go.GetComponent<ConstructionMachine>();
        if (machine == null) machine = go.AddComponent<ConstructionMachine>();
        machine.speed = vehicleSpeed;
        return machine;
    }

    /// <summary>Where the plot meets its access road. front = unit vector from plot toward the road.</summary>
    public void GetAccess(Plot plot, out Vector3 front, out float roadZ)
    {
        if (map != null && plot.district != null)
        {
            map.GetPlotAccess(plot, out front, out roadZ);
            return;
        }
        front = Vector3.back;
        roadZ = plot.transform.position.z - plot.size * 0.5f - CityMap.TileSize * 1.5f;
    }

    internal void NotifyCompleted(Plot plot, Building building) => ConstructionCompleted?.Invoke(plot, building);
}
