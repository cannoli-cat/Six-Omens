using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

public class MainMenuController : MonoBehaviour {
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private AudioMixer mixer;

    private VisualElement root, settingsContainer;
    private Slider masterSlider, musicSlider, sfxSlider;
    private Button playButton, settingsButton, quitButton, exitSettingsButton;
    private DropdownField displayDropDown;
    private InputAction escapeAction;

    private const string PMaster = "MASTER_VOL";
    private const string PMusic = "MUSIC_VOL";
    private const string PSfx = "SFX_VOL";

    private const string KMaster = "vol_master";
    private const string KMusic = "vol_music";
    private const string KSfx = "vol_sfx";
    private const string KDisplayMode = "display_mode";

    private const float DefaultVol = 0.5f;
    private const float MuteDB = -80f;

    private void Awake() {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable() {
        root = uiDocument.rootVisualElement;

        settingsContainer = root.Q<VisualElement>("SettingsContainer");
        playButton = root.Q<Button>("PlayButton");
        settingsButton = root.Q<Button>("SettingsButton");
        exitSettingsButton = root.Q<Button>("ExitButton");
        quitButton = root.Q<Button>("QuitButton");

        displayDropDown = settingsContainer.Q<DropdownField>("DisplayDropDown");
        displayDropDown.choices = new List<string> { "Fullscreen", "Windowed", "Borderless" };

        if (Application.platform == RuntimePlatform.WebGLPlayer) {
            displayDropDown.SetEnabled(false);
        }
        else {
            var saved = PlayerPrefs.GetInt(KDisplayMode, int.MinValue);
            var initialMode = (saved != int.MinValue)
                ? (FullScreenMode)saved
                : Screen.fullScreenMode;

            var initialChoice = ModeToChoice(initialMode);
            displayDropDown.SetValueWithoutNotify(initialChoice);

            if (saved != int.MinValue) {
                ApplyDisplayMode(initialMode);
            }

            displayDropDown.RegisterValueChangedCallback(evt => {
                var mode = ChoiceToMode(evt.newValue);
                ApplyDisplayMode(mode);
                PlayerPrefs.SetInt(KDisplayMode, (int)mode);
                PlayerPrefs.Save();
                SFXPlayer.Instance?.PlayButtonClick();
            });
        }

        masterSlider = settingsContainer.Q<Slider>("MasterVolume");
        musicSlider = settingsContainer.Q<Slider>("MusicVolume");
        sfxSlider = settingsContainer.Q<Slider>("SFXVolume");

        playButton.clicked += OnPlayClicked;
        settingsButton.clicked += OnSettingsClicked;
        exitSettingsButton.clicked += CloseSettings;

        if (Application.platform == RuntimePlatform.WebGLPlayer)
            quitButton.style.display = DisplayStyle.None;
        else
            quitButton.clicked += OnQuitClicked;

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

        escapeAction = InputSystem.actions.FindAction("Escape");
        escapeAction.performed += CloseSettings;
    }

    private void OnDisable() {
        playButton.clicked -= OnPlayClicked;
        settingsButton.clicked -= OnSettingsClicked;
        exitSettingsButton.clicked -= CloseSettings;
        escapeAction.performed -= CloseSettings;

        if (quitButton != null && quitButton.style.display != DisplayStyle.None)
            quitButton.clicked -= OnQuitClicked;
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

    private static void OnPlayClicked() {
        SFXPlayer.Instance?.PlayButtonClick();
        MusicPlayer.Instance?.PlayIntroCutsceneMusic();
        SceneManager.LoadScene(1);
    }

    private void OnSettingsClicked() {
        SFXPlayer.Instance?.PlayButtonClick();
        settingsContainer.style.display = DisplayStyle.Flex;
    }

    private static void OnQuitClicked() {
        SFXPlayer.Instance?.PlayButtonClick();
        Application.Quit();
    }

    private void CloseSettings(InputAction.CallbackContext ctx) {
        SFXPlayer.Instance?.PlayButtonClick();
        settingsContainer.style.display = DisplayStyle.None;
    }

    private void CloseSettings() {
        SFXPlayer.Instance?.PlayButtonClick();
        settingsContainer.style.display = DisplayStyle.None;
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
            var w = Mathf.Min(1280, desktop.width);
            var h = Mathf.Min(720, desktop.height);
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
}