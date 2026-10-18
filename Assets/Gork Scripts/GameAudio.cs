using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Persistent Music/SFX routing. Public volume methods accept slider values from 0 to 1.</summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }
    public float MasterVolume { get; private set; }
    public float MusicVolume { get; private set; }
    public float SfxVolume { get; private set; }
    public GameAudioLibrary Library { get; private set; }
    AudioSource music, clicks, sea;
    CityMap map;
    Camera view;
    float nextMapCheck;
    bool nextDropUsesGrunt;
    public void PlayHappinessDrop()
    {
        Play(nextDropUsesGrunt ? CitySound.MaleGrunt : CitySound.CommunitySigh);
        nextDropUsesGrunt = !nextDropUsesGrunt;
    }
    readonly Dictionary<CitySound, float> lastPlayed = new Dictionary<CitySound, float>();
    readonly Dictionary<CitySound, List<AudioSource>> activeSounds = new Dictionary<CitySound, List<AudioSource>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance == null) new GameObject("GameAudio").AddComponent<GameAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Library = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
        if (Library == null) { Debug.LogWarning("GameAudioLibrary is missing. Run Tools/Audio/Configure City Audio."); return; }
        music = Source("Music", transform, Library.background, true, false, true, false);
        music.volume = .45f;
        clicks = Source("Clicks", transform, null, false, false);
        sea = Source("SeaAmbience", transform, Library.sea, true, false, false, false);
        sea.volume = 0f;
        MusicVolume = PlayerPrefs.GetFloat("Audio.Music", 1f);
        SfxVolume = PlayerPrefs.GetFloat("Audio.SFX", 1f);
        MasterVolume = PlayerPrefs.GetFloat("Audio.Master", 1f);
    }

    void Start()
    {
        // Mixer values must be applied after Awake (Unity's mixer initialization order).
        SetMasterVolume(MasterVolume);
        SetMusicVolume(MusicVolume);
        SetSfxVolume(SfxVolume);
        if (music != null && music.clip != null) music.Play();
        if (sea != null && sea.clip != null) sea.Play();
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        Library?.mixer?.SetFloat("MasterVolume", Decibels(MasterVolume));
        PlayerPrefs.SetFloat("Audio.Master", MasterVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        Library?.mixer?.SetFloat("MusicVolume", Decibels(MusicVolume));
        PlayerPrefs.SetFloat("Audio.Music", MusicVolume);
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        Library?.mixer?.SetFloat("SFXVolume", Decibels(SfxVolume));
        PlayerPrefs.SetFloat("Audio.SFX", SfxVolume);
    }

    static float Decibels(float value) => value <= .0001f ? -80f : Mathf.Log10(value) * 20f;

    AudioSource Source(string name, Transform parent, AudioClip clip, bool loop, bool spatial, bool isMusic = false, bool play = true)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.clip = clip;
        source.loop = loop;
        source.outputAudioMixerGroup = isMusic ? Library.musicGroup : Library.sfxGroup;
        source.spatialBlend = spatial ? .7f : 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 15f;
        source.maxDistance = 250f;
        source.dopplerLevel = 0f;
        if (play && clip != null) source.Play();
        return source;
    }

    public AudioSource PlayLoop(CitySound sound, Transform owner, bool spatial = true, float volume = .45f)
    {
        if (Library == null || owner == null || Library.Clip(sound) == null) return null;
        var source = Source("Audio_" + sound, owner, Library.Clip(sound), true, spatial);
        source.volume = volume;
        Track(sound, source);
        return source;
    }

    public void Play(CitySound sound, Vector3? position = null, float volume = .65f)
    {
        if (Library == null || Library.Clip(sound) == null) return;
        float cooldown = sound == CitySound.CrowdScream ? .8f : .08f;
        if (lastPlayed.TryGetValue(sound, out float last) && Time.unscaledTime - last < cooldown) return;
        lastPlayed[sound] = Time.unscaledTime;
        if (!position.HasValue)
        {
            clicks.PlayOneShot(Library.Clip(sound), volume);
            return;
        }
        var source = Source("Audio_" + sound, transform, Library.Clip(sound), false, true);
        source.transform.position = position.Value;
        source.volume = volume;
        Track(sound, source);
        Destroy(source.gameObject, Library.Clip(sound).length + .2f);
    }

    void Track(CitySound sound, AudioSource source)
    {
        if (!activeSounds.TryGetValue(sound, out var sources))
            activeSounds[sound] = sources = new List<AudioSource>();
        sources.RemoveAll(item => item == null);
        sources.Add(source);
    }

    public void Stop(CitySound sound)
    {
        if (!activeSounds.TryGetValue(sound, out var sources)) return;
        foreach (var source in sources)
        {
            if (source == null) continue;
            source.Stop();
            Destroy(source.gameObject);
        }
        sources.Clear();
        lastPlayed.Remove(sound);
    }

    public void RouteSfx(AudioSource source)
    {
        if (source != null && Library != null) source.outputAudioMixerGroup = Library.sfxGroup;
    }

    void Update()
    {
        if (Library == null) return;
        var mouse = Mouse.current;
        var touch = Touchscreen.current;
        if ((mouse != null && mouse.leftButton.wasPressedThisFrame) ||
            (touch != null && touch.primaryTouch.press.wasPressedThisFrame))
            Play(CitySound.Tap2, volume: .35f);

        if (sea == null || sea.clip == null) return;
        if (Time.unscaledTime >= nextMapCheck)
        {
            nextMapCheck = Time.unscaledTime + 1f;
            if (map == null) map = FindAnyObjectByType<CityMap>();
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        }
        float target = 0f;
        if (map != null && view != null)
        {
            var ray = view.ViewportPointToRay(new Vector3(.5f, .5f));
            var plane = new Plane(Vector3.up, new Vector3(0f, map.transform.position.y + map.seaLevel, 0f));
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 focus = map.transform.InverseTransformPoint(ray.GetPoint(distance));
                // The sea slab also extends underneath the city, so use the actual southern coastline.
                float slopeLength = Mathf.Max(4f, map.beachWidth - map.beachFlat) + 10f;
                float coastline = -map.beachFlat - slopeLength * Mathf.Clamp01((-map.seaLevel - .06f) / 1.2f);
                target = Mathf.SmoothStep(0f, 1f, (coastline + 20f - focus.z) / 35f) * .45f;
            }
        }
        sea.volume = Mathf.MoveTowards(sea.volume, target, Time.unscaledDeltaTime * .3f);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }
}
