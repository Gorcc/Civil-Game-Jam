using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>Builds serialized clip references and Music/SFX mixer groups without moving source files.</summary>
[InitializeOnLoad]
public static class CityAudioSetup
{
    const string MixerPath = "Assets/Audio/CityAudio.mixer";
    const string LibraryPath = "Assets/Resources/GameAudioLibrary.asset";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static bool queued;

    static CityAudioSetup()
    {
        QueueSetup();
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) QueueSetup(); };
    }

    public static void QueueSetup()
    {
        if (queued) return;
        queued = true;
        EditorApplication.delayCall += () => { queued = false; if (!EditorApplication.isPlayingOrWillChangePlaymode) Configure(); };
    }

    [MenuItem("Tools/Audio/Configure City Audio")]
    public static void Configure()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            if (!AssetDatabase.IsValidFolder("Assets/Audio")) AssetDatabase.CreateFolder("Assets", "Audio");
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var library = AssetDatabase.LoadAssetAtPath<GameAudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<GameAudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            library.background = Clip("background");
            library.collapse = Clip("building collapse");
            library.construction = Clip("construction");
            library.crowdScream = Clip("crowd scream");
            library.earthquake = Clip("earthquake");
            library.fire = Clip("fire");
            library.paper = Clip("paper1");
            library.policeSiren = Clip("police siren"); // Emergency response siren; routed through SFX.
            library.tap = Clip("tap");
            library.tap2 = Clip("tap2");
            library.sea = Clip("sea");
            library.communitySigh = Clip("community sigh");
            library.communityHappy = Clip("community happy");
            library.coin = Clip("coin");
            library.notification = Clip("notification"); // Reserved for future notifications.
            library.tvOn = Clip("tv on");
            library.tvOff = Clip("tv off");
            library.maleGrunt = Clip("malegrunt");
            library.flood = Clip("flood");

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            var controller = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEditor.Audio.AudioMixerController")).First(t => t != null);
            if (mixer == null)
                mixer = (AudioMixer)controller.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { MixerPath });
            library.mixer = mixer;
            library.musicGroup = Group(mixer, controller, "Music");
            library.sfxGroup = Group(mixer, controller, "SFX");
            ExposeVolumes(mixer, controller, library.musicGroup, library.sfxGroup);
            EditorUtility.SetDirty(mixer);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(mixer);
            AssetDatabase.SaveAssetIfDirty(library);
            Debug.Log("[CityAudio] Music/SFX configured. Sea clip: " + (library.sea != null));
        }
        catch (Exception error) { Debug.LogException(error); }
    }

    static AudioClip Clip(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audios" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }

    static AudioMixerGroup Group(AudioMixer mixer, Type controller, string name)
    {
        var existing = mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name);
        if (existing != null) return existing;
        var group = (AudioMixerGroup)controller.GetMethod("CreateNewGroup", Flags)
            .Invoke(mixer, new object[] { name, false });
        var master = controller.GetProperty("masterGroup", Flags).GetValue(mixer);
        controller.GetMethod("AddChildToParent", Flags).Invoke(mixer, new object[] { group, master });
        return group;
    }

    static void ExposeVolumes(AudioMixer mixer, Type controller, AudioMixerGroup music, AudioMixerGroup sfx)
    {
        // Unity's public runtime API cannot create mixer assets; these editor-only members
        // follow UnityCsReference/Editor/Mono/Audio/Mixer/Bindings/AudioMixerController.cs.
        var property = controller.GetProperty("exposedParameters", Flags);
        var entries = (Array)property.GetValue(mixer);
        var type = property.PropertyType.GetElementType();
        var list = new System.Collections.Generic.List<object>();
        foreach (var item in entries) list.Add(item);
        var master = (AudioMixerGroup)controller.GetProperty("masterGroup", Flags).GetValue(mixer);
        foreach (var pair in new[] { (group: master, name: "MasterVolume"), (group: music, name: "MusicVolume"), (group: sfx, name: "SFXVolume") })
        {
            if (list.Any(item => (string)type.GetField("name", Flags).GetValue(item) == pair.name)) continue;
            object entry = Activator.CreateInstance(type);
            type.GetField("name", Flags).SetValue(entry, pair.name);
            type.GetField("guid", Flags).SetValue(entry, pair.group.GetType().GetMethod("GetGUIDForVolume", Flags).Invoke(pair.group, null));
            list.Add(entry);
        }
        var result = Array.CreateInstance(type, list.Count);
        for (int i = 0; i < list.Count; i++) result.SetValue(list[i], i);
        property.SetValue(mixer, result);
    }
}

public class CityAudioImportProcessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] previous)
    {
        if (imported.Concat(deleted).Concat(moved).Concat(previous).Any(p => p.StartsWith("Assets/Audios/", StringComparison.OrdinalIgnoreCase)))
            CityAudioSetup.QueueSetup();
    }
}
