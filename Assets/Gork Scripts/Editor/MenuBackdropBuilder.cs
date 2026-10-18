using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the main menu diorama in MenuScene: a floating island city (3x3 blocks, every block used) on a calm sea,
/// with a beach, tree line and citizens. MenuCityBackdrop animates it (quake, rebuild, storm, flood).
/// Menu: Afet Sehri/Rebuild Menu Backdrop
/// </summary>
public static class MenuBackdropBuilder
{
    const string Data = "Assets/Gork Data/MenuBackdrop";
    const string Buildings = "Assets/Gork Data/Buildings/";
    const float Pitch = 18f, Half = 33f;
    static Transform root;
    static Material grass, dirt, paving, asphalt, marking, sand, sea;

    [MenuItem("Afet Sehri/Rebuild Menu Backdrop")]
    public static void Build()
    {
        var menuScene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/MenuScene.unity");
        if (!menuScene.isLoaded) menuScene = EditorSceneManager.OpenScene("Assets/Scenes/MenuScene.unity", OpenSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(menuScene);
        var old = menuScene.GetRootGameObjects().FirstOrDefault(g => g.name == "MenuCityBackdrop");
        if (old != null) Object.DestroyImmediate(old);
        if (!AssetDatabase.IsValidFolder(Data)) AssetDatabase.CreateFolder("Assets/Gork Data", "MenuBackdrop");

        grass = Mat("Grass", new Color(.4f, .58f, .3f)); dirt = Mat("IslandDirt", new Color(.47f, .36f, .26f));
        paving = Mat("Sidewalk", new Color(.7f, .7f, .66f)); asphalt = Mat("Road", new Color(.27f, .29f, .31f));
        marking = Mat("RoadMarkings", new Color(.92f, .9f, .78f)); sand = Mat("Sand", new Color(.9f, .82f, .6f));
        sea = Mat("Sea", new Color(.5f, .76f, .82f), .85f);

        root = new GameObject("MenuCityBackdrop").transform;
        var anim = root.gameObject.AddComponent<MenuCityBackdrop>();
        anim.islandHalfSize = Half;

        // Floating island: grass top on a thick dirt body, sitting in a calm sea.
        Cube("IslandBody", new Vector3(0f, -2.6f, 0f), new Vector3(Half * 2f, 5f, Half * 2f), dirt);
        Cube("IslandTop", new Vector3(0f, -0.08f, 0f), new Vector3(Half * 2f + 0.3f, 0.3f, Half * 2f + 0.3f), grass);
        Cube("Sea", new Vector3(0f, -3.4f, 0f), new Vector3(400f, 0.2f, 400f), sea);
        // Beach along the south edge.
        Cube("Beach", new Vector3(0f, 0.02f, -Half + 2.4f), new Vector3(Half * 2f - 0.2f, 0.12f, 4.6f), sand);

        // Streets: a grid between and around the blocks.
        for (int line = -27; line <= 27; line += 18)
        {
            Cube("Street", new Vector3(line, 0.05f, 0f), new Vector3(5f, 0.1f, 59f), asphalt);
            Cube("Street", new Vector3(0f, 0.05f, line), new Vector3(59f, 0.1f, 5f), asphalt);
            for (float s = -27f; s <= 27f; s += 3.6f)
            {
                if (Mathf.Abs(s % Pitch) < 3f || Mathf.Abs(Mathf.Abs(s % Pitch) - Pitch) < 3f) continue;   // skip junctions
                Cube("Dash", new Vector3(line, 0.11f, s), new Vector3(0.12f, 0.02f, 1.6f), marking);
                Cube("Dash", new Vector3(s, 0.11f, line), new Vector3(1.6f, 0.02f, 0.12f), marking);
            }
        }

        // Every block is used. Two old apartment blocks are the weak ones that collapse and get rebuilt.
        var plan = new (string asset, int floors, bool weak)[,]
        {
            { ("Bld_ToplanmaAlani", 0, false), ("Bld_MustakilEv", 0, false), ("Bld_GoletliPark", 0, false) },   // south row (z = -18)
            { ("Bld_MahalleParki", 0, false), ("Bld_Konut", 5, true), ("Bld_Itfaiye", 0, false) },             // middle row
            { ("Bld_Konut", 4, true), ("Bld_Hastane", 0, false), ("Bld_Konut", 3, false) },                    // north row
        };
        var actors = new List<MenuCityBackdrop.Actor>();
        Vector3 refuge = Vector3.zero;
        Transform fireTarget = null;
        for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                var (asset, floors, weak) = plan[row, col];
                float x = (col - 1) * Pitch, z = (row - 1) * Pitch;
                Cube("Block", new Vector3(x, 0.08f, z), new Vector3(12.9f, 0.16f, 12.9f), paving);
                var holder = new GameObject(asset + "_Menu").transform;
                holder.SetParent(root, false);
                holder.localPosition = new Vector3(x, 0.16f, z);
                Building(asset, floors, holder, row * 3 + col);
                if (row == 2 && col == 2) fireTarget = holder;   // the block that catches fire
                if (asset == "Bld_ToplanmaAlani") refuge = holder.localPosition;
                else actors.Add(new MenuCityBackdrop.Actor { target = holder, vulnerable = weak });
            }
        anim.buildings = actors.ToArray();
        anim.fireTarget = fireTarget;

        // Fire truck parked on the east street by the fire station; drives up when the fire breaks out.
        var truckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gork Data/Prefabs/Veh_FireTruck.prefab");
        var truck = new GameObject("MenuFireTruck").transform;
        truck.SetParent(root, false);
        truck.localPosition = new Vector3(Pitch * 1.5f - 1.2f, 0.12f, -6f);
        Spawn(truckPrefab, truck, Vector3.zero, Quaternion.identity, 0.85f);
        anim.fireTruck = truck;

        // Trees around the island edge, palms on the beach.
        var tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SyntyStudios/PolygonAdventure/Prefabs/Environments/SM_Env_Tree_01.prefab");
        var palm = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Gork Data/Prefabs/Env_Palm_01.prefab");
        var rng = new System.Random(7);
        for (float s = -Half + 1.5f; s <= Half - 1.5f; s += 3.3f)
        {
            Spawn(tree, root, new Vector3(s + (float)rng.NextDouble() - .5f, 0.1f, Half - 1.4f), Quaternion.Euler(0f, rng.Next(360), 0f), 0.9f + (float)rng.NextDouble() * 0.3f);
            Spawn(tree, root, new Vector3(-Half + 1.4f, 0.1f, s + (float)rng.NextDouble() - .5f), Quaternion.Euler(0f, rng.Next(360), 0f), 0.9f + (float)rng.NextDouble() * 0.3f);
            Spawn(tree, root, new Vector3(Half - 1.4f, 0.1f, s + (float)rng.NextDouble() - .5f), Quaternion.Euler(0f, rng.Next(360), 0f), 0.9f + (float)rng.NextDouble() * 0.3f);
        }
        for (float s = -Half + 4f; s <= Half - 4f; s += 7.5f)
            Spawn(palm, root, new Vector3(s, 0.1f, -Half + 1.6f), Quaternion.Euler(0f, rng.Next(360), 0f), 1.1f);

        // No traffic: the cars looked busy and odd on such a small island.
        anim.cars = new Transform[0];

        // Citizens around the middle block who run to the assembly area when the quake hits.
        var people = AssetDatabase.FindAssets("Citizen t:Model", new[] { "Assets/Assets/Citizens_LowPoly" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(g => g != null).ToArray();
        var citizens = new List<MenuCityBackdrop.Citizen>();
        for (int i = 0; i < 14; i++)
        {
            var model = people[i % people.Length];
            var holder = new GameObject("MenuCitizen").transform;
            holder.SetParent(root, false);
            float a = i / 14f * Mathf.PI * 2f;
            var home = new Vector3(Mathf.Cos(a) * 7.6f, 0.2f, Mathf.Sin(a) * 7.6f);
            var spot = refuge + new Vector3((i % 5 - 2) * 1.3f, 0.2f, (i / 5 - 1) * 1.4f);
            holder.localPosition = home;
            Spawn(model, holder, Vector3.zero, model.transform.localRotation, 1f);
            citizens.Add(new MenuCityBackdrop.Citizen { target = holder, home = home, refuge = spot });
        }
        anim.citizens = citizens.ToArray();
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);

        // Camera: isometric orthographic view; MenuCityBackdrop fits it to the screen at runtime.
        var camera = menuScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>()).First();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.6f, .82f, .87f);
        camera.transform.rotation = Quaternion.Euler(34f, -38f, 0f);
        camera.transform.position = -camera.transform.forward * 150f;
        camera.orthographicSize = 34f;
        camera.farClipPlane = 500f;
        anim.sceneCamera = camera;

        var sun = menuScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Light>()).First();
        sun.type = LightType.Directional; sun.intensity = 1.15f; sun.color = new Color(1f, .95f, .85f);
        sun.transform.rotation = Quaternion.Euler(46f, -60f, 0f); sun.shadows = LightShadows.Soft;
        anim.sun = sun;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.62f, .67f, .7f);
        RenderSettings.fog = false;

        // Readability: a soft fade only behind the buttons, the rest of the diorama stays vivid.
        var menu = menuScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MainMenuUI>()).First();
        var texture = new Texture2D(256, 64, TextureFormat.RGBA32, false) { name = "MenuReadabilityGradient", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 256; x++)
            {
                float u = x / 255f;
                float alpha = Mathf.Lerp(.82f, 0f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.08f, .36f, u)));
                texture.SetPixel(x, y, new Color(.95f, .96f, .95f, alpha));
            }
        texture.Apply();
        var texturePath = Data + "/ReadabilityGradient.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (existing != null) { EditorUtility.CopySerialized(texture, existing); Object.DestroyImmediate(texture); texture = existing; }
        else AssetDatabase.CreateAsset(texture, texturePath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Data + "/ReadabilityOverlay.asset");
        if (sprite == null) { sprite = Sprite.Create(texture, new Rect(0, 0, 256, 64), new Vector2(.5f, .5f), 100); AssetDatabase.CreateAsset(sprite, Data + "/ReadabilityOverlay.asset"); }
        menu.backgroundSprite = sprite; menu.background.sprite = sprite; menu.background.color = new Color(1f, .97f, .92f);   // warm paper, matches the logo
        EditorUtility.SetDirty(menu);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(menuScene);
        EditorSceneManager.SaveScene(menuScene);
    }

    static void Building(string asset, int floors, Transform holder, int seed)
    {
        var d = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + asset + ".asset");
        if (d == null) return;
        if (d.prefab != null) { Spawn(d.prefab, holder, Vector3.zero, Quaternion.identity, 1f); return; }
        for (int f = 0; f < floors; f++) Modules(f == 0 ? d.groundModule : d.floorModule, holder, f * d.floorHeight, d.quadModules);
        Modules(d.roofModule, holder, floors * d.floorHeight, d.quadModules);
        if (d.residentialPalettes == null || d.residentialPalettes.Length == 0) return;
        foreach (var r in holder.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
                if (mats[m] != null && mats[m].name.StartsWith("PolygonCity_Mat_01")) mats[m] = d.residentialPalettes[seed % d.residentialPalettes.Length];
            r.sharedMaterials = mats;
        }
    }

    static Material Mat(string name, Color color, float smoothness = .1f)
    {
        var path = Data + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", smoothness); EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Cube(string name, Vector3 p, Vector3 size, Material m)
    {
        var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name = name; o.transform.SetParent(root, false); o.transform.localPosition = p; o.transform.localScale = size;
        o.GetComponent<Renderer>().sharedMaterial = m;
        return o.transform;
    }

    static GameObject Spawn(GameObject prefab, Transform parent, Vector3 p, Quaternion r, float scale)
    {
        if (prefab == null) return null;
        var o = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        o.transform.localPosition = p; o.transform.localRotation = r; o.transform.localScale *= scale;
        foreach (var script in o.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
        return o;
    }

    static void Modules(GameObject module, Transform parent, float y, bool quad)
    {
        if (module == null) return;
        if (!quad) { Spawn(module, parent, new Vector3(2.5f, y, 2.5f), Quaternion.identity, 1f); return; }
        for (int k = 0; k < 4; k++)
        {
            var r = Quaternion.Euler(0f, k * 90f, 0f);
            Spawn(module, parent, r * new Vector3(5f, 0f, 5f) + Vector3.up * y, r, 1f);
        }
    }
}
