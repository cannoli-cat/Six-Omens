using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class MusicPlayer : MonoBehaviour {
    [Header("Routing")] [SerializeField] private AudioMixerGroup musicOutput;

    [Header("Clips")] [SerializeField] private AudioClip titleClip;
    [SerializeField] private AudioClip introCutsceneClip;
    [SerializeField] private AudioClip dayClip;

    [Header("Fading")] [SerializeField, Min(0f)]
    private float crossfadeSeconds = 1f;

    [SerializeField] private bool useUnscaledTime = true;

    public static MusicPlayer Instance { get; private set; }

    private AudioSource a;
    private AudioSource b;
    private AudioSource active;
    private Coroutine fadeRoutine;
    
    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        a = CreateChildSource("MusicA");
        b = CreateChildSource("MusicB");
        active = a;
        
        PlayTitleMusic();
    }

    private AudioSource CreateChildSource(string name) {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.outputAudioMixerGroup = musicOutput;
        return src;
    }
    
    public void PlayTitleMusic() => Play(titleClip);
    public void PlayIntroCutsceneMusic() => Play(introCutsceneClip);
    public void PlayGameMusic() => Play(dayClip);

    private void Play(AudioClip clip, float? customFadeSeconds = null) {
        if (clip == null) {
            Debug.LogWarning("[MusicPlayer] Null clip");
            return;
        }

        if (active.clip == clip && active.isPlaying) return;

        var from = active;
        var to = (from == a) ? b : a;

        to.clip = clip;
        to.loop = true;
        to.volume = 0f;
        to.Play();
        
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(Crossfade(from, to, customFadeSeconds ?? crossfadeSeconds));

        active = to;
    }

    private IEnumerator Crossfade(AudioSource from, AudioSource to, float dur) {
        if (Mathf.Approximately(dur, 0f)) {
            if (from.isPlaying) from.Stop();
            to.volume = 1f;
            yield break;
        }

        var t = 0f;
        
        var startFrom = Mathf.Clamp01(from ? from.volume : 0f);
        var startTo = Mathf.Clamp01(to.volume);

        while (t < dur) {
            t += DT();
            var u = Mathf.Clamp01(t / dur);
            if (from) from.volume = Mathf.Lerp(startFrom, 0f, u);
            to.volume = Mathf.Lerp(startTo, 1f, u);
            yield return null;
        }

        if (from && from.isPlaying) from.Stop();
        to.volume = 1f;
        fadeRoutine = null;
        yield break;

        float DT() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
    
    public void FadeOut(float seconds) {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeOutRoutine(active, seconds));
    }

    private IEnumerator FadeOutRoutine(AudioSource src, float dur) {
        var t = 0f;
        var start = src ? src.volume : 0f;
        
        while (src && t < dur) {
            t += DT();
            var u = Mathf.Clamp01(t / dur);
            src.volume = Mathf.Lerp(start, 0f, u);
            yield return null;
        }

        if (src) src.Stop();
        fadeRoutine = null;
        yield break;
        
        float DT() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}