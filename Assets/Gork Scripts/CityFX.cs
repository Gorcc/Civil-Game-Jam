using UnityEngine;

/// <summary>
/// Procedural effects and placeholder blocks: dust, sparks, debris, rubble.
/// Materials are assigned by ConstructionManager on Awake.
/// </summary>
public static class CityFX
{
    public static Material blockMaterial;
    public static Material particleMaterial;
    public static Material debrisMaterial;

    static MaterialPropertyBlock mpb;

    // ---------- Blocks ----------

    /// <summary>Gray-box block with its pivot at the bottom center; scale the returned transform to resize.</summary>
    public static Transform Block(string name, Transform parent, Vector3 localPos, Vector3 size, Color color)
    {
        var pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = localPos;
        pivot.localScale = size;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(cube.GetComponent<Collider>());
        cube.transform.SetParent(pivot, false);
        cube.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        var r = cube.GetComponent<Renderer>();
        if (blockMaterial != null) r.sharedMaterial = blockMaterial;
        SetColor(r, color);
        return pivot;
    }

    public static void SetColor(Renderer r, Color color)
    {
        if (mpb == null) mpb = new MaterialPropertyBlock();
        mpb.Clear();
        mpb.SetColor("_BaseColor", color);
        mpb.SetColor("_Color", color);
        r.SetPropertyBlock(mpb);
    }

    // ---------- Particles ----------

    static ParticleSystem NewSystem(string name, Transform parent, Vector3 pos, bool worldPos)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        if (parent != null) go.transform.SetParent(parent, false);
        if (worldPos) go.transform.position = pos;
        else go.transform.localPosition = pos;

        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        if (particleMaterial != null) r.sharedMaterial = particleMaterial;

        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        return ps;
    }

    static Gradient AlphaFade(Color c, float peak = 0.15f)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, peak), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    /// <summary>Looping dust rising from a square area.</summary>
    public static ParticleSystem Dust(Transform parent, Vector3 localPos, float halfSize, float rate)
    {
        var ps = NewSystem("Dust", parent, localPos, false);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.78f, 0.7f, 0.58f, 0.75f), new Color(0.65f, 0.63f, 0.6f, 0.6f));
        main.gravityModifier = -0.02f;
        main.maxParticles = 300;

        var em = ps.emission;
        em.rateOverTime = rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(halfSize * 2f, halfSize * 2f, 0.2f);
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFade(Color.white);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));

        ps.gameObject.SetActive(true);
        return ps;
    }

    /// <summary>Looping welding sparks shooting upward in short bursts.</summary>
    public static ParticleSystem Sparks(Transform parent, Vector3 localPos)
    {
        var ps = NewSystem("Sparks", parent, localPos, false);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.45f), new Color(1f, 0.55f, 0.12f));
        main.gravityModifier = 1.4f;
        main.maxParticles = 200;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)6, (short)14, 0, 0.35f) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 40f;
        sh.radius = 0.1f;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.06f;
        r.lengthScale = 1.5f;

        ps.gameObject.SetActive(true);
        return ps;
    }

    /// <summary>One-shot dust cloud; destroys itself when done.</summary>
    public static ParticleSystem DustBurst(Vector3 worldPos, float radius, int count, float sizeMul = 1f)
    {
        var ps = NewSystem("DustBurst", null, worldPos, true);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f * sizeMul, 3.5f * sizeMul);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.72f, 0.6f, 0.9f), new Color(0.62f, 0.6f, 0.58f, 0.8f));
        main.gravityModifier = -0.03f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Hemisphere;
        sh.radius = radius;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var vel = ps.limitVelocityOverLifetime;
        vel.enabled = true;
        vel.limit = 0.6f;
        vel.dampen = 0.08f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFade(Color.white, 0.08f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));

        ps.gameObject.SetActive(true);
        ps.Play();
        return ps;
    }

    /// <summary>One-shot chunks flying out and bouncing on the ground.</summary>
    public static ParticleSystem Debris(Vector3 worldPos, float radius, int count)
    {
        var ps = NewSystem("Debris", null, worldPos, true);
        var main = ps.main;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 0.35f, 0.28f), new Color(0.5f, 0.5f, 0.5f));
        main.gravityModifier = 2f;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Hemisphere;
        sh.radius = radius;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        var collision = ps.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.bounce = 0.3f;
        collision.dampen = 0.5f;
        collision.quality = ParticleSystemCollisionQuality.Low;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (debrisMaterial != null) r.sharedMaterial = debrisMaterial;

        ps.gameObject.SetActive(true);
        ps.Play();
        return ps;
    }

    // ---------- Disaster effects ----------

    /// <summary>Looping flames rising from a point; size ~1 for a window, ~3 for a roof.</summary>
    public static ParticleSystem Flames(Transform parent, Vector3 localPos, float size)
    {
        var ps = NewSystem("Flames", parent, localPos, false);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * size, 3f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f * size, 1.4f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.45f, 0.1f));
        main.gravityModifier = -0.15f;
        main.maxParticles = 400;

        var em = ps.emission;
        em.rateOverTime = 28f * size;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 12f;
        sh.radius = 0.5f * size;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.4f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ps.gameObject.SetActive(true);
        ps.Play();
        return ps;
    }

    /// <summary>Looping dark smoke column.</summary>
    public static ParticleSystem SmokeColumn(Transform parent, Vector3 localPos, float size)
    {
        var ps = NewSystem("Smoke", parent, localPos, false);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.5f * size, 3f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.18f, 0.17f, 0.17f, 0.7f), new Color(0.35f, 0.33f, 0.32f, 0.6f));
        main.gravityModifier = -0.05f;
        main.maxParticles = 200;

        var em = ps.emission;
        em.rateOverTime = 7f * size;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 15f;
        sh.radius = 0.8f * size;
        sh.rotation = new Vector3(-90f, 0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFade(Color.white, 0.1f);

        var sz = ps.sizeOverLifetime;
        sz.enabled = true;
        sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.2f));

        ps.gameObject.SetActive(true);
        ps.Play();
        return ps;
    }

    /// <summary>Hose spray along the local forward axis.</summary>
    public static ParticleSystem WaterSpray(Transform parent, Vector3 localPos)
    {
        var ps = NewSystem("WaterSpray", parent, localPos, false);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(12f, 16f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.9f, 1f, 0.85f), new Color(0.9f, 0.97f, 1f, 0.7f));
        main.gravityModifier = 1.2f;
        main.maxParticles = 600;

        var em = ps.emission;
        em.rateOverTime = 160f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 5f;
        sh.radius = 0.1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        col.color = AlphaFade(Color.white, 0.05f);

        ps.gameObject.SetActive(true);
        ps.Play();
        return ps;
    }

    /// <summary>Rain falling over a square area around the parent (e.g. the camera rig).</summary>
    public static ParticleSystem Rain(Transform parent, float area, float rate)
    {
        var ps = NewSystem("Rain", parent, new Vector3(0f, 32f, 0f), false);
        var main = ps.main;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = 1.3f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(26f, 32f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.82f, 0.95f, 0.55f), new Color(0.85f, 0.9f, 1f, 0.4f));
        main.maxParticles = 8000;

        var em = ps.emission;
        em.rateOverTime = rate;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(area, area, 0.1f);
        sh.rotation = new Vector3(90f, 0f, 0f);   // emit straight down

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(3f, 4.5f);   // wind
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(1f, 2f);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 2f;

        ps.gameObject.SetActive(true);
        ps.Play();
        return ps;
    }

    /// <summary>A small pile of blocks that shrinks away after a while.</summary>
    public static Transform Rubble(Vector3 worldPos, float halfSize, float lifetime)
    {
        var root = new GameObject("Rubble").transform;
        root.position = worldPos;
        Color[] colors = { new Color(0.55f, 0.35f, 0.28f), new Color(0.5f, 0.5f, 0.5f), new Color(0.4f, 0.38f, 0.36f) };
        for (int i = 0; i < 14; i++)
        {
            var size = new Vector3(Random.Range(0.6f, 2f), Random.Range(0.3f, 1f), Random.Range(0.6f, 2f));
            var pos = new Vector3(Random.Range(-halfSize, halfSize), -0.1f, Random.Range(-halfSize, halfSize));
            var b = Block("Chunk", root, pos, size, colors[i % colors.Length]);
            b.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
        }
        var s = root.gameObject.AddComponent<ShrinkAndDestroy>();
        s.delay = lifetime;
        return root;
    }
}
