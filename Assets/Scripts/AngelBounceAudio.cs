using UnityEngine;

// Three user-supplied music tracks with local crossfading; procedural effects and local speech.
public sealed class AngelBounceAudio : MonoBehaviour
{
    public enum SceneMusic { Title, Action, Result }
    AudioSource music, otherMusic, effects, voice;
    AudioClip boing, success, throwSound, speech;
    readonly AudioClip[] tracks = new AudioClip[3];
    SceneMusic scene = SceneMusic.Title;
    bool started, fading;
    float fade;
    public bool Muted { get; private set; }
    public SceneMusic CurrentScene => scene;
    public AudioClip CurrentTrack => tracks[(int)scene];
    const int Rate = 22050;
    void Awake()
    {
        music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.volume = .28f;
        otherMusic = gameObject.AddComponent<AudioSource>(); otherMusic.loop = true; otherMusic.volume = 0;
        effects = gameObject.AddComponent<AudioSource>(); effects.volume = .42f;
        voice = gameObject.AddComponent<AudioSource>(); voice.volume = .65f;
        Muted = PlayerPrefs.GetInt("AngelBounce.Mute", 0) == 1;
        tracks[0] = Resources.Load<AudioClip>("Audio/Before_the_Sun_Breaks");
        tracks[1] = Resources.Load<AudioClip>("Audio/Running_Toward_the_Sun");
        tracks[2] = Resources.Load<AudioClip>("Audio/A_Heart_in_a_Quiet_Room");
        music.clip = tracks[0]; speech = Resources.Load<AudioClip>("Audio/Boyoon");
        boing = Tone("Boing", .48f, (t) => Mathf.Sin(2 * Mathf.PI * (105 * t + 150 * (1 - Mathf.Exp(-8 * t)) / 8)) * Mathf.Exp(-6 * t));
        throwSound = Tone("Throw", .16f, (t) => Mathf.Sin(2 * Mathf.PI * (450 * t - 900 * t * t)) * (1 - t / .16f));
        success = Tone("Delivered", .8f, (t) => { int[] notes = { 72,76,79,84 }; int n = Mathf.Min(3, (int)(t / .18f)); return Mathf.Sin(2 * Mathf.PI * Hz(notes[n]) * t) * Mathf.Exp(-4 * (t % .18f)); });
        ApplyMute();
    }
    public void SetScene(SceneMusic next)
    {
        if (scene == next) return;
        scene = next;
        if (!started) { music.clip = tracks[(int)next]; return; }
        // Two sources maintain the outgoing song while the new scene fades in.
        otherMusic.Stop(); otherMusic.clip = tracks[(int)next]; otherMusic.volume = 0; otherMusic.Play();
        fade = 0; fading = true;
    }
    void Update()
    {
        if (!fading) return;
        fade = Mathf.Min(1, fade + Time.unscaledDeltaTime / .85f);
        music.volume = (1-fade)*.28f; otherMusic.volume = fade*.28f;
        if (fade < 1) return;
        music.Stop(); var old = music; music = otherMusic; otherMusic = old; fading = false;
    }
    static float Hz(int midi) => 440 * Mathf.Pow(2, (midi - 69) / 12f);
    static AudioClip Tone(string name, float seconds, System.Func<float,float> sample)
    {
        var data = new float[(int)(seconds * Rate)];
        for (int i = 0; i < data.Length; i++) data[i] = sample(i / (float)Rate) * .5f;
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false); clip.SetData(data, 0); return clip;
    }
    public void BeginAudio() { if (started) return; started = true; music.Play(); }
    public void ToggleMute() { Muted = !Muted; PlayerPrefs.SetInt("AngelBounce.Mute", Muted ? 1 : 0); PlayerPrefs.Save(); ApplyMute(); }
    void ApplyMute() { music.mute = otherMusic.mute = effects.mute = voice.mute = Muted; }
    public void Throw() { BeginAudio(); effects.PlayOneShot(throwSound); }
    public void Bounce() { effects.PlayOneShot(boing); if (speech) voice.PlayOneShot(speech); }
    public void Win() => effects.PlayOneShot(success);
    void OnApplicationPause(bool pause)
    {
        if (pause) {music.Pause(); otherMusic.Pause(); effects.Stop(); voice.Stop();}
        else if (started) {music.UnPause(); if(fading) otherMusic.UnPause();}
    }
}
