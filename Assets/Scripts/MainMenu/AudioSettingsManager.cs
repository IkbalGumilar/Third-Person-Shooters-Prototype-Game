using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>Applies global audio settings from the options menu.</summary>
public sealed class AudioSettingsManager : MonoBehaviour
{
    private const string MasterVolumeKey = "Audio.MasterVolume";
    private const string MusicVolumeKey = "Audio.MusicVolume";
    private const string SfxVolumeKey = "Audio.SfxVolume";
    private const string UiVolumeKey = "Audio.UiVolume";
    private const string MixerKey = "Audio.MixerPreset";
    private const string LanguageKey = "Localization.Language";
    private const string SpeakerModeKey = "Audio.SpeakerMode";
    private const string MuteKey = "Audio.Mute";

    private const float DefaultMasterVolume = 1f;
    private const float DefaultMusicVolume = 0.8f;
    private const float DefaultSfxVolume = 0.9f;
    private const float DefaultUiVolume = 0.85f;
    private const AudioSpeakerMode DefaultSpeakerMode = AudioSpeakerMode.Stereo;

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParameter = "MasterVolume";
    [SerializeField] private string musicVolumeParameter = "MusicVolume";
    [SerializeField] private string sfxVolumeParameter = "SFXVolume";
    [SerializeField] private string uiVolumeParameter = "UIVolume";

    [Header("UI")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider uiVolumeSlider;
    [SerializeField] private TMP_Text masterValueText;
    [SerializeField] private TMP_Text musicValueText;
    [SerializeField] private TMP_Text sfxValueText;
    [SerializeField] private TMP_Text uiValueText;
    [SerializeField] private TMP_Dropdown mixerDropdown;
    [SerializeField] private TMP_Dropdown languageDropdown;
    [SerializeField] private TMP_Dropdown speakerModeDropdown;
    [SerializeField] private Toggle muteToggle;
    [SerializeField] private Slider muteSlider;

    [Header("Options")]
    [SerializeField] private string[] mixerOptions = { "Default", "Headphones", "Speakers", "Night Mode" };
    [SerializeField] private string[] languageOptions = { "English", "Indonesian" };
    [SerializeField] private bool controlSceneAudioSources = true;

    private readonly List<AudioSource> sceneAudioSources = new();
    private bool initialized;
    private bool suppressCallbacks;

    public bool IsReady => masterVolumeSlider != null
        || musicVolumeSlider != null
        || sfxVolumeSlider != null
        || uiVolumeSlider != null
        || mixerDropdown != null
        || languageDropdown != null
        || speakerModeDropdown != null
        || muteToggle != null
        || muteSlider != null;

    private void Start()
    {
        Initialize();
    }

    public void ConfigureForOptions(
        GameObject audioRoot,
        Slider masterSlider,
        Slider musicSlider,
        Slider sfxSlider,
        Slider uiSlider,
        TMP_Text masterText,
        TMP_Text musicText,
        TMP_Text sfxText,
        TMP_Text uiText,
        TMP_Dropdown mixer,
        TMP_Dropdown language,
        TMP_Dropdown speakerMode,
        Toggle mute,
        Slider muteControl)
    {
        masterVolumeSlider = masterSlider;
        musicVolumeSlider = musicSlider;
        sfxVolumeSlider = sfxSlider;
        uiVolumeSlider = uiSlider;
        masterValueText = masterText;
        musicValueText = musicText;
        sfxValueText = sfxText;
        uiValueText = uiText;
        mixerDropdown = mixer;
        languageDropdown = language;
        speakerModeDropdown = speakerMode;
        muteToggle = mute;
        muteSlider = muteControl;

        if (audioRoot != null)
        {
            audioRoot.TryGetComponent(out AudioSettingsManager existing);
            if (existing != null && existing != this && audioMixer == null)
            {
                audioMixer = existing.audioMixer;
            }
        }

        initialized = false;
        Initialize();
    }

    public void Initialize()
    {
        if (initialized)
        {
            return;
        }

        suppressCallbacks = true;
        ConfigureVolumeSlider(masterVolumeSlider);
        ConfigureVolumeSlider(musicVolumeSlider);
        ConfigureVolumeSlider(sfxVolumeSlider);
        ConfigureVolumeSlider(uiVolumeSlider);
        ConfigureBinarySlider(muteSlider);
        PopulateDropdown(mixerDropdown, mixerOptions);
        PopulateDropdown(languageDropdown, languageOptions);
        PopulateDropdown(speakerModeDropdown, GetSpeakerModeLabels());
        RestoreSavedValues();
        BindLocalizationDropdown();
        suppressCallbacks = false;
        RegisterCallbacks();
        ApplySettings();
        initialized = true;
    }

    public void ApplySettings()
    {
        float master = GetVolume(masterVolumeSlider, DefaultMasterVolume);
        float music = GetVolume(musicVolumeSlider, DefaultMusicVolume);
        float sfx = GetVolume(sfxVolumeSlider, DefaultSfxVolume);
        float ui = GetVolume(uiVolumeSlider, DefaultUiVolume);
        bool muted = GetMuteValue();
        float outputMaster = muted ? 0f : master;
        float outputMusic = muted ? 0f : music;
        float outputSfx = muted ? 0f : sfx;
        float outputUi = muted ? 0f : ui;

        AudioListener.volume = outputMaster;
        ApplyMixerVolume(masterVolumeParameter, outputMaster);
        ApplyMixerVolume(musicVolumeParameter, outputMusic);
        ApplyMixerVolume(sfxVolumeParameter, outputSfx);
        ApplyMixerVolume(uiVolumeParameter, outputUi);
        UIAudioController.SetGlobalVolume(outputUi);

        if (controlSceneAudioSources)
        {
            ApplySceneAudioSourceVolumes(outputMusic, outputSfx, outputUi);
        }

        PlayerPrefs.SetFloat(MasterVolumeKey, master);
        PlayerPrefs.SetFloat(MusicVolumeKey, music);
        PlayerPrefs.SetFloat(SfxVolumeKey, sfx);
        PlayerPrefs.SetFloat(UiVolumeKey, ui);
        if (mixerDropdown != null)
        {
            PlayerPrefs.SetInt(MixerKey, mixerDropdown.value);
        }
        if (languageDropdown != null)
        {
            ApplyLocalizationLanguage(languageDropdown.value);
            PlayerPrefs.SetInt(LanguageKey, languageDropdown.value);
        }
        if (speakerModeDropdown != null)
        {
            ApplySpeakerMode(speakerModeDropdown.value);
            PlayerPrefs.SetInt(SpeakerModeKey, speakerModeDropdown.value);
        }
        if (muteToggle != null)
        {
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        }

        UpdateValueTexts();
        PlayerPrefs.Save();
    }

    public void ResetToDefault()
    {
        suppressCallbacks = true;
        SetVolume(masterVolumeSlider, DefaultMasterVolume);
        SetVolume(musicVolumeSlider, DefaultMusicVolume);
        SetVolume(sfxVolumeSlider, DefaultSfxVolume);
        SetVolume(uiVolumeSlider, DefaultUiVolume);
        SetDropdownValue(mixerDropdown, 0);
        SetDropdownValue(languageDropdown, 0);
        SetDropdownValue(speakerModeDropdown, GetSpeakerModeIndex(DefaultSpeakerMode));
        SetToggleValue(muteToggle, false);
        SetBinarySliderValue(muteSlider, false);
        suppressCallbacks = false;
        ApplySettings();
    }

    public void StepMixer(int direction)
    {
        StepDropdown(mixerDropdown, direction);
    }

    public void StepLanguage(int direction)
    {
        StepDropdown(languageDropdown, direction);
    }

    private void RegisterCallbacks()
    {
        RegisterVolumeCallback(masterVolumeSlider);
        RegisterVolumeCallback(musicVolumeSlider);
        RegisterVolumeCallback(sfxVolumeSlider);
        RegisterVolumeCallback(uiVolumeSlider);
        RegisterDropdownCallback(mixerDropdown);
        RegisterDropdownCallback(languageDropdown);
        RegisterDropdownCallback(speakerModeDropdown);
        RegisterToggleCallback(muteToggle);
        RegisterVolumeCallback(muteSlider);
    }

    private void RegisterVolumeCallback(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.onValueChanged.RemoveListener(HandleControlChanged);
        slider.onValueChanged.AddListener(HandleControlChanged);
    }

    private void RegisterDropdownCallback(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        dropdown.onValueChanged.RemoveListener(HandleDropdownChanged);
        dropdown.onValueChanged.AddListener(HandleDropdownChanged);
    }

    private void RegisterToggleCallback(Toggle toggle)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(HandleToggleChanged);
        toggle.onValueChanged.AddListener(HandleToggleChanged);
    }

    private void HandleControlChanged(float _)
    {
        if (!suppressCallbacks)
        {
            ApplySettings();
        }
    }

    private void HandleDropdownChanged(int _)
    {
        if (!suppressCallbacks)
        {
            ApplySettings();
        }
    }

    private void HandleToggleChanged(bool _)
    {
        if (!suppressCallbacks)
        {
            ApplySettings();
        }
    }

    private void RestoreSavedValues()
    {
        SetVolume(masterVolumeSlider, PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
        SetVolume(musicVolumeSlider, PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume));
        SetVolume(sfxVolumeSlider, PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        SetVolume(uiVolumeSlider, PlayerPrefs.GetFloat(UiVolumeKey, DefaultUiVolume));
        SetDropdownValue(mixerDropdown, PlayerPrefs.GetInt(MixerKey, 0));
        SetDropdownValue(languageDropdown, PlayerPrefs.GetInt(LanguageKey, 0));
        SetDropdownValue(speakerModeDropdown, PlayerPrefs.GetInt(SpeakerModeKey, GetSpeakerModeIndex(AudioSettings.speakerMode)));
        bool muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        SetToggleValue(muteToggle, muted);
        SetBinarySliderValue(muteSlider, muted);
        UpdateValueTexts();
    }

    private void StepDropdown(TMP_Dropdown dropdown, int direction)
    {
        if (dropdown == null || dropdown.options.Count == 0 || direction == 0)
        {
            return;
        }

        int next = Mathf.Clamp(dropdown.value + Math.Sign(direction), 0, dropdown.options.Count - 1);
        if (next == dropdown.value)
        {
            return;
        }

        dropdown.SetValueWithoutNotify(next);
        ApplySettings();
    }

    private void BindLocalizationDropdown()
    {
        if (languageDropdown == null || LocalizationManager.Instance == null)
        {
            return;
        }

        LocalizationManager.Instance.BindDropdown(languageDropdown);
    }

    private static void ApplyLocalizationLanguage(int dropdownValue)
    {
        if (LocalizationManager.Instance == null)
        {
            return;
        }

        LocalizationManager.Instance.SetLanguage(LocalizationManager.IndexToLanguage(dropdownValue));
    }

    private void ApplyMixerVolume(string parameterName, float normalizedVolume)
    {
        if (audioMixer == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        audioMixer.SetFloat(parameterName, NormalizedToDecibels(normalizedVolume));
    }

    private void ApplySpeakerMode(int optionIndex)
    {
        List<AudioSpeakerMode> modes = GetAvailableSpeakerModes();
        if (modes.Count == 0)
        {
            return;
        }

        int index = Mathf.Clamp(optionIndex, 0, modes.Count - 1);
        AudioSpeakerMode mode = modes[index];
        AudioConfiguration configuration = AudioSettings.GetConfiguration();
        if (configuration.speakerMode == mode)
        {
            return;
        }

        configuration.speakerMode = mode;
        AudioSettings.Reset(configuration);
    }

    private void ApplySceneAudioSourceVolumes(float music, float sfx, float ui)
    {
        sceneAudioSources.Clear();
        sceneAudioSources.AddRange(FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude));

        for (int i = 0; i < sceneAudioSources.Count; i++)
        {
            AudioSource source = sceneAudioSources[i];
            if (source == null)
            {
                continue;
            }

            string sourceName = source.gameObject.name;
            if (ContainsAny(sourceName, "music", "bgm", "ost"))
            {
                source.volume = music;
            }
            else if (ContainsAny(sourceName, "ui", "button", "menu"))
            {
                source.volume = ui;
            }
            else if (ContainsAny(sourceName, "sfx", "sound", "weapon", "enemy", "player", "footstep", "voice", "effect"))
            {
                source.volume = sfx;
            }
        }
    }

    private void UpdateValueTexts()
    {
        SetValueText(masterValueText, GetVolume(masterVolumeSlider, DefaultMasterVolume));
        SetValueText(musicValueText, GetVolume(musicVolumeSlider, DefaultMusicVolume));
        SetValueText(sfxValueText, GetVolume(sfxVolumeSlider, DefaultSfxVolume));
        SetValueText(uiValueText, GetVolume(uiVolumeSlider, DefaultUiVolume));
    }

    private static void ConfigureVolumeSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    private static void PopulateDropdown(TMP_Dropdown dropdown, IReadOnlyList<string> options)
    {
        if (dropdown == null || options == null || options.Count == 0)
        {
            return;
        }

        dropdown.ClearOptions();
        List<string> localizedOptions = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            localizedOptions.Add(LocalizationManager.GetText(options[i]));
        }

        dropdown.AddOptions(localizedOptions);
    }

    private static float GetVolume(Slider slider, float fallback)
    {
        return slider == null ? fallback : Mathf.Clamp01(slider.value);
    }

    private bool GetMuteValue()
    {
        if (muteSlider != null)
        {
            return muteSlider.value < 0.5f;
        }

        return muteToggle != null && muteToggle.isOn;
    }

    private static void SetVolume(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private static void ConfigureBinarySlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = true;
    }

    private static void SetBinarySliderValue(Slider slider, bool active)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(active ? 0f : 1f);
        }
    }

    private static void SetValueText(TMP_Text text, float volume)
    {
        if (text != null)
        {
            float decibels = NormalizedToDecibels(volume);
            text.text = decibels <= -79.9f ? "-80 dB" : $"{decibels:0.#} dB";
        }
    }

    private List<string> GetSpeakerModeLabels()
    {
        List<AudioSpeakerMode> modes = GetAvailableSpeakerModes();
        var labels = new List<string>(modes.Count);
        for (int i = 0; i < modes.Count; i++)
        {
            labels.Add(GetSpeakerModeLabel(modes[i]));
        }

        return labels;
    }

    private int GetSpeakerModeIndex(AudioSpeakerMode mode)
    {
        List<AudioSpeakerMode> modes = GetAvailableSpeakerModes();
        for (int i = 0; i < modes.Count; i++)
        {
            if (modes[i] == mode)
            {
                return i;
            }
        }

        return Mathf.Max(0, modes.IndexOf(AudioSpeakerMode.Stereo));
    }

    private static List<AudioSpeakerMode> GetAvailableSpeakerModes()
    {
        var modes = new List<AudioSpeakerMode> { AudioSpeakerMode.Mono, AudioSpeakerMode.Stereo };
        AudioSpeakerMode driverMode = AudioSettings.driverCapabilities;
        if (driverMode != AudioSpeakerMode.Mono
            && driverMode != AudioSpeakerMode.Stereo
            && !modes.Contains(driverMode))
        {
            modes.Add(driverMode);
        }

        return modes;
    }

    private static string GetSpeakerModeLabel(AudioSpeakerMode mode)
    {
        return mode switch
        {
            AudioSpeakerMode.Mono => "Mono",
            AudioSpeakerMode.Stereo => "Stereo",
            AudioSpeakerMode.Quad => "Quad",
            AudioSpeakerMode.Mode5point1 => "5.1 Surround",
            AudioSpeakerMode.Mode7point1 => "7.1 Surround",
            AudioSpeakerMode.Prologic => "Prologic",
            _ => mode.ToString()
        };
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown != null && dropdown.options.Count > 0)
        {
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, dropdown.options.Count - 1));
        }
    }

    private static float NormalizedToDecibels(float value)
    {
        return Mathf.Log10(Mathf.Max(0.0001f, Mathf.Clamp01(value))) * 20f;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
