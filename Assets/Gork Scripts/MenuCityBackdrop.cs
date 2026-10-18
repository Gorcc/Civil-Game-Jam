using System;
using UnityEngine;

/// <summary>
/// Main menu diorama: a small island city that plays the game's story in a loop, no gameplay systems.
///   0-7 s   calm day: traffic, people about
///   7-14 s  earthquake: everything shakes, the two weak blocks collapse in dust and debris, people run to the
///           assembly area
///   18-23 s the collapsed blocks rise again, rebuilt
///   24-33 s storm: dark sky, rain, lightning, the streets flood and drain
///   33-40 s the sun is back, people walk home
///   40-50 s fire: a block burns, the fire truck drives up and puts it out, then drives back
/// The camera frames the island to the right of the menu buttons for any screen shape.
/// Built by Editor/MenuBackdropBuilder ("Afet Sehri/Rebuild Menu Backdrop").
/// </summary>
public class MenuCityBackdrop : MonoBehaviour
{
    [Serializable] public class Actor
    {
        public Transform target;
        public bool vulnerable;
        [NonSerialized] public Vector3 position, scale;
        [NonSerialized] public Quaternion rotation;
        [NonSerialized] public bool fell, rose;
        [NonSerialized] public Transform rubble;
    }
    [Serializable] public class Citizen
    {
        public Transform target;
        public Vector3 home, refuge;
        [NonSerialized] public Quaternion rotation;
    }

    public Actor[] buildings;
    public Citizen[] citizens;
    public Transform[] cars;
    public Camera sceneCamera;
    public Light sun;
    [Tooltip("Island footprint (half size) used to frame the camera.")]
    public float islandHalfSize = 33f;
    [Tooltip("Where the island's center sits on screen (0 left, 1 right): right of the menu buttons.")]
    [Range(0.4f, 0.8f)] public float screenCenterX = 0.64f;
    public float cycleSeconds = 50f;
    [Tooltip("Block that catches fire in the last act.")]
    public Transform fireTarget;
    [Tooltip("Fire truck parked by the station; drives to fireTarget.")]
    public Transform fireTruck;
    [Tooltip("Car lane span along X.")]
    public float laneHalfLength = 29f;

    public float Phase { get; private set; }

    Vector3 cameraBase;
    float lastAspect, clock;
    ParticleSystem dust, debris, rain;
    Transform flood;
    Material dustMat, debrisMat, rainMat, floodMat;
    float sunIntensity;
    Color sunColor, ambient, sky;
    float flash;
    // Sea life: scrolling waves, a foam ring around the island, drifting clouds.
    Material seaMat, foamMat, cloudMat, fireMat;
    Texture2D softTex;
    ParticleSystem flames, smoke, spray;
    Vector3 truckHome, fireTop;
    Renderer[] fireRenderers;
    MaterialPropertyBlock soot;
    Texture2D waveTex;
    Transform foam;
    readonly System.Collections.Generic.List<Transform> clouds = new System.Collections.Generic.List<Transform>();

    void Awake()
    {
        foreach (var a in buildings)
        {
            if (a.target == null) continue;
            a.position = a.target.localPosition;
            a.rotation = a.target.localRotation;
            a.scale = a.target.localScale;
        }
        foreach (var c in citizens) if (c.target != null) c.rotation = c.target.rotation;
        if (sun == null) sun = FindAnyObjectByType<Light>();
        if (sun != null) { sunIntensity = sun.intensity; sunColor = sun.color; }
        ambient = RenderSettings.ambientLight;
        if (sceneCamera != null) sky = sceneCamera.backgroundColor;
        BuildEffects();
        BuildSeaLife();
        BuildFire();
        // The dark title must stay readable when the storm darkens the sky: soft light halo around it.
        var title = GameObject.Find("GameTitle");
        if (title != null && title.GetComponent<UnityEngine.UI.Outline>() == null)
        {
            var halo = title.AddComponent<UnityEngine.UI.Outline>();
            halo.effectColor = new Color(1f, 1f, 1f, 0.6f);
            halo.effectDistance = new Vector2(2.5f, -2.5f);
        }
    }

    void OnDestroy()
    {
        foreach (var m in new[] { dustMat, debrisMat, rainMat, floodMat, seaMat, foamMat, cloudMat, fireMat }) if (m != null) Destroy(m);
        if (softTex != null) Destroy(softTex);
        if (waveTex != null) Destroy(waveTex);
    }

    // ---------- Camera framing ----------

    /// <summary>Fits the island (plus building height) into the right part of the screen for the current aspect.</summary>
    void Frame()
    {
        var cam = sceneCamera;
        if (cam == null || Mathf.Approximately(cam.aspect, lastAspect)) return;
        lastAspect = cam.aspect;
        var t = cam.transform;
        Vector3 center = transform.position;
        // Re-aim at the island center along the authored view direction.
        t.position = center - t.forward * 150f;
        Vector2 min = Vector2.one * float.MaxValue, max = Vector2.one * float.MinValue;
        float h = islandHalfSize;
        foreach (var y in new[] { -5f, 16f })
            foreach (var x in new[] { -h, h })
                foreach (var z in new[] { -h, h })
                {
                    Vector3 v = t.InverseTransformPoint(center + new Vector3(x, y, z));
                    min = Vector2.Min(min, v);
                    max = Vector2.Max(max, v);
                }
        float width = max.x - min.x, height = max.y - min.y;
        // Island uses ~58% of the width and ~76% of the height (room for the title), whichever is tighter.
        cam.orthographicSize = Mathf.Max(height * 0.5f / 0.76f, width * 0.5f / (0.58f * cam.aspect));
        Vector2 mid = (min + max) * 0.5f;
        float shiftX = (screenCenterX - 0.5f) * 2f * cam.orthographicSize * cam.aspect;
        t.position += t.right * (mid.x - shiftX) + t.up * (mid.y + cam.orthographicSize * 0.1f);
        cameraBase = t.position;
    }

    // ---------- Story loop ----------

    void Update()
    {
        Frame();
        // Capped step: the scene-load hitch must not jump the story ahead (it starts calm).
        clock += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        float t = clock % cycleSeconds;
        Phase = t;

        float quake = Window(t, 7f, 14f);
        float shake = quake > 0f ? Mathf.Sin((t - 7f) / 7f * Mathf.PI) : 0f;
        float storm = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 23.5f) / 1.5f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 33f) / 2f)));

        SeaLife(storm);
        Fire(t);
        Buildings(t, shake);
        People(t);
        Cars(quake > 0f || storm > 0.5f);
        Weather(t, storm);

        if (sceneCamera != null)
            sceneCamera.transform.position = cameraBase
                + sceneCamera.transform.right * Mathf.Sin(clock * 31f) * shake * 0.35f
                + sceneCamera.transform.up * Mathf.Cos(clock * 27f) * shake * 0.2f;
    }

    static float Window(float t, float a, float b) => t >= a && t < b ? 1f : 0f;

    void Buildings(float t, float shake)
    {
        for (int i = 0; i < buildings.Length; i++)
        {
            var a = buildings[i];
            if (a.target == null) continue;
            float wobble = Mathf.Sin(clock * 22f + i * 2.1f) * shake;
            Vector3 p = a.position + new Vector3(wobble * 0.12f, 0f, Mathf.Cos(clock * 26f + i) * shake * 0.08f);
            Quaternion r = a.rotation * Quaternion.Euler(wobble * 1.2f, 0f, wobble * 0.9f);
            Vector3 s = a.scale;

            if (a.vulnerable)
            {
                // Falls 10.5-12.5 s (staggered), lies in rubble, rises again 18-23 s.
                float fall = Mathf.Clamp01((t - (10.5f + (i % 2) * 0.8f)) / 1.6f);
                float rise = Mathf.Clamp01((t - (18.5f + (i % 2) * 1f)) / 3.5f);
                float down = Ease(fall) * (1f - Ease(rise));
                p += Vector3.down * down * 0.4f;
                r *= Quaternion.Euler(0f, 0f, down * (i % 2 == 0 ? 7f : -7f) * (1f - rise));
                s = Vector3.Scale(a.scale, new Vector3(1f + down * 0.08f, Mathf.Max(0.04f, 1f - down * 0.94f), 1f + down * 0.08f));
                Vector3 world = transform.TransformPoint(a.position);
                if (fall > 0.15f && !a.fell) { a.fell = true; Burst(world, 70, 40); a.rubble = Rubble(world, i); }
                if (rise > 0.05f && !a.rose)
                {
                    a.rose = true;
                    Burst(world, 25, 0);
                    if (a.rubble != null) { Destroy(a.rubble.gameObject); a.rubble = null; }
                }
                if (t < 7f) a.fell = a.rose = false;
            }
            a.target.localPosition = p;
            a.target.localRotation = r;
            a.target.localScale = s;
        }
    }

    static float Ease(float x) => x * x * (3f - 2f * x);

    void People(float t)
    {
        for (int i = 0; i < citizens.Length; i++)
        {
            var c = citizens[i];
            if (c.target == null) continue;
            float go = Mathf.Clamp01((t - 8.5f - i * 0.12f) / 4.5f);
            float back = Mathf.Clamp01((t - 34f - i * 0.1f) / 4f);
            float k = Ease(go) * (1f - Ease(back));
            var p = Vector3.Lerp(c.home, c.refuge, k);
            bool moving = (go > 0f && go < 1f) || (back > 0f && back < 1f);
            if (moving) p.y += Mathf.Abs(Mathf.Sin(clock * 15f + i)) * 0.1f;
            c.target.localPosition = p;
            var dir = (back > 0f ? c.home - c.refuge : c.refuge - c.home); dir.y = 0f;
            c.target.rotation = moving && dir.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dir) : c.rotation;
        }
    }

    void Cars(bool stopped)
    {
        float step = stopped ? 0f : Time.unscaledDeltaTime * 6f;
        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] == null) continue;
            var p = cars[i].localPosition;
            Vector3 dir = cars[i].localRotation * Vector3.forward;
            p += dir * step;
            if (p.x > laneHalfLength) p.x = -laneHalfLength;
            if (p.x < -laneHalfLength) p.x = laneHalfLength;
            if (p.z > laneHalfLength) p.z = -laneHalfLength;
            if (p.z < -laneHalfLength) p.z = laneHalfLength;
            cars[i].localPosition = p;
        }
    }

    void Weather(float t, float storm)
    {
        // Lightning: two flashes per storm.
        if ((t > 26.4f && t < 26.5f) || (t > 29.1f && t < 29.2f)) flash = 1f;
        flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 5f);

        if (sun != null)
        {
            sun.intensity = Mathf.Lerp(sunIntensity, sunIntensity * 0.22f, storm) + flash * 1.8f;
            sun.color = Color.Lerp(sunColor, new Color(0.62f, 0.7f, 0.85f), storm);
        }
        RenderSettings.ambientLight = Color.Lerp(ambient, ambient * 0.55f + new Color(0.02f, 0.03f, 0.06f), storm) + Color.white * flash * 0.25f;
        if (sceneCamera != null) sceneCamera.backgroundColor = Color.Lerp(sky, new Color(0.3f, 0.36f, 0.44f), storm) + Color.white * flash * 0.2f;

        var emission = rain.emission;
        emission.rateOverTime = storm * 900f;

        // Streets flood mid-storm and drain once it passes.
        float level = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 25f) / 2.5f)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 32f) / 3f)));
        flood.gameObject.SetActive(level > 0.01f);
        flood.localPosition = new Vector3(0f, Mathf.Lerp(-0.25f, 0.24f, level), 0f);
        floodMat.SetTextureOffset("_BaseMap", new Vector2(clock * 0.03f, clock * 0.02f));
    }

    /// <summary>A heap of concrete chunks where a building fell; removed when it is rebuilt.</summary>
    Transform Rubble(Vector3 world, int seed)
    {
        var pile = new GameObject("Rubble").transform;
        pile.SetParent(transform, true);
        pile.position = world;
        var rng = new System.Random(seed * 97 + 3);
        var colors = new[] { new Color(0.55f, 0.53f, 0.5f), new Color(0.46f, 0.42f, 0.38f), new Color(0.62f, 0.5f, 0.42f), new Color(0.36f, 0.35f, 0.34f) };
        for (int k = 0; k < 22; k++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(pile, false);
            float r = (float)rng.NextDouble() * 4.8f, a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float size = 0.6f + (float)rng.NextDouble() * 1.6f;
            c.transform.localPosition = new Vector3(Mathf.Cos(a) * r, size * 0.3f + (4.8f - r) * 0.18f, Mathf.Sin(a) * r);
            c.transform.localRotation = Quaternion.Euler(rng.Next(-30, 30), rng.Next(360), rng.Next(-30, 30));
            c.transform.localScale = new Vector3(size, size * 0.55f, size * (0.6f + (float)rng.NextDouble()));
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", colors[rng.Next(colors.Length)]);
            var rend = c.GetComponent<Renderer>();
            rend.sharedMaterial = RubbleMaterial;
            rend.SetPropertyBlock(mpb);
        }
        return pile;
    }

    static Material rubbleMaterial;
    static Material RubbleMaterial => rubbleMaterial != null ? rubbleMaterial
        : rubbleMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MenuRubble" };

    // ---------- Fire ----------

    void BuildFire()
    {
        if (fireTarget == null) return;
        // Soft round puff texture for flames, smoke and spray (the game's particle material lives in CityMap).
        softTex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "MenuSoftPuff", wrapMode = TextureWrapMode.Clamp };
        var px = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                px[y * 64 + x] = new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, d));
            }
        softTex.SetPixels(px);
        softTex.Apply();
        fireMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "MenuFireParticles" };
        fireMat.SetTexture("_BaseMap", softTex);
        fireMat.SetFloat("_Surface", 1f);
        fireMat.SetFloat("_ZWrite", 0f);
        fireMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        fireMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        fireMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        fireMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        CityFX.particleMaterial = fireMat;

        fireRenderers = fireTarget.GetComponentsInChildren<Renderer>();
        Bounds b = fireRenderers.Length > 0 ? fireRenderers[0].bounds : new Bounds(fireTarget.position, Vector3.one * 8f);
        foreach (var r in fireRenderers) b.Encapsulate(r.bounds);
        fireTop = new Vector3(b.center.x, b.max.y, b.center.z);
        soot = new MaterialPropertyBlock();

        flames = CityFX.Flames(transform, transform.InverseTransformPoint(fireTop - Vector3.up * 0.5f), 2.6f);
        smoke = CityFX.SmokeColumn(transform, transform.InverseTransformPoint(fireTop + Vector3.up * 1.5f), 2f);
        var fe = flames.emission; fe.rateOverTime = 0f;
        var se = smoke.emission; se.rateOverTime = 0f;
        // Flames sit across the roof, not on one point.
        var fs = flames.shape; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(b.size.x * 0.7f, b.size.z * 0.7f, 0.5f);

        if (fireTruck != null)
        {
            truckHome = fireTruck.localPosition;
            spray = CityFX.WaterSpray(fireTruck, new Vector3(0f, 2.6f, 1.5f));
            var pe = spray.emission; pe.rateOverTime = 0f;
        }
    }

    void Fire(float t)
    {
        if (flames == null) return;
        // 40 s ignition, 41.5-44 s truck drives up, 44-47.5 s spraying, flames die by 47.5 s, truck back by 50 s.
        float burn = Mathf.Clamp01((t - 40f) / 1.5f) * (1f - Mathf.Clamp01((t - 44.5f) / 3f));
        var fe = flames.emission; fe.rateOverTime = burn * 90f;
        float smokeK = Mathf.Clamp01((t - 40.3f) / 1.5f) * (1f - Mathf.Clamp01((t - 46f) / 3.5f));
        var se = smoke.emission; se.rateOverTime = smokeK * 22f;

        // Scorched look while it burns.
        float scorch = Mathf.Clamp01((t - 40.5f) / 2f) * (1f - Mathf.Clamp01((t - 47f) / 2.5f));
        foreach (var r in fireRenderers)
        {
            if (r == null) continue;
            if (scorch <= 0.001f) { r.SetPropertyBlock(null); continue; }
            soot.Clear();
            soot.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.45f, 0.4f, 0.38f), scorch));
            r.SetPropertyBlock(soot);
        }

        if (fireTruck == null) return;
        Vector3 stop = new Vector3(truckHome.x, truckHome.y, fireTarget.localPosition.z - 2f);
        float go = Ease(Mathf.Clamp01((t - 41.5f) / 2.5f));
        float back = Ease(Mathf.Clamp01((t - 47.5f) / 2.3f));
        fireTruck.localPosition = Vector3.Lerp(truckHome, stop, go * (1f - back));
        fireTruck.localRotation = Quaternion.Euler(0f, back > 0f && back < 1f ? 180f : 0f, 0f);
        bool spraying = t > 44f && t < 47.5f;
        var pe = spray.emission; pe.rateOverTime = spraying ? 180f : 0f;
        if (spraying) spray.transform.rotation = Quaternion.LookRotation((fireTop + Vector3.down * 2f) - spray.transform.position);
    }

    // ---------- Sea life ----------

    void BuildSeaLife()
    {
        // Waves: a soft ripple texture on a copy of the sea material.
        var sea = transform.Find("Sea");
        if (sea != null)
        {
            var r = sea.GetComponent<Renderer>();
            seaMat = new Material(r.sharedMaterial) { name = "MenuSeaLive" };
            waveTex = new Texture2D(128, 128, TextureFormat.RGBA32, true) { name = "MenuWaves", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[128 * 128];
            for (int y = 0; y < 128; y++)
                for (int x = 0; x < 128; x++)
                {
                    float u = x / 128f * Mathf.PI * 2f, v = y / 128f * Mathf.PI * 2f;
                    float crest = Mathf.Sin(v * 2f + Mathf.Sin(u * 2f) * 0.9f) * Mathf.Sin(u * 3f + v);
                    float k = 0.95f + 0.03f * Mathf.Sin(u + v * 2f);
                    if (crest > 0.82f) k = Mathf.Lerp(k, 1f, Mathf.InverseLerp(0.82f, 1f, crest) * 0.8f);
                    px[y * 128 + x] = new Color(k, k, k, 1f);
                }
            waveTex.SetPixels(px);
            waveTex.Apply(true, true);
            seaMat.SetTexture("_BaseMap", waveTex);
            seaMat.SetTextureScale("_BaseMap", new Vector2(14f, 14f));
            r.sharedMaterial = seaMat;
        }

        // Foam: a pale ring where the island meets the water.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(ring.GetComponent<Collider>());
        ring.name = "Foam";
        foam = ring.transform;
        foam.SetParent(transform, false);
        foam.localPosition = new Vector3(0f, -3.27f, 0f);
        foam.localScale = new Vector3(islandHalfSize * 2f + 3f, 0.04f, islandHalfSize * 2f + 3f);
        foamMat = Transparent("MenuFoam", new Color(1f, 1f, 1f, 0.55f));
        var fr = ring.GetComponent<Renderer>();
        fr.sharedMaterial = foamMat;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Clouds: a few low-poly puffs drifting over the open sea in front of the island
        // (never over the city or the logo).
        cloudMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MenuCloud" };
        cloudMat.SetColor("_BaseColor", Color.white);
        cloudMat.SetFloat("_Smoothness", 0f);
        var rng = new System.Random(11);
        for (int i = 0; i < 4; i++)
        {
            var cloud = new GameObject("Cloud").transform;
            cloud.SetParent(transform, false);
            cloud.localPosition = new Vector3(-90f + i * 48f + (float)rng.NextDouble() * 12f, 6f + (float)rng.NextDouble() * 3f, -islandHalfSize - 14f - (float)rng.NextDouble() * 22f);
            int puffs = 3 + rng.Next(3);
            for (int k = 0; k < puffs; k++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(puff.GetComponent<Collider>());
                puff.transform.SetParent(cloud, false);
                float size = 3f + (float)rng.NextDouble() * 2.5f;
                puff.transform.localPosition = new Vector3((k - puffs * 0.5f) * 2.8f, (float)rng.NextDouble() * 1.2f, (float)rng.NextDouble() * 2.5f);
                puff.transform.localScale = new Vector3(size, size * 0.6f, size * 0.8f);
                var pr = puff.GetComponent<Renderer>();
                pr.sharedMaterial = cloudMat;
                pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            clouds.Add(cloud);
        }
    }

    void SeaLife(float storm)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        if (seaMat != null) seaMat.SetTextureOffset("_BaseMap", new Vector2(clock * 0.012f, clock * 0.02f));
        if (foam != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 1.4f);
            float s = islandHalfSize * 2f + 2.2f + pulse * 1.6f;
            foam.localScale = new Vector3(s, 0.04f, s);
            foamMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0.65f, 1f - pulse)));
        }
        cloudMat.SetColor("_BaseColor", Color.Lerp(Color.white, new Color(0.42f, 0.45f, 0.5f), storm));
        foreach (var c in clouds)
        {
            var p = c.localPosition;
            p.x += dt * (2.2f + storm * 5f);
            if (p.x > 95f) p.x = -95f;
            c.localPosition = p;
        }
    }

    static Material Transparent(string name, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Surface", 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    void Burst(Vector3 world, int dustCount, int debrisCount)
    {
        dust.transform.position = world + Vector3.up;
        dust.Emit(dustCount);
        if (debrisCount <= 0) return;
        debris.transform.position = world + Vector3.up * 4f;
        debris.Emit(debrisCount);
    }

    // ---------- Effects ----------

    void BuildEffects()
    {
        dust = Particles("Dust", new Color(0.66f, 0.62f, 0.54f, 0.55f), out dustMat, false);
        var main = dust.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f); main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 3.2f); main.gravityModifier = -0.03f;
        var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(9f, 2f, 9f);
        var size = dust.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
        Fade(dust, 0.75f);

        debris = Particles("Debris", new Color(0.55f, 0.5f, 0.45f, 1f), out debrisMat, true);
        main = debris.main;
        main.startLifetime = 2.2f; main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.7f); main.gravityModifier = 1.4f;
        main.startRotation3D = true;
        main.startRotationX = main.startRotationY = main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        shape = debris.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 3f;
        var rot = debris.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
        rot.x = rot.y = rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        rain = Particles("Rain", new Color(0.78f, 0.85f, 0.95f, 0.45f), out rainMat, false);
        rain.transform.localPosition = new Vector3(0f, 34f, 0f);
        main = rain.main;
        main.loop = true; main.startLifetime = 1.3f; main.startSpeed = 32f; main.startSize = 0.12f; main.maxParticles = 3000;
        // Box emits along its local +Z; turned to face down, its X/Y become the horizontal rain area.
        shape = rain.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(islandHalfSize * 2.4f, islandHalfSize * 2.4f, 1f);
        shape.rotation = new Vector3(90f, 0f, 0f);
        var em = rain.emission; em.enabled = true; em.rateOverTime = 0f;
        var r = rain.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 4f; r.velocityScale = 0.04f;
        rain.Play();

        // Flood sheet over the island, hidden until the storm.
        var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(water.GetComponent<Collider>());
        water.name = "MenuFlood";
        flood = water.transform;
        flood.SetParent(transform, false);
        flood.localScale = new Vector3(islandHalfSize * 2f - 1f, 0.05f, islandHalfSize * 2f - 1f);
        floodMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "MenuFlood" };
        floodMat.SetColor("_BaseColor", new Color(0.22f, 0.36f, 0.4f, 0.8f));
        floodMat.SetFloat("_Smoothness", 0.92f);
        floodMat.SetFloat("_Surface", 1f);
        floodMat.SetOverrideTag("RenderType", "Transparent");
        floodMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        floodMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        floodMat.SetFloat("_ZWrite", 0f);
        floodMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        floodMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        floodMat.SetTextureScale("_BaseMap", new Vector2(6f, 6f));
        var r2 = water.GetComponent<Renderer>();
        r2.sharedMaterial = floodMat;
        r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        water.SetActive(false);
    }

    ParticleSystem Particles(string name, Color color, out Material mat, bool meshCubes)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 600;
        main.startColor = color;
        var em = ps.emission; em.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (meshCubes)
        {
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
        }
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        r.sharedMaterial = mat;
        return ps;
    }

    static void Fade(ParticleSystem ps, float startAlpha)
    {
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
    }
}
