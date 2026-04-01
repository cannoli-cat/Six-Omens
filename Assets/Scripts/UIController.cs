using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class UIController : MonoBehaviour {
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private Sprite omenCard;
    [Header("Card Fade")] [SerializeField] private float cardFadeDuration = 0.20f;
    [SerializeField] private float cardStagger = 0.05f;
    [Header("Phone")] [SerializeField] private float ringAmplitudeDeg = 8f;
    [SerializeField] private float ringFrequency = 8f;
    [SerializeField] private float ringTranslatePx = 2f;
    [SerializeField] private float ringDuration = 0.8f;
    [SerializeField] private float ringPause = 0.35f;

    private readonly HashSet<Button> peekingNow = new HashSet<Button>();

    private VisualElement root, phone;
    private VisualElement omenCardContainer, blackOverlay, startGameScreen, pauseMenu, settingsMenu;
    private VisualElement winGameScreen, loseGameScreen;
    private Button willDieButton, willLiveButton, cashOutButton, phoneButton, peekButton, flipOneButton;
    private Button resumeButton, settingsButton, quitButton, settingsQuitButton;
    private Button winToMainMenuButton, loseToMainMenuButton;
    private Button itemTl, itemTr, itemBl, itemBr;
    private Slider masterSlider, musicSlider, sfxSlider;
    private DropdownField displayDropDown;

    private Button[] itemSlots;
    private Button lastPeekedBtn;

    private Label balanceLabel, multiplierLabel, processedLabel, topScoreLabel;
    private Label cutsceneLabel;
    private Label winGameBalance, winGameStats, loseGameBalance, loseGameStats;
    private Label itemTooltip;

    private bool paused;
    private bool cutscenePlaying;
    private bool introAccepted;
    private bool settingsOpen;
    private bool phoneRinging;
    private bool cardsInteractive = true;
    private bool targetPeekMode = false;
    private bool targetFlipMode = false;
    private int targetPeekCharges = 0;
    private int targetFlipCharges = 0;
    private Coroutine phoneRingCo;
    private Coroutine cutsceneRoutine;
    private Dictionary<Button, OmenCardData> omens;
    private InputAction skipAction;

    public Action onCutsceneComplete;
    public Action onPhoneAnswered;
    public Action onGuessWillDie;
    public Action onGuessWillLive;
    public Action onCashOut;
    public Action onPeekRandom;
    public Action onFlipOne;
    public Action<int> onUseItemSlot;

    private const string PMaster = "MASTER_VOL";
    private const string PMusic = "MUSIC_VOL";
    private const string PSfx = "SFX_VOL";

    private const string KMaster = "vol_master";
    private const string KMusic = "vol_music";
    private const string KSfx = "vol_sfx";
    private const string KDisplayMode = "display_mode";

    private const float DefaultVol = 0.5f;
    private const float MuteDB = -80f;

    private const float TtMarginX = 12f;
    private const float TtMarginY = 10f;

    public enum ItemSlot {
        TL = 0,
        TR = 1,
        BL = 2,
        BR = 3
    }

    private void Awake() {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable() {
        root = uiDocument.rootVisualElement;

        blackOverlay = root.Q("BlackOverlay");
        startGameScreen = root.Q("StartGameScreen");
        omenCardContainer = root.Q("OmenCardContainer");
        phone = root.Q("Phone");
        pauseMenu = root.Q("PauseMenu");
        settingsMenu = root.Q("SettingsContainer");
        winGameScreen = root.Q("WinGameScreen");
        loseGameScreen = root.Q("LoseGameScreen");

        if (blackOverlay != null) blackOverlay.pickingMode = PickingMode.Ignore;

        balanceLabel = root.Q<Label>("BalanceLabel");
        cutsceneLabel = root.Q<Label>("CutsceneLabel");
        multiplierLabel = root.Q<Label>("MultiplierLabel");
        processedLabel = root.Q<Label>("ProcessedLabel");
        winGameBalance = root.Q<Label>("WinGameBalance");
        winGameStats = root.Q<Label>("WinGameStats");
        loseGameBalance = root.Q<Label>("LoseGameBalance");
        loseGameStats = root.Q<Label>("LoseGameStats");
        topScoreLabel = root.Q<Label>("TopScoreLabel");

        displayDropDown = settingsMenu.Q<DropdownField>("DisplayDropDown");
        displayDropDown.choices = new List<string> { "Fullscreen", "Windowed", "Borderless" };

        if (Application.platform == RuntimePlatform.WebGLPlayer) {
            displayDropDown?.SetEnabled(false);
        }
        else if (displayDropDown != null) {
            var saved = PlayerPrefs.GetInt(KDisplayMode, int.MinValue);
            var initialMode = (saved != int.MinValue)
                ? (FullScreenMode)saved
                : Screen.fullScreenMode;

            displayDropDown.SetValueWithoutNotify(ModeToChoice(initialMode));

            if (saved != int.MinValue) ApplyDisplayMode(initialMode);

            displayDropDown.RegisterValueChangedCallback(evt => {
                var mode = ChoiceToMode(evt.newValue);
                ApplyDisplayMode(mode);
                PlayerPrefs.SetInt(KDisplayMode, (int)mode);
                PlayerPrefs.Save();
                SFXPlayer.Instance?.PlayButtonClick();
            });
        }

        willDieButton = root.Q<Button>("WillDieButton");
        willLiveButton = root.Q<Button>("WillLiveButton");
        cashOutButton = root.Q<Button>("CashOutButton");
        phoneButton = root.Q<Button>("PhoneButton");
        phoneButton.SetEnabled(false);
        resumeButton = root.Q<Button>("ResumeButton");
        settingsButton = root.Q<Button>("SettingsButton");
        quitButton = root.Q<Button>("QuitButton");
        peekButton = root.Q<Button>("PeekButton");
        flipOneButton = root.Q<Button>("FlipOneButton");
        loseToMainMenuButton = root.Q<Button>("LoseToMainMenuButton");
        winToMainMenuButton = root.Q<Button>("WinToMainMenuButton");
        itemTl = root.Q<Button>("ItemTL");
        itemTr = root.Q<Button>("ItemTR");
        itemBl = root.Q<Button>("ItemBL");
        itemBr = root.Q<Button>("ItemBR");

        itemSlots = new[] { itemTl, itemTr, itemBl, itemBr };

        itemTooltip = new Label();
        itemTooltip.AddToClassList("ItemTooltip");
        itemTooltip.style.position = Position.Absolute;
        itemTooltip.style.display = DisplayStyle.None;
        itemTooltip.style.unityTextAlign = TextAnchor.UpperLeft;
        itemTooltip.pickingMode = PickingMode.Ignore;
        root.Add(itemTooltip);

        for (int i = 0; i < itemSlots.Length; i++) {
            HookItemTooltip(itemSlots[i], i);
        }

        for (var i = 0; i < itemSlots.Length; i++) {
            var iLocal = i;
            var b = itemSlots[i];
            if (b == null) continue;
            b.style.opacity = 0f;
            b.style.width = StyleKeyword.Auto;
            b.style.height = StyleKeyword.Auto;
            b.style.backgroundImage = StyleKeyword.None;
            b.SetEnabled(false);
            b.clicked += () => {
                SFXPlayer.Instance?.PlayItemSelect();
                onUseItemSlot?.Invoke(iLocal);
            };
        }

        settingsQuitButton = root.Q<Button>("ExitButton");

        willDieButton.clicked += OnWillDiePressed;
        willLiveButton.clicked += OnWillLivePressed;
        cashOutButton.clicked += OnCashOutPressed;
        phoneButton.clicked += AnswerPhone;

        phoneButton.RegisterCallback<PointerEnterEvent>(_ => {
            if (phoneRinging) ShowTooltipOver(phoneButton, "Answer the phone");
        });
        phoneButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        phoneButton.RegisterCallback<DetachFromPanelEvent>(_ => HideTooltip());

        resumeButton.clicked += UnPause;
        settingsButton.clicked += OpenSettings;
        quitButton.clicked += ToMainMenu;
        settingsQuitButton.clicked += CloseSettings;
        peekButton.clicked += OnPeekButtonPressed;
        flipOneButton.clicked += OnRevealButtonPressed;
        loseToMainMenuButton.clicked += ToMainMenu;
        winToMainMenuButton.clicked += ToMainMenu;

        SetCashoutInteractive(false);

        masterSlider = settingsMenu.Q<Slider>("MasterVolume");
        musicSlider = settingsMenu.Q<Slider>("MusicVolume");
        sfxSlider = settingsMenu.Q<Slider>("SFXVolume");

        SetupSlider(masterSlider);
        SetupSlider(musicSlider);
        SetupSlider(sfxSlider);

        var vMaster = PlayerPrefs.GetFloat(KMaster, DefaultVol);
        var vMusic = PlayerPrefs.GetFloat(KMusic, DefaultVol);
        var vSfx = PlayerPrefs.GetFloat(KSfx, DefaultVol);

        masterSlider?.SetValueWithoutNotify(vMaster);
        musicSlider?.SetValueWithoutNotify(vMusic);
        sfxSlider?.SetValueWithoutNotify(vSfx);

        ApplyMixer(PMaster, vMaster);
        ApplyMixer(PMusic, vMusic);
        ApplyMixer(PSfx, vSfx);

        masterSlider?.RegisterValueChangedCallback(evt => {
            ApplyMixer(PMaster, evt.newValue);
            PlayerPrefs.SetFloat(KMaster, evt.newValue);
        });
        musicSlider?.RegisterValueChangedCallback(evt => {
            ApplyMixer(PMusic, evt.newValue);
            PlayerPrefs.SetFloat(KMusic, evt.newValue);
        });
        sfxSlider?.RegisterValueChangedCallback(evt => {
            ApplyMixer(PSfx, evt.newValue);
            PlayerPrefs.SetFloat(KSfx, evt.newValue);
        });

        var escape = InputSystem.actions.FindAction("Escape");
        if (escape != null)
            escape.performed += _ => {
                if (settingsOpen) {
                    CloseSettings();
                    return;
                }

                if (!paused) Pause();
                else UnPause();
            };


        skipAction = InputSystem.actions.FindAction("Skip");
        if (skipAction != null) skipAction.performed += OnSkipPerformed;

        omens = new Dictionary<Button, OmenCardData>();
        StartGameCutsceneNow();
    }

    private void OnDisable() {
        willDieButton.clicked -= OnWillDiePressed;
        willLiveButton.clicked -= OnWillLivePressed;
        cashOutButton.clicked -= OnCashOutPressed;
        phoneButton.clicked -= AnswerPhone;
        resumeButton.clicked -= UnPause;
        settingsButton.clicked -= OpenSettings;
        settingsQuitButton.clicked -= CloseSettings;
        peekButton.clicked -= OnPeekButtonPressed;
        flipOneButton.clicked -= OnRevealButtonPressed;
        loseToMainMenuButton.clicked -= ToMainMenu;
        winToMainMenuButton.clicked -= ToMainMenu;
        quitButton.clicked -= ToMainMenu;

        for (var i = 0; i < itemSlots?.Length; i++) {
            var b = itemSlots[i];
            if (b != null) b.clicked -= () => onUseItemSlot?.Invoke(i);
        }

        if (skipAction != null) skipAction.performed -= OnSkipPerformed;
    }

    private void OnDestroy() {
        StopPhoneRing();
        SFXPlayer.Instance?.StopPhoneRing();
    }

    public void PlayWrongGuessFX() {
        StartCoroutine(WrongGuessFXCo());
    }

    private IEnumerator WrongGuessFXCo() {
        SFXPlayer.Instance?.PlayWrongGuess();

        if (blackOverlay != null) {
            var prevOpacity = blackOverlay.style.opacity.value;
            var prevBg = blackOverlay.style.backgroundColor;
            blackOverlay.style.backgroundColor = new Color(0.90196f, 0.27059f, 0.22353f, 0.6f);
            blackOverlay.style.opacity = 1f;
            yield return new WaitForSecondsRealtime(0.08f);
            yield return FadeOut(blackOverlay, 0.25f);
            blackOverlay.style.backgroundColor = prevBg;
            blackOverlay.style.opacity = prevOpacity;
        }

        if (root != null) {
            const float dur = 0.25f;
            var t = 0f;
            while (t < dur) {
                t += Time.unscaledDeltaTime;
                var off = Mathf.Sin(t * 80f) * (1f - t / dur) * 6f;
                root.style.translate = new Translate(off, 0, 0);
                yield return null;
            }

            root.style.translate = new Translate(0, 0, 0);
        }
    }

    public void PlayRightGuessFX() {
        StartCoroutine(RightGuessFXCo());
    }

    private IEnumerator RightGuessFXCo() {
        SFXPlayer.Instance?.PlayRightGuess();

        if (blackOverlay != null) {
            var prevOpacity = blackOverlay.style.opacity.value;
            var prevBg = blackOverlay.style.backgroundColor;
            blackOverlay.style.backgroundColor = new Color(0.38824f, 0.67059f, 0.24706f, 0.55f);
            blackOverlay.style.opacity = 1f;
            yield return new WaitForSecondsRealtime(0.06f);
            yield return FadeOut(blackOverlay, 0.20f);
            blackOverlay.style.backgroundColor = prevBg;
            blackOverlay.style.opacity = prevOpacity;
        }

        yield return PunchLabelsCo(balanceLabel, multiplierLabel);
    }

    private IEnumerator PunchLabelsCo(params VisualElement[] elems) {
        const float dur = 0.28f;
        const float peak = 1.22f;

        var originals = new Scale[elems.Length];
        for (var i = 0; i < elems.Length; i++) {
            if (elems[i] == null) continue;
            originals[i] = elems[i].resolvedStyle.scale;
        }

        var t = 0f;
        while (t < dur) {
            t += Time.unscaledDeltaTime;
            var u = t / dur;
            var s = Mathf.Lerp(peak, 1f, u * u);
            for (var i = 0; i < elems.Length; i++) {
                if (elems[i] == null) continue;
                elems[i].style.scale = new Scale(new Vector3(s, s, 1f));
            }

            yield return null;
        }
        
        for (var i = 0; i < elems.Length; i++) {
            if (elems[i] == null) continue;
            elems[i].style.scale = originals[i];
        }
    }

    public void UpdateBalance(int newBalance) {
        if (balanceLabel == null) return;
        balanceLabel.text = $"${newBalance}";
    }

    private static void SetupSlider(Slider s) {
        if (s == null) return;
        s.lowValue = 0f;
        s.highValue = 1f;
        s.showInputField = false;
    }

    private void ApplyMixer(string param, float slider01) {
        if (mixer == null) return;
        var dB = (slider01 <= 0.0001f) ? MuteDB : Mathf.Log10(slider01) * 20f;
        mixer.SetFloat(param, dB);
    }

    public IEnumerator Next(OmenCardData[] cards) {
        if (omenCardContainer == null) yield break;
        SetCardsInteractive(false);

        var hadOld = omenCardContainer.childCount > 0;

        if (hadOld) {
            for (var i = 0; i < omenCardContainer.childCount; i++) {
                var child = omenCardContainer[i];
                child.style.opacity = 1f;
                yield return FadeOut(child, cardFadeDuration);
                if (cardStagger > 0f) yield return new WaitForSecondsRealtime(cardStagger);
            }

            omenCardContainer.Clear();
        }

        omens.Clear();
        lastPeekedBtn = null;

        if (cards is { Length: > 0 }) {
            for (var i = 0; i < cards.Length; i++) {
                var cardButton = new Button();
                cardButton.AddToClassList("OmenCard");
                cardButton.style.backgroundImage = new StyleBackground(omenCard);
                cardButton.style.opacity = 0f;

                cardButton.clicked += () => ChooseOmen(cardButton);

                omenCardContainer.Add(cardButton);
                omens.Add(cardButton, cards[i]);
            }

            for (var i = 0; i < omenCardContainer.childCount; i++) {
                var child = omenCardContainer[i];
                SFXPlayer.Instance?.PlayCardFlip();
                yield return FadeIn(child, cardFadeDuration);
                if (cardStagger > 0f) yield return new WaitForSecondsRealtime(cardStagger);
            }
        }

        SetCardsInteractive(true);
    }

    private void OnSkipPerformed(InputAction.CallbackContext ctx) {
        if (cutscenePlaying) {
            introAccepted = true;
        }
    }

    private void StartGameCutsceneNow() {
        if (cutsceneRoutine != null) StopCoroutine(cutsceneRoutine);
        cutsceneRoutine = StartCoroutine(ShowHowToPlay());
    }

    private IEnumerator ShowHowToPlay() {
        cutscenePlaying = true;
        introAccepted = false;

        if (blackOverlay != null) {
            blackOverlay.style.opacity = 1f;
            blackOverlay.pickingMode = PickingMode.Ignore;
        }

        if (startGameScreen != null) startGameScreen.style.display = DisplayStyle.Flex;

        if (cutsceneLabel != null) {
            cutsceneLabel.text =
                "HOW TO PLAY\n\n" +
                "GOAL\n" +
                "Answer the phone and decide: Will they <color=#e64539>DIE</color> or <color=#63ab3f>LIVE</color>?\n\n" +
                "CARDS\n" +
                "Each round shows hidden omens(cards): <color=#e64539>skulls</color> = <color=#e64539>DIE</color>, <color=#63ab3f>halos</color> = <color=#63ab3f>LIVE</color>.\n" +
                "More <color=#e64539>skulls</color> than <color=#63ab3f>halos</color> = <color=#e64539>DIE</color>. More <color=#63ab3f>halos</color> than <color=#e64539>skulls</color> = <color=#63ab3f>LIVE</color>.\n" +
                "<color=#f0b541>There is never a tie.</color>\n\n" +
                "TIMER\n" +
                "Make your guess before the timer hits <color=#f0b541>0</color>.\n" +
                "<color=#e64539>No guess = wrong.</color>\n\n" +
                "TOOLS\n" +
                "Peek: briefly reveals a random card (<color=#f0b541>costs 6s</color>).\n" +
                "Flip: permanently reveals 1 card (<color=#f0b541>costs 10s</color>).\n" +
                "Items: appear sometimes, you can hover over them to see what they do.\n\n" +
                "MONEY\n" +
                "<color=#63ab3f>Correct = earn $</color>. <color=#e64539>Wrong/timeout = lose $</color>.\n" +
                "<color=#f0b541>Streaks</color> and faster guesses increase your <color=#f0b541>payout</color>.\n\n";


            cutsceneLabel.style.opacity = 1f;
        }

        yield return new WaitUntil(() => introAccepted);

        if (startGameScreen != null) {
            yield return FadeOut(startGameScreen, 0.8f);
            startGameScreen.style.display = DisplayStyle.None;
        }

        if (blackOverlay != null) yield return FadeOut(blackOverlay, 0.8f);

        cutscenePlaying = false;
        cutsceneRoutine = null;
        onCutsceneComplete?.Invoke();
    }

    private void Pause() {
        paused = true;
        GameManager.Instance?.Pause();

        if (pauseMenu != null) pauseMenu.style.display = settingsOpen ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void UnPause() {
        SFXPlayer.Instance?.PlayButtonClick();
        paused = false;
        GameManager.Instance?.UnPause();

        if (pauseMenu != null) pauseMenu.style.display = DisplayStyle.None;
        if (settingsMenu != null) settingsMenu.style.display = DisplayStyle.None;
        settingsOpen = false;
    }

    private static IEnumerator FadeIn(VisualElement v, float duration = 1f) {
        var elapsed = 0f;

        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            v.style.opacity = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        v.style.opacity = 1f;
    }

    private static IEnumerator FadeOut(VisualElement v, float duration = 1f) {
        var elapsed = 0f;
        while (elapsed < duration) {
            elapsed += Time.unscaledDeltaTime;
            v.style.opacity = 1f - Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        v.style.opacity = 0f;
    }

    private void OnPeekButtonPressed() {
        SFXPlayer.Instance?.PlayButtonClick();
        onPeekRandom?.Invoke();
    }

    private void OnRevealButtonPressed() {
        SFXPlayer.Instance?.PlayButtonClick();
        onFlipOne?.Invoke();
    }

    private void ChooseOmen(Button button) {
        if (!cardsInteractive) return;

        if (targetFlipMode) {
            if (!omens.TryGetValue(button, out var omen)) return;
            if (omen.isRevealed) return;

            FlipSpecific(button);
            targetFlipCharges--;
            if (targetFlipCharges <= 0) StopTargetFlip();
            return;
        }

        if (targetPeekMode) {
            if (!omens.TryGetValue(button, out var omen)) return;
            if (omen.isRevealed) return;

            PeekSpecific(button, 1f);
            targetPeekCharges--;
            if (targetPeekCharges <= 0) StopTargetPeek();
        }
    }

    public void Reveal(int amount) {
        if (omens == null || omens.Count == 0 || amount <= 0) return;

        var candidates = new List<Button>();
        foreach (var kv in omens) {
            if (!kv.Value.isRevealed) candidates.Add(kv.Key);
        }

        if (candidates.Count == 0) return;

        var toReveal = Mathf.Min(amount, candidates.Count);

        for (var i = 0; i < toReveal; i++) {
            var idx = UnityEngine.Random.Range(i, candidates.Count);
            (candidates[i], candidates[idx]) = (candidates[idx], candidates[i]);

            var btn = candidates[i];
            if (!omens.TryGetValue(btn, out var data)) continue;
            if (data.isRevealed) continue;

            data.isRevealed = true;
            omens[btn] = data;

            SFXPlayer.Instance?.PlayCardFlip();
            btn.style.backgroundImage = new StyleBackground(data.icon);
            btn.SetEnabled(false);
        }

        if (!HasUnrevealed()) {
            GameManager.Instance?.NotifyAllRevealed();
        }
    }

    public void PeekRandom(float time) {
        if (time <= 0f || omens == null || omens.Count == 0) return;
        StartCoroutine(PeekRandomCo(time));
    }

    private IEnumerator PeekRandomCo(float time) {
        var candidates = new List<Button>(omens.Count);
        foreach (var kv in omens) {
            if (!kv.Value.isRevealed && !peekingNow.Contains(kv.Key)) {
                candidates.Add(kv.Key);
            }
        }

        if (candidates.Count == 0) yield break;

        if (lastPeekedBtn != null && candidates.Count > 1) {
            candidates.Remove(lastPeekedBtn);
            if (candidates.Count == 0) {
                foreach (var kv in omens) {
                    if (!kv.Value.isRevealed && !peekingNow.Contains(kv.Key)) {
                        candidates.Add(kv.Key);
                    }
                }

                if (candidates.Count == 0) yield break;
            }
        }

        var btn = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        if (!peekingNow.Add(btn)) yield break;
        lastPeekedBtn = btn;

        if (!omens.TryGetValue(btn, out var data)) {
            peekingNow.Remove(btn);
            yield break;
        }

        var original = btn.style.backgroundImage;
        btn.style.backgroundImage = new StyleBackground(data.icon);
        SFXPlayer.Instance?.PlayCardFlip();

        var t = 0f;
        while (t < time) {
            t += Time.unscaledDeltaTime;
            if (omens.TryGetValue(btn, out var live) && live.isRevealed) break;
            yield return null;
        }

        if (omens.TryGetValue(btn, out var d) && !d.isRevealed) {
            btn.style.backgroundImage = original;
            SFXPlayer.Instance?.PlayCardFlip();
        }

        peekingNow.Remove(btn);
    }

    private void OnWillDiePressed() {
        SFXPlayer.Instance?.PlayButtonClick();
        onGuessWillDie?.Invoke();
    }

    private void OnWillLivePressed() {
        SFXPlayer.Instance?.PlayButtonClick();
        onGuessWillLive?.Invoke();
    }

    private void OnCashOutPressed() {
        SFXPlayer.Instance?.PlayButtonClick();
        onCashOut?.Invoke();
    }

    private void AnswerPhone() {
        if (!phoneRinging) return;

        StopPhoneRing();
        SFXPlayer.Instance?.PlayPhonePickup();

        onPhoneAnswered?.Invoke();
    }

    public void StartPhoneRing() {
        if (phoneRinging || phone == null || phoneButton == null) return;
        phoneRinging = true;
        phone.style.transformOrigin = new TransformOrigin(50f, 0f, 0f);
        phoneButton.SetEnabled(true);
        SetButtonsInteractive(false);
        SFXPlayer.Instance?.PlayPhoneRing();
        phoneRingCo = StartCoroutine(PhoneRingLoop());
    }

    public void StopPhoneRing() {
        if (!phoneRinging) return;
        SFXPlayer.Instance?.StopPhoneRing();
        phoneRinging = false;

        if (phoneRingCo != null) {
            StopCoroutine(phoneRingCo);
            phoneRingCo = null;
        }

        phone.style.rotate = new Rotate(0f);
        phone.style.translate = new Translate(0, 0, 0);

        phoneButton?.SetEnabled(false);
        HideTooltip();
    }

    private IEnumerator PhoneRingLoop() {
        var t = 0f;
        while (phoneRinging) {
            var end = Time.unscaledTime + ringDuration;

            while (phoneRinging && Time.unscaledTime < end) {
                t += Time.unscaledDeltaTime;
                var s = Mathf.Sin(t * Mathf.PI * 2f * ringFrequency);
                phone.style.rotate = new Rotate(new Angle(s * ringAmplitudeDeg, AngleUnit.Degree));
                phone.style.translate = new Translate(Mathf.Sin(t * Mathf.PI * ringFrequency) * ringTranslatePx, 0, 0);
                yield return null;
            }

            phone.style.rotate = new Rotate(0f);
            phone.style.translate = new Translate(0, 0, 0);
            var until = Time.unscaledTime + ringPause;
            while (phoneRinging && Time.unscaledTime < until) yield return null;
        }
    }

    private void OpenSettings() {
        if (settingsMenu == null) return;
        SFXPlayer.Instance?.PlayButtonClick();

        settingsOpen = true;

        if (pauseMenu != null) pauseMenu.style.display = DisplayStyle.None;

        settingsMenu.style.display = DisplayStyle.Flex;
    }

    private void CloseSettings() {
        if (settingsMenu == null) return;
        SFXPlayer.Instance?.PlayButtonClick();

        settingsOpen = false;
        settingsMenu.style.display = DisplayStyle.None;

        if (paused && pauseMenu != null) pauseMenu.style.display = DisplayStyle.Flex;
    }

    public IEnumerator ClearCards(float fadeDuration = 0.2f, float stagger = 0.05f) {
        if (omenCardContainer == null || omenCardContainer.childCount == 0) yield break;

        for (var i = 0; i < omenCardContainer.childCount; i++) {
            var child = omenCardContainer[i];
            child.style.opacity = 1f;
            yield return FadeOut(child, fadeDuration);
            if (stagger > 0f) yield return new WaitForSecondsRealtime(stagger);
        }

        omenCardContainer.Clear();
    }

    public void SetCardsInteractive(bool on) => cardsInteractive = on;

    public void WinGame(int currentBalance, int newHighScore, int numDied, int numLived, int clientsProcessed) {
        StopPhoneRing();
        MusicPlayer.Instance?.PlayIntroCutsceneMusic();
        winGameScreen.style.display = DisplayStyle.Flex;
        winGameBalance.text = $"Balance: ${currentBalance}<br>Lifetime High Score: ${newHighScore}";
        winGameStats.text = $"You made a total of {clientsProcessed} decisions.<br>{numDied} died.<br>{numLived} lived";
    }

    public void LoseGame(int currentBalance, int newHighScore, int numDied, int numLived, int clientsProcessed) {
        StopPhoneRing();
        MusicPlayer.Instance?.PlayIntroCutsceneMusic();
        loseGameScreen.style.display = DisplayStyle.Flex;
        loseGameBalance.text = $"Balance: ${currentBalance}<br>Lifetime High Score: ${newHighScore}";
        loseGameStats.text =
            $"You made a total of {clientsProcessed} decisions.<br>{numDied} died.<br>{numLived} lived.";
    }

    public void UpdateMultiplier(float multiplier) {
        if (multiplierLabel == null) return;
        multiplierLabel.text = $"Multiplier<br>x{multiplier:0.00}";
    }

    public void UpdateProcessed(int processed) {
        if (processedLabel == null) return;
        processedLabel.text = $"Guesses Made<br>#{processed}";
    }

    public void UpdateHighScore(int score) {
        topScoreLabel.text = $"High Score<br>${score}";
    }

    private void ToMainMenu() {
        SFXPlayer.Instance?.PlayButtonClick();

        StopPhoneRing();

        GameManager.Instance?.UnPause();   
        Time.timeScale = 1f;               

        MusicPlayer.Instance?.PlayTitleMusic();
        SceneManager.LoadScene(0);
    }
    
    public bool AddInventoryIconAt(int slot, Sprite icon, string tooltip, float fade = 0.2f) {
        if (itemSlots == null) {
            Debug.LogWarning("UI: itemSlots is null");
            return false;
        }

        if (slot < 0 || slot >= itemSlots.Length) {
            Debug.LogWarning($"UI: slot {slot} OOB");
            return false;
        }

        if (!icon) {
            Debug.LogWarning("UI: icon null");
            return false;
        }

        var b = itemSlots[slot];
        if (b == null) {
            Debug.LogWarning($"UI: itemSlots[{slot}] Button is null (check UXML ids)");
            return false;
        }

        b.tooltip = tooltip ?? "";
        b.style.width = new Length(icon.rect.width, LengthUnit.Pixel);
        b.style.height = new Length(icon.rect.height, LengthUnit.Pixel);
        b.style.backgroundImage = new StyleBackground(icon);
        b.pickingMode = PickingMode.Position;

        b.style.opacity = 0f;
        StartCoroutine(FadeIn(b, fade));
        return true;
    }

    public void RemoveInventoryIcon(int slot, float fade = 0.2f) {
        if (itemSlots == null || slot < 0 || slot >= itemSlots.Length) return;
        var b = itemSlots[slot];
        if (b == null) return;
        StartCoroutine(RemoveInventoryIconCo(b, fade));
    }

    private IEnumerator RemoveInventoryIconCo(VisualElement b, float fade) {
        yield return FadeOut(b, fade);
        b.style.backgroundImage = StyleKeyword.None;
        b.style.width = StyleKeyword.Auto;
        b.style.height = StyleKeyword.Auto;
        b.tooltip = "";
        b.SetEnabled(false);
        b.style.opacity = 0f;
    }

    private void SetButtonsInteractive(bool toggle) {
        willDieButton.SetEnabled(toggle);
        willLiveButton.SetEnabled(toggle);
        peekButton.SetEnabled(toggle);
        flipOneButton.SetEnabled(toggle);
        foreach (var b in itemSlots) {
            b.SetEnabled(toggle && b.style.backgroundImage.keyword != StyleKeyword.None);
        }
    }

    public void SetCashoutInteractive(bool toggle) {
        cashOutButton.SetEnabled(toggle);
    }

    public void SetDecisionControlsEnabled(bool on) {
        willDieButton?.SetEnabled(on);
        willLiveButton?.SetEnabled(on);
        peekButton?.SetEnabled(on);
        flipOneButton?.SetEnabled(on);
        if (itemSlots != null) {
            foreach (var b in itemSlots) {
                if (b == null) continue;
                b.SetEnabled(on && b.style.backgroundImage.keyword != StyleKeyword.None);
            }
        }
    }

    private void HookItemTooltip(Button b, int slotIndex) {
        if (b == null) return;

        b.RegisterCallback<PointerEnterEvent>(evt => {
            var text = GameManager.Instance?.GetItemDescriptionAtSlot(slotIndex);
            if (string.IsNullOrEmpty(text)) return;
            ShowTooltip(text, evt.position);
        });

        b.RegisterCallback<PointerMoveEvent>(evt => {
            if (itemTooltip.style.display == DisplayStyle.Flex)
                MoveTooltip(evt.position);
        });

        b.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        b.RegisterCallback<DetachFromPanelEvent>(_ => HideTooltip());
        b.RegisterCallback<FocusOutEvent>(_ => HideTooltip());
    }

    private void ShowTooltip(string text, Vector2 panelPos) {
        if (itemTooltip == null) return;
        itemTooltip.text = text;
        itemTooltip.style.display = DisplayStyle.Flex;
        MoveTooltip(panelPos);
    }

    private void MoveTooltip(Vector2 panelPos) {
        if (itemTooltip == null) return;

        var x = panelPos.x + TtMarginX;
        var y = panelPos.y + TtMarginY;

        var panelW = root.resolvedStyle.width;
        var panelH = root.resolvedStyle.height;

        var w = itemTooltip.resolvedStyle.width;
        var h = itemTooltip.resolvedStyle.height;

        if (x + w > panelW) x = Mathf.Max(0, panelW - w - 2f);
        if (y + h > panelH) y = Mathf.Max(0, panelH - h - 2f);

        itemTooltip.style.left = x;
        itemTooltip.style.top = y;
    }

    private void HideTooltip() {
        if (itemTooltip == null) return;
        itemTooltip.style.display = DisplayStyle.None;
    }

    public void StartTargetPeek(int charges) {
        targetPeekMode = true;
        targetPeekCharges = charges;

        peekButton?.SetEnabled(false);
        flipOneButton?.SetEnabled(false);
        willDieButton?.SetEnabled(false);
        willLiveButton?.SetEnabled(false);

        foreach (var item in itemSlots) {
            item.SetEnabled(false);
        }

        foreach (var kv in omens) {
            kv.Key.RemoveFromClassList("OmenCard");
            kv.Key.AddToClassList("TargetPeekArmed");
        }
    }

    private void StopTargetPeek() {
        targetPeekMode = false;
        targetPeekCharges = 0;

        peekButton?.SetEnabled(true);
        flipOneButton?.SetEnabled(true);
        willDieButton?.SetEnabled(true);
        willLiveButton?.SetEnabled(true);

        foreach (var item in itemSlots) {
            item.SetEnabled(true);
        }

        foreach (var kv in omens) {
            kv.Key.RemoveFromClassList("TargetPeekArmed");
            kv.Key.AddToClassList("OmenCard");
        }
    }

    public void StartTargetFlip(int charges) {
        targetFlipMode = true;
        targetFlipCharges = charges;

        peekButton?.SetEnabled(false);
        flipOneButton?.SetEnabled(false);
        willDieButton?.SetEnabled(false);
        willLiveButton?.SetEnabled(false);

        foreach (var item in itemSlots) {
            item.SetEnabled(false);
        }

        foreach (var kv in omens) {
            kv.Key.RemoveFromClassList("OmenCard");
            kv.Key.AddToClassList("TargetPeekArmed");
        }
    }

    private void StopTargetFlip() {
        targetFlipMode = false;
        targetFlipCharges = 0;

        peekButton?.SetEnabled(true);
        flipOneButton?.SetEnabled(true);
        willDieButton?.SetEnabled(true);
        willLiveButton?.SetEnabled(true);

        foreach (var item in itemSlots)
            item.SetEnabled(true);

        foreach (var kv in omens) {
            kv.Key.RemoveFromClassList("TargetPeekArmed");
            kv.Key.AddToClassList("OmenCard");
        }
    }

    private void PeekSpecific(Button btn, float time) {
        if (time <= 0f || btn == null) return;
        StartCoroutine(PeekSpecificCo(btn, time));
    }

    private IEnumerator PeekSpecificCo(Button btn, float time) {
        if (!omens.TryGetValue(btn, out var data)) yield break;
        if (data.isRevealed) yield break;

        if (!peekingNow.Add(btn)) yield break;

        var original = btn.style.backgroundImage;
        btn.style.backgroundImage = new StyleBackground(data.icon);
        SFXPlayer.Instance?.PlayCardFlip();

        var t = 0f;
        while (t < time) {
            t += Time.unscaledDeltaTime;
            if (omens.TryGetValue(btn, out var live) && live.isRevealed) break;
            yield return null;
        }

        if (omens.TryGetValue(btn, out var d) && !d.isRevealed) {
            btn.style.backgroundImage = original;
            SFXPlayer.Instance?.PlayCardFlip();
        }

        peekingNow.Remove(btn);
    }

    private void FlipSpecific(Button btn) {
        if (btn == null) return;
        if (!omens.TryGetValue(btn, out var data)) return;
        if (data.isRevealed) return;

        data.isRevealed = true;
        omens[btn] = data;

        btn.style.backgroundImage = new StyleBackground(data.icon);
        btn.SetEnabled(false);
        SFXPlayer.Instance?.PlayCardFlip();

        if (!HasUnrevealed()) {
            GameManager.Instance?.NotifyAllRevealed();
        }
    }

    public bool HasUnrevealed() {
        if (omens == null) return false;
        foreach (var kv in omens)
            if (!kv.Value.isRevealed)
                return true;
        return false;
    }

    private void ShowTooltipOver(VisualElement target, string text) {
        if (itemTooltip == null || target == null) return;

        itemTooltip.text = text;
        itemTooltip.style.display = DisplayStyle.Flex;

        var r = target.worldBound;

        var w = itemTooltip.resolvedStyle.width;
        var h = itemTooltip.resolvedStyle.height;

        var x = r.xMin + (r.width - w) * 0.5f;
        var y = r.yMin - h - 6f;

        var panelW = root.resolvedStyle.width;
        var panelH = root.resolvedStyle.height;
        if (x + w > panelW) x = Mathf.Max(0, panelW - w - 2f);
        if (y + h > panelH) y = Mathf.Max(0, panelH - h - 2f);
        if (x < 0) x = 0;
        if (y < 0) y = 0;

        itemTooltip.style.left = x;
        itemTooltip.style.top = y;
    }

    private string ModeToChoice(FullScreenMode mode) {
        switch (mode) {
            case FullScreenMode.ExclusiveFullScreen: return "Fullscreen";
            case FullScreenMode.FullScreenWindow: return "Borderless";
            case FullScreenMode.Windowed:
            case FullScreenMode.MaximizedWindow: return "Windowed";
            default: return "Windowed";
        }
    }

    private FullScreenMode ChoiceToMode(string choice) {
        switch (choice) {
            case "Fullscreen":
#if UNITY_STANDALONE_OSX
            return FullScreenMode.FullScreenWindow;
#else
                return FullScreenMode.ExclusiveFullScreen;
#endif
            case "Borderless": return FullScreenMode.FullScreenWindow;
            case "Windowed":
            default: return FullScreenMode.Windowed;
        }
    }

    private void ApplyDisplayMode(FullScreenMode mode) {
        var desktop = Screen.currentResolution;

        if (mode == FullScreenMode.Windowed) {
            int w = Mathf.Min(1280, desktop.width);
            int h = Mathf.Min(720, desktop.height);
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
        }
        else {
#if UNITY_2022_2_OR_NEWER
            Screen.SetResolution(desktop.width, desktop.height, mode, desktop.refreshRateRatio);
#else
        Screen.SetResolution(desktop.width, desktop.height, mode);
#endif
        }
    }

    private static bool IsVisible(VisualElement ve) =>
        ve != null && ve.style.display == DisplayStyle.Flex;
}