using UnityEngine;
using UnityEngine.Audio;

public enum CitySound { Background, Collapse, Construction, CrowdScream, Earthquake, Fire, Paper, PoliceSiren, Tap, Tap2, Sea, CommunitySigh, CommunityHappy, Coin, Notification, TvOn, TvOff, MaleGrunt, Flood }

/// <summary>Asset references live in Resources/GameAudioLibrary; source audio stays in Assets/Audios.</summary>
public class GameAudioLibrary : ScriptableObject
{
    public AudioMixer mixer;
    public AudioMixerGroup musicGroup;
    public AudioMixerGroup sfxGroup;
    public AudioClip background, collapse, construction, crowdScream, earthquake, fire, paper, policeSiren, tap, tap2, sea;
    public AudioClip communitySigh, communityHappy, coin, notification;
    public AudioClip tvOn, tvOff;
    public AudioClip maleGrunt, flood;

    public AudioClip Clip(CitySound sound)
    {
        switch (sound)
        {
            case CitySound.Background: return background;
            case CitySound.Collapse: return collapse;
            case CitySound.Construction: return construction;
            case CitySound.CrowdScream: return crowdScream;
            case CitySound.Earthquake: return earthquake;
            case CitySound.Fire: return fire;
            case CitySound.Paper: return paper;
            case CitySound.PoliceSiren: return policeSiren;
            case CitySound.Tap: return tap;
            case CitySound.Tap2: return tap2;
            case CitySound.Sea: return sea;
            case CitySound.CommunitySigh: return communitySigh;
            case CitySound.CommunityHappy: return communityHappy;
            case CitySound.Coin: return coin;
            case CitySound.Notification: return notification;
            case CitySound.TvOn: return tvOn;
            case CitySound.TvOff: return tvOff;
            case CitySound.MaleGrunt: return maleGrunt;
            case CitySound.Flood: return flood;
            default: return null;
        }
    }
}
