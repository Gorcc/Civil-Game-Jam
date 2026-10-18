using UnityEditor;
using UnityEngine;

/// <summary>Make model references available to runtime-added maintenance managers in every scene/build.</summary>
[InitializeOnLoad]
public static class MunicipalResourceSetup
{
    static MunicipalResourceSetup()
    {
        EditorApplication.delayCall += EnsureResources;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EnsureResources();
        };
    }

    [MenuItem("Tools/Municipal/Configure Maintenance Models")]
    public static void EnsureResources()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Municipal")) AssetDatabase.CreateFolder("Assets/Resources", "Municipal");
        foreach (string name in new[] { "Overgrown_Weeds", "Litter_Pile", "Broken_Utility_Box",
            "Municipal_Worker_01_Orange_Vest", "Municipal_Worker_02_Lime_Vest", "Municipal_Worker_03_Yellow_Vest" })
        {
            string target = "Assets/Resources/Municipal/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(target) != null) continue;
            string folder = name.StartsWith("Municipal_Worker_") ? "Municipal_Workers_LowPoly" : "Municipal_Events_LowPoly";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Assets/" + folder + "/" + name + ".fbx");
            if (model == null) { Debug.LogError("[Municipal] Missing model: " + name); continue; }
            var root = new GameObject(name);
            try
            {
                var child = (GameObject)PrefabUtility.InstantiatePrefab(model);
                child.transform.SetParent(root.transform, false);
                PrefabUtility.SaveAsPrefabAsset(root, target);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
