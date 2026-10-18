using System.Collections;
using UnityEngine;

/// <summary>Temporary weather with exact restoration of the original lighting and fog.</summary>
public class DisasterWeather : MonoBehaviour
{
    Light sun;
    float sunIntensity, fogDensity, darkIntensity;
    Color sunColor, ambient, fogColor;
    bool fog, captured;
    Transform rainAnchor;
    CityCameraController rig;
    AudioSource sound;
    AudioClip rumbleClip, rainClip;
    Coroutine flashRoutine;
    /// <summary>A storm owns the sun's color and intensity right now (DayNightCycle steps aside).</summary>
    public bool Active => captured;

    void Awake()
    {
        sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 0;
        GameAudio.Instance?.RouteSfx(sound);
        rumbleClip = Noise("DisasterRumble", 3f, true); rainClip = Noise("StormRain", 2f, false);
    }
    static AudioClip Noise(string name, float seconds, bool low)
    {
        const int rate = 22050;
        var data = new float[Mathf.RoundToInt(rate * seconds)]; uint state = 839272u; float filtered = 0;
        for (int i = 0; i < data.Length; i++)
        {
            state = unchecked(1664525u * state + 1013904223u);
            float noise = ((state >> 8) / 16777215f) * 2 - 1;
            filtered = Mathf.Lerp(filtered, noise, low ? .022f : .45f);
            data[i] = filtered * (low ? Mathf.Sin(Mathf.PI * i / data.Length) * .9f : .14f);
        }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    public void Rumble(float duration)
    {
        // The recorded earthquake loop is played by DisasterManager.
        if (GameAudio.Instance != null && GameAudio.Instance.Library != null && GameAudio.Instance.Library.earthquake != null) return;
        if (sound == null) return;
        sound.clip = rumbleClip; sound.loop = true; sound.volume = .65f; sound.Play();
        StartCoroutine(StopRumble(duration));
    }
    IEnumerator StopRumble(float duration) { yield return new WaitForSeconds(duration); if (!captured && sound != null) sound.Stop(); }

    public void Begin(CityMap map, CityCameraController cameraRig, Transform effects)
    {
        Restore(); rig = cameraRig; sun = RenderSettings.sun;
        if (sun == null)
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light.enabled && light.gameObject.scene == gameObject.scene) { sun = light; break; }
        ambient = RenderSettings.ambientLight; fog = RenderSettings.fog;
        fogColor = RenderSettings.fogColor; fogDensity = RenderSettings.fogDensity;
        if (sun != null) { sunIntensity = sun.intensity; sunColor = sun.color; darkIntensity = sunIntensity * .28f; }
        captured = true;
        if (sun != null) { sun.intensity = darkIntensity; sun.color = new Color(.57f, .66f, .8f); }
        RenderSettings.ambientLight = Color.Lerp(ambient, new Color(.11f, .15f, .23f), .65f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.25f, .30f, .38f); RenderSettings.fogDensity = Mathf.Max(.003f, fogDensity);
        rainAnchor = new GameObject("StormRainAnchor").transform; rainAnchor.SetParent(effects, false);
        Follow(); CityFX.Rain(rainAnchor, 110f, 1900f);
        if (sound != null) { sound.clip = rainClip; sound.loop = true; sound.volume = .65f; sound.Play(); }
    }
    public void Follow()
    {
        if (rainAnchor == null) return;
        var camera = rig != null ? rig.cam : Camera.main;
        Vector3 center = rig != null ? rig.transform.position : Vector3.zero;
        if (camera != null)
        {
            var ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) center = ray.GetPoint(distance);
        }
        rainAnchor.position = new Vector3(center.x, 0, center.z);
    }
    public void Lightning() { if (captured && flashRoutine == null) flashRoutine = StartCoroutine(Flash()); }
    IEnumerator Flash()
    {
        if (sun != null) sun.intensity = Mathf.Max(sunIntensity * 2.1f, 1.5f);
        yield return new WaitForSeconds(.09f); if (sun != null) sun.intensity = darkIntensity;
        yield return new WaitForSeconds(.07f); if (sun != null) sun.intensity = Mathf.Max(sunIntensity * 1.6f, 1.1f);
        yield return new WaitForSeconds(.07f); if (sun != null) sun.intensity = darkIntensity;
        yield return new WaitForSeconds(.35f);
        if (sound != null) sound.PlayOneShot(rumbleClip, .85f);
        if (rig != null) rig.Shake(.22f, .6f);
        flashRoutine = null;
    }
    public void Restore()
    {
        if (flashRoutine != null) { StopCoroutine(flashRoutine); flashRoutine = null; }
        if (captured)
        {
            if (sun != null) { sun.intensity = sunIntensity; sun.color = sunColor; }
            RenderSettings.ambientLight = ambient; RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor; RenderSettings.fogDensity = fogDensity;
        }
        captured = false;
        if (rainAnchor != null) Destroy(rainAnchor.gameObject); rainAnchor = null;
        if (sound != null) sound.Stop();
    }
    void OnDisable() => Restore();
    void OnDestroy()
    {
        Restore(); if (rumbleClip != null) Destroy(rumbleClip); if (rainClip != null) Destroy(rainClip);
    }
}
