using UnityEngine;
using System;
using System.Collections;

public class Clock : MonoBehaviour {
    [Header("Countdown Settings")]
    [Tooltip("Default countdown length in seconds (used by StartTimer() with no args).")]
    [SerializeField]
    private int defaultSeconds = 30;

    [Tooltip("1 = real time. >1 = faster, <1 = slower.")] [SerializeField]
    private float secondsPerRealSecond = 1f;

    [Header("Digits & Color")] [SerializeField]
    private Color color = Color.white;

    [SerializeField] private Sprite[] digits; // 0..9
    [SerializeField] private SpriteRenderer[] digitRenderers;
    [SerializeField] private SpriteRenderer[] dividerRenderer;

    [Header("Blinking (milestones)")] [Tooltip("Seconds between on/off during a blink burst")] [SerializeField]
    private float blinkInterval = 0.12f;

    [Tooltip("How many on/off toggles per blink burst")] [SerializeField]
    private int blinkToggles = 8;

    [Tooltip("Blink once when timer crosses half remaining (from above)")] [SerializeField]
    private bool blinkAtHalf = true;

    [Tooltip("Blink once when entering last 10 seconds")] [SerializeField]
    private bool blinkAtLastTen = true;

    public event Action OnTimeUp;

    public bool IsRunning { get; private set; }
    public int RemainingSeconds { get; private set; }
    public bool IsFrozen  { get; private set; }

    private float accum;
    private bool halfBlinkDone;
    private bool lastTenBlinkDone;
    private Coroutine blinkCo;
    private Coroutine freezeCo;

    private void Awake() {
        if (digitRenderers != null)
            foreach (var r in digitRenderers)
                if (r)
                    r.color = color;

        if (dividerRenderer != null)
            foreach (var r in dividerRenderer)
                if (r)
                    r.color = color;

        RemainingSeconds = Mathf.Max(0, defaultSeconds);
        SetClockSpritesFromSeconds(RemainingSeconds);
        SetAllVisible(true);
    }

    public void StartTimer() => StartTimer(defaultSeconds);

    public void StartTimer(int seconds) {
        if (blinkCo != null) {
            StopCoroutine(blinkCo);
            blinkCo = null;
        }

        RemainingSeconds = Mathf.Max(0, seconds);
        IsRunning = RemainingSeconds > 0;
        accum = 0f;

        halfBlinkDone = false;
        lastTenBlinkDone = false;

        SetClockSpritesFromSeconds(RemainingSeconds);
        SetAllVisible(true);
    }
    
    public void ResetTimer() => ResetTimer(defaultSeconds);

    public void ResetTimer(int seconds) {
        if (blinkCo != null) { StopCoroutine(blinkCo); blinkCo = null; }
        IsRunning = false;
        accum = 0f;
        halfBlinkDone = false;
        lastTenBlinkDone = false;

        RemainingSeconds = Mathf.Clamp(seconds, 0, 99 * 60 + 59);
        SetClockSpritesFromSeconds(RemainingSeconds);
        SetAllVisible(true);
    }
    
    public void PauseTimer() => IsRunning = false;

    public void PauseTimerForSeconds(float seconds) {
        StartCoroutine(PauseTimerSeconds(seconds));
    }

    private IEnumerator PauseTimerSeconds(float seconds) {
        PauseTimer();
        yield return new WaitForSeconds(seconds);
        ResumeTimer();
    }

    public void ResumeTimer() {
        if (RemainingSeconds > 0) IsRunning = true;
    }
    
    public void FreezeForSeconds(float seconds) {
        if (freezeCo != null) StopCoroutine(freezeCo);
        freezeCo = StartCoroutine(FreezeCo(seconds));
    }

    private IEnumerator FreezeCo(float seconds) {
        IsFrozen = true;
        yield return new WaitForSecondsRealtime(seconds);
        IsFrozen = false;
        freezeCo = null;
    }

    public void AddSeconds(int delta) {
        RemainingSeconds = Mathf.Clamp(RemainingSeconds + delta, 0, 99 * 60 + 59);
        SetClockSpritesFromSeconds(RemainingSeconds);
    }

    private void Update() {
        if (!IsRunning || IsFrozen) return; 
        accum += Time.deltaTime * Mathf.Max(0f, secondsPerRealSecond);
        while (accum >= 1f && IsRunning && !IsFrozen) {
            accum -= 1f;
            TickOneSecond();
        }
    }

    private void TickOneSecond() {
        if (RemainingSeconds <= 0) return;

        var prev = RemainingSeconds;
        RemainingSeconds = Mathf.Max(0, RemainingSeconds - 1);
        SetClockSpritesFromSeconds(RemainingSeconds);

        SFXPlayer.Instance?.PlayClockTick();

        if (blinkAtHalf && !halfBlinkDone) {
            var half = Mathf.Max(1, Mathf.CeilToInt(defaultSeconds * 0.5f));
            if (prev > half && RemainingSeconds <= half) {
                halfBlinkDone = true;
                TriggerBlink(); 
            }
        }

        if (blinkAtLastTen && !lastTenBlinkDone) {
            if (prev > 10 && RemainingSeconds <= 10) {
                lastTenBlinkDone = true;
                TriggerBlink(); 
            }
        }

        if (RemainingSeconds <= 0) {
            IsRunning = false;
            SetClockSpritesFromSeconds(0);
            if (blinkCo != null) { StopCoroutine(blinkCo); blinkCo = null; }
            SetAllVisible(true);
            OnTimeUp?.Invoke();
        }
    }


    private void SetClockSpritesFromSeconds(int totalSeconds) {
        totalSeconds = Mathf.Clamp(totalSeconds, 0, 99 * 60 + 59);
        var mm = totalSeconds / 60;
        var ss = totalSeconds % 60;

        var mTens = mm / 10;
        var mOnes = mm % 10;
        var sTens = ss / 10;
        var sOnes = ss % 10;

        if (digits == null || digits.Length < 10) return;
        if (digitRenderers == null || digitRenderers.Length < 4) return;

        digitRenderers[0].sprite = digits[mTens];
        digitRenderers[1].sprite = digits[mOnes];
        digitRenderers[2].sprite = digits[sTens];
        digitRenderers[3].sprite = digits[sOnes];
    }

    private void SetColonVisible(bool v) {
        if (dividerRenderer == null) return;
        foreach (var div in dividerRenderer)
            if (div)
                div.enabled = v;
    }

    private void SetAllVisible(bool v) {
        if (digitRenderers != null)
            foreach (var r in digitRenderers)
                if (r)
                    r.enabled = v;
        SetColonVisible(v);
    }

    private void TriggerBlink() {
        SFXPlayer.Instance?.PlayClockDing();

        if (blinkCo != null) StopCoroutine(blinkCo);
        blinkCo = StartCoroutine(BlinkBurst());
    }

    private IEnumerator BlinkBurst() {
        SetAllVisible(true);
        for (var i = 0; i < blinkToggles; i++) {
            var on = (i % 2 == 0);
            SetAllVisible(on);
            yield return new WaitForSeconds(blinkInterval);
        }

        SetAllVisible(true);
        blinkCo = null;
    }
}