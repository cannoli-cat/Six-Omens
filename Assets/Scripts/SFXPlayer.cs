using UnityEngine;
using UnityEngine.Audio;

public class SFXPlayer : MonoBehaviour {
    [Header("Routing")] [SerializeField] private AudioMixerGroup sfxOutput;

    [SerializeField] private AudioClip phoneRingClip,
        phonePickupClip,
        cardFlipClip,
        buttonClickClip,
        clockTickClip,
        clockDingClip,
        wrongGuessClip,
        rightGuessClip;

    [Header("One-shot Variance")] [Range(0f, 0.5f)] [SerializeField]
    private float pitchJitter = 0.06f;

    [Range(0f, 0.5f)] [SerializeField] private float clockPitchJitter = 0.03f;

    public static SFXPlayer Instance { get; private set; }

    private AudioSource oneShotSource;
    private AudioSource tickSource;
    private AudioSource ringLoopSource;
    private AudioSource loopSourceB;

    void Awake() {
        if (Instance && Instance != this) {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        oneShotSource = gameObject.AddComponent<AudioSource>();
        tickSource = gameObject.AddComponent<AudioSource>();
        ringLoopSource = gameObject.AddComponent<AudioSource>();
        loopSourceB = gameObject.AddComponent<AudioSource>();

        foreach (var src in new[] { oneShotSource, tickSource, ringLoopSource, loopSourceB }) {
            src.playOnAwake = false;
            src.outputAudioMixerGroup = sfxOutput;
            src.spatialBlend = 0f;
        }

        ringLoopSource.loop = true;
        loopSourceB.loop = true;
    }

    public void PlayCardFlip() => PlayOneVar(oneShotSource, cardFlipClip, pitchJitter);
    public void PlayItemSelect() => PlayButtonClick();
    public void PlayButtonClick() => PlayOneVar(oneShotSource, buttonClickClip, pitchJitter);
    public void PlayPhonePickup() => PlayOne(oneShotSource, phonePickupClip);
    public void PlayClockDing() => PlayOne(oneShotSource, clockDingClip);
    public void PlayWrongGuess() => PlayOne(oneShotSource, wrongGuessClip);
    public void PlayRightGuess() => PlayOne(oneShotSource, rightGuessClip);
    public void PlayClockTick() => PlayOneVar(tickSource, clockTickClip, clockPitchJitter);

    private static void PlayOne(AudioSource src, AudioClip clip) {
        if (!clip) {
            Debug.LogWarning("[SFX] Missing oneshot clip");
            return;
        }

        src.pitch = 1f;
        src.PlayOneShot(clip);
    }

    private static void PlayOneVar(AudioSource src, AudioClip clip, float jitter) {
        if (!clip) {
            Debug.LogWarning("[SFX] Missing oneshot clip");
            return;
        }

        src.pitch = 1f + Random.Range(-jitter, jitter);
        src.PlayOneShot(clip);
        src.pitch = 1f;
    }

    public void PlayPhoneRing() {
        if (!phoneRingClip) {
            Debug.LogWarning("[SFX] Missing phoneRingClip");
            return;
        }

        if (ringLoopSource.isPlaying && ringLoopSource.clip == phoneRingClip) return;
        ringLoopSource.clip = phoneRingClip;
        ringLoopSource.Play();
    }

    public void StopPhoneRing() => ringLoopSource.Stop();
}