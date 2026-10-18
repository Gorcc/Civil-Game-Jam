using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Wraps the Construction_LowPoly / Hospital FBX models into game prefabs:
/// forward = +Z, ground origin, and moving parts grouped under pivots
/// (excavator upper body + boom, mixer drum, crane jib) for ConstructionMachine.
/// Menu: Afet Sehri/Build Construction Prefabs
/// </summary>
public static class ConstructionPrefabBuilder
{
    const string Src = "Assets/Assets/Construction_LowPoly/";
    const string Dst = "Assets/Gork Data/Prefabs";

    [MenuItem("Afet Sehri/Build Construction Prefabs")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(Dst))
            AssetDatabase.CreateFolder("Assets/Gork Data", "Prefabs");

        BuildExcavator();
        BuildMixer();
        BuildCrane();
        foreach (var n in new[] { "Scaffolding", "Brick_Pile", "Cement_Bags", "Timber_Stack", "Steel_Beam_Stack" })
            BuildStatic(Src + n + ".fbx", "Con_" + n, 0f, 1f);
        BuildStatic("Assets/Assets/Hospital_LowPoly/Hospital_LowPoly.fbx", "Bld_Hospital", 0f, 0.55f);

        // City buildings, scaled to fit a 2x2-tile plot and centered (entrances face +Z).
        const string A = "Assets/Assets/";
        BuildFit(A + "Civic_LowPoly/Fire_Station.fbx", "Bld_FireStation");
        BuildFit(A + "Civic_LowPoly/Earthquake_Assembly_Area.fbx", "Bld_AssemblyArea");
        BuildFit(A + "Civic_LowPoly/Neighborhood_Park.fbx", "Bld_NeighborhoodPark");
        BuildFit(A + "Detached_Houses_LowPoly/Starter_Detached_House.fbx", "Bld_StarterHouse");
        BuildFit(A + "Detached_Houses_LowPoly/Luxury_Detached_Villa.fbx", "Bld_LuxuryVilla");
        BuildFit(A + "Factory_Beach_LowPoly/Factory.fbx", "Bld_Factory");
        // The beach model has its sea on +Z; turn it so the sea side faces away from the road.
        BuildFit(A + "Factory_Beach_LowPoly/Beach.fbx", "Bld_BeachResort", 10.4f, 180f);
        BuildFit(A + "University_Pond_LowPoly/University.fbx", "Bld_University");
        BuildFit(A + "University_Pond_LowPoly/Pond_Park.fbx", "Bld_PondPark");

        // Fire truck faces +X in the model; turn it to +Z for CarAgent.
        BuildStatic(A + "Fire_Truck_LowPoly/Fire_Truck.fbx", "Veh_FireTruck", -90f, 1f);

        AssetDatabase.SaveAssets();
        Debug.Log("Construction prefabs built in " + Dst);
    }

    static void BuildExcavator()
    {
        var root = Begin(Src + "Excavator.fbx", "Veh_Excavator", out Transform model, out Transform fbx);

        Transform upper = Group(model, fbx, "Upper", new Vector3(0.45f, 0.9f, 0f),
            n => n.StartsWith("Upper Chassis") || n.StartsWith("Cabin") || n.StartsWith("Engine") ||
                 n.StartsWith("Rear Counterweight") || n.StartsWith("Exhaust") || n.StartsWith("Excavator Mark") ||
                 n == "Arm Pivot");
        Transform boom = Group(upper, fbx, "Boom", new Vector3(-0.2f, 1.55f, -0.34f),
            n => n.StartsWith("Main Boom") || n.StartsWith("Boom ") || n.StartsWith("Dipper") ||
                 n.StartsWith("Bucket") || n.StartsWith("Arm Pivot."));

        // Bucket faces -X in the model; rotate so it faces +Z.
        model.localRotation = Quaternion.Euler(0f, 90f, 0f);

        var m = root.AddComponent<ConstructionMachine>();
        m.slewPivot = upper;
        m.armPivot = boom;
        m.armAxis = Vector3.forward;
        m.armAngleMin = -14f;
        m.armAngleMax = 12f;
        m.slewAngle = 35f;
        Save(root);
    }

    static void BuildMixer()
    {
        var root = Begin(Src + "Concrete_Mixer_Truck.fbx", "Veh_MixerTruck", out Transform model, out Transform fbx);
        Transform drum = Group(model, fbx, "Drum", new Vector3(-0.85f, 2.07f, 0f), n => n.StartsWith("Faceted Mixer Drum"));

        // Cabin faces +X in the model.
        model.localRotation = Quaternion.Euler(0f, -90f, 0f);

        var m = root.AddComponent<ConstructionMachine>();
        m.spinner = drum;
        m.spinAxis = Vector3.right;
        m.spinSpeed = 120f;
        Save(root);
    }

    static void BuildCrane()
    {
        var root = Begin(Src + "Tower_Crane.fbx", "Con_TowerCrane", out Transform model, out Transform fbx);
        Transform jib = Group(model, fbx, "Jib", new Vector3(0f, 10.3f, 0f),
            n => n.StartsWith("Jib") || n.StartsWith("Counterweight") || n.StartsWith("Hoist") ||
                 n.StartsWith("Hook") || n.StartsWith("Crane Hook") || n.StartsWith("Operator Cabin") ||
                 n.StartsWith("Cabin"));

        var m = root.AddComponent<ConstructionMachine>();
        m.slewPivot = jib;
        m.slewAngle = 70f;
        m.slewSpeed = 0.35f;
        Save(root);
    }

    static void BuildStatic(string fbxPath, string name, float yRot, float scale)
    {
        var root = Begin(fbxPath, name, out Transform model, out _);
        model.localRotation = Quaternion.Euler(0f, yRot, 0f);
        model.localScale = Vector3.one * scale;
        Save(root);
    }

    /// <summary>Scales a model so its footprint fits maxSize meters and centers it on the root (ground stays at y=0).</summary>
    public static void BuildFit(string fbxPath, string name, float maxSize = 10.4f, float yRot = 0f)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath) == null)
        {
            Debug.LogWarning("Missing model, skipped: " + fbxPath);
            return;
        }
        var root = Begin(fbxPath, name, out Transform model, out _);
        model.localRotation = Quaternion.Euler(0f, yRot, 0f);
        var rs = model.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        float scale = maxSize / Mathf.Max(b.size.x, b.size.z);
        model.localScale = Vector3.one * scale;
        model.localPosition = new Vector3(-b.center.x * scale, -b.min.y * scale, -b.center.z * scale);
        Save(root);
    }

    // ---------- helpers ----------

    static GameObject Begin(string fbxPath, string name, out Transform model, out Transform fbx)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (asset == null) throw new Exception("Missing model: " + fbxPath);

        var root = new GameObject(name);
        model = new GameObject("Model").transform;
        model.SetParent(root.transform, false);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        fbx = inst.transform;
        fbx.SetParent(model, false);
        return root;
    }

    /// <summary>Moves matching direct children of the FBX root under a new pivot at modelPos (model space).</summary>
    static Transform Group(Transform parent, Transform fbx, string name, Vector3 modelPos, Func<string, bool> match)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, true);
        pivot.position = parent.root.TransformPoint(modelPos);
        pivot.rotation = parent.rotation;

        var kids = new Transform[fbx.childCount];
        for (int i = 0; i < kids.Length; i++) kids[i] = fbx.GetChild(i);
        foreach (var k in kids)
            if (match(k.name)) k.SetParent(pivot, true);
        return pivot;
    }

    static void Save(GameObject root)
    {
        string path = Dst + "/" + root.name + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }
}
