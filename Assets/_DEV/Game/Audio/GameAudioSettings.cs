using UnityEngine;
using UnityEngine.Audio;

// Music and sound-effects volume, 0..1, saved between sessions (PlayerPrefs) and applied on start.
//
// Best with an AudioMixer: route music sources to a "Music" group and everything else to an "SFX"
// group, expose each group's Volume as "MusicVolume" / "SFXVolume", and assign the mixer on PauseUI.
// Without a mixer, the SFX value drives AudioListener.volume (all sound), and Music is stored for
// whatever plays music later (read GameAudioSettings.Music).
public static class GameAudioSettings
{
    public enum Channel { Music, SoundEffects }

    const string MusicKey = "Glowtag.MusicVolume";
    const string SfxKey = "Glowtag.SfxVolume";

    public static AudioMixer Mixer { get; private set; }
    public static string MusicParameter = "MusicVolume";
    public static string SfxParameter = "SFXVolume";

    public static float Music { get; private set; } = 0.8f;
    public static float SoundEffects { get; private set; } = 0.8f;

    static bool loaded;

    // Called once by PauseUI on start. Safe to call again.
    public static void Init(AudioMixer mixer)
    {
        Mixer = mixer;
        if (!loaded)
        {
            Music = PlayerPrefs.GetFloat(MusicKey, Music);
            SoundEffects = PlayerPrefs.GetFloat(SfxKey, SoundEffects);
            loaded = true;
        }
        Apply(Channel.Music);
        Apply(Channel.SoundEffects);
    }

    public static float Get(Channel channel) => channel == Channel.Music ? Music : SoundEffects;

    public static void Set(Channel channel, float value)
    {
        value = Mathf.Clamp01(value);
        if (channel == Channel.Music) { Music = value; PlayerPrefs.SetFloat(MusicKey, value); }
        else { SoundEffects = value; PlayerPrefs.SetFloat(SfxKey, value); }
        Apply(channel);
    }

    public static void Save() => PlayerPrefs.Save();

    static void Apply(Channel channel)
    {
        float value = Get(channel);

        if (Mixer != null)
        {
            // Mixer volume is in decibels: 1 -> 0 dB, 0.5 -> -6 dB, 0 -> silent (-80 dB).
            float db = value <= 0.0001f ? -80f : Mathf.Log10(value) * 20f;
            Mixer.SetFloat(channel == Channel.Music ? MusicParameter : SfxParameter, db);
            return;
        }

        if (channel == Channel.SoundEffects) AudioListener.volume = value;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Mixer = null; loaded = false; }
}
