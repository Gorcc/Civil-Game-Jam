using UnityEditor;
using UnityEngine;

/// <summary>
/// The Blender low-poly packs (Assets/Assets/...) import as glossy URP Lit materials with
/// specular highlights and environment reflections on, and slightly washed-out pastel colors.
/// Next to the matte, textured Synty assets they look like they glow. This postprocessor makes
/// every material generated from those FBX files matte and a touch deeper in color.
/// Runs automatically on import; use the menu to reimport the packs after changing it.
/// </summary>
public class LowPolyMaterialFix : AssetPostprocessor
{
    const string Root = "Assets/Assets/";
    const float Smoothness = 0.05f;
    const float SaturationBoost = 1.12f;
    const float ValueScale = 0.88f;

    // Run after URP's own FBX material description preprocessor, which sets up the Lit material.
    public override int GetPostprocessOrder() => 1000;

    // Models use "Import via MaterialDescription", so this is the hook that sees their materials.
    void OnPreprocessMaterialDescription(UnityEditor.AssetImporters.MaterialDescription description, Material material, AnimationClip[] animations)
    {
        Fix(material);
    }

    // Legacy import mode path.
    void OnPostprocessMaterial(Material material)
    {
        Fix(material);
    }

    void Fix(Material material)
    {
        if (!assetPath.StartsWith(Root)) return;
        if (!material.HasProperty("_BaseColor")) return;

        string n = material.name.ToLowerInvariant();
        bool glossy = n.Contains("glass") || n.Contains("water") || n.Contains("pool") || n.Contains("glazing");

        // Matte surfaces: no sun glints, no sky reflections (glass/water keep a little sheen).
        material.SetFloat("_Smoothness", glossy ? 0.5f : Smoothness);
        material.SetFloat("_Metallic", 0f);
        if (!glossy)
        {
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        }
        material.SetFloat("_EnvironmentReflections", 0f);
        material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");

        // Slightly deeper, richer colors so the pastel palette sits with the Synty textures.
        Color c = material.GetColor("_BaseColor");
        Color.RGBToHSV(c, out float h, out float s, out float v);
        Color fixedColor = Color.HSVToRGB(h, Mathf.Clamp01(s * SaturationBoost), v * ValueScale);
        fixedColor.a = c.a;
        material.SetColor("_BaseColor", fixedColor);
        if (material.HasProperty("_Color")) material.SetColor("_Color", fixedColor);
    }

    [MenuItem("Afet Sehri/Reimport Low Poly Packs")]
    public static void ReimportPacks()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Assets" }))
            AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        Debug.Log("Low poly packs reimported with matte materials.");
    }
}
