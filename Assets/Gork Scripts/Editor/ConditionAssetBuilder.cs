using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the assets BuildingCondition uses: a 4-variant crack atlas (alpha-clipped decal
/// texture) and the crack / steel / timber materials. Menu: Afet Sehri/Build Condition Assets
/// </summary>
public static class ConditionAssetBuilder
{
    const string TexDir = "Assets/Gork Data/Textures";
    const string MatDir = "Assets/Gork Data/Materials";
    const int CellW = 128, CellH = 256, Variants = 4;

    [MenuItem("Afet Sehri/Build Condition Assets")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(TexDir)) AssetDatabase.CreateFolder("Assets/Gork Data", "Textures");

        string texPath = TexDir + "/T_Cracks.png";
        File.WriteAllBytes(texPath, CrackAtlas().EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        var crack = Mat("M_Crack", "Universal Render Pipeline/Unlit");
        crack.SetTexture("_BaseMap", tex);
        crack.SetColor("_BaseColor", new Color(0.13f, 0.11f, 0.1f, 1f));
        crack.SetFloat("_AlphaClip", 1f);
        crack.SetFloat("_Cutoff", 0.5f);
        crack.EnableKeyword("_ALPHATEST_ON");
        crack.SetFloat("_Cull", 0f);
        crack.renderQueue = 2450;

        var steel = Mat("M_Steel", "Universal Render Pipeline/Lit");
        steel.SetColor("_BaseColor", new Color(0.32f, 0.35f, 0.4f));
        steel.SetFloat("_Metallic", 0.6f);
        steel.SetFloat("_Smoothness", 0.45f);

        var timber = Mat("M_Timber", "Universal Render Pipeline/Lit");
        timber.SetColor("_BaseColor", new Color(0.55f, 0.38f, 0.22f));
        timber.SetFloat("_Smoothness", 0.1f);

        var rubble = Mat("M_Rubble", "Universal Render Pipeline/Lit");
        rubble.SetColor("_BaseColor", new Color(0.55f, 0.5f, 0.45f));
        rubble.SetFloat("_Smoothness", 0.05f);

        AssetDatabase.SaveAssets();
        Debug.Log("Condition assets built.");
    }

    static Material Mat(string name, string shader)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(m, path);
        }
        else m.shader = Shader.Find(shader);
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Four jagged, branching cracks side by side, white on transparent.</summary>
    static Texture2D CrackAtlas()
    {
        int w = CellW * Variants, h = CellH;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var px = new Color32[w * h];
        var rng = new System.Random(7);
        for (int v = 0; v < Variants; v++)
        {
            int x0 = v * CellW;
            // Main crack from the top, wandering down; a few branches.
            Walk(px, w, h, rng, x0, CellW / 2f + Rand(rng, -20, 20), h - 6, Rand(rng, 0.7f, 0.9f) * h, 6f, 5);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    static void Walk(Color32[] px, int w, int h, System.Random rng, int x0, float x, float y, float length, float thick, int branches)
    {
        float travelled = 0f;
        float dir = -Mathf.PI / 2f + Rand(rng, -0.4f, 0.4f);   // downward
        while (travelled < length && y > 4 && thick > 0.6f)
        {
            float seg = Rand(rng, 6f, 14f);
            dir += Rand(rng, -0.7f, 0.7f);
            dir = Mathf.Clamp(dir, -Mathf.PI * 0.85f, -Mathf.PI * 0.15f);
            float nx = x + Mathf.Cos(dir) * seg, ny = y + Mathf.Sin(dir) * seg;
            nx = Mathf.Clamp(nx, 6, CellW - 6);
            Line(px, w, h, x0, x, y, nx, ny, thick);
            x = nx; y = ny;
            travelled += seg;
            thick *= 0.96f;
            if (branches > 0 && rng.NextDouble() < 0.18)
            {
                branches--;
                Walk(px, w, h, rng, x0, x, y, length * 0.35f, thick * 0.6f, 0);
            }
        }
    }

    static void Line(Color32[] px, int w, int h, int x0, float ax, float ay, float bx, float by, float thick)
    {
        int steps = Mathf.CeilToInt(Vector2.Distance(new Vector2(ax, ay), new Vector2(bx, by)) * 2f);
        int r = Mathf.CeilToInt(thick);
        for (int s = 0; s <= steps; s++)
        {
            float t = steps == 0 ? 0 : (float)s / steps;
            float cx = Mathf.Lerp(ax, bx, t), cy = Mathf.Lerp(ay, by, t);
            for (int oy = -r; oy <= r; oy++)
                for (int ox = -r; ox <= r; ox++)
                {
                    if (ox * ox + oy * oy > thick * thick) continue;
                    int px_ = x0 + Mathf.RoundToInt(cx) + ox, py = Mathf.RoundToInt(cy) + oy;
                    if (px_ < x0 || px_ >= x0 + CellW || py < 0 || py >= h) continue;
                    px[py * w + px_] = new Color32(255, 255, 255, 255);
                }
        }
    }

    static float Rand(System.Random r, float a, float b) => a + (float)r.NextDouble() * (b - a);
}
