using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies graphics controls from the MainMenu canvas.</summary>
public sealed class GraphicsSettingsManager : MonoBehaviour
{
    private const string ResolutionKey = "Graphics.Resolution";
    private const string QualityKey = "Graphics.Quality";
    private const string QualityPresetKey = "Graphics.QualityPreset";
    private const string ShadowKey = "Graphics.Shadows";
    private const string AntiAliasingKey = "Graphics.AntiAliasing";
    private const string TextureKey = "Graphics.TextureQuality";
    private const string VSyncKey = "Graphics.VSync";
    private const string FullscreenKey = "Graphics.Fullscreen";
    private const string FieldOfViewKey = "Graphics.FieldOfView";
    private const string FoliagePhysicsKey = VegetationWindSettings.PreferenceKey;
    private const int QualityPresetLow = 0;
    private const int QualityPresetMedium = 1;
    private const int QualityPresetHigh = 2;
    private const int QualityPresetUltra = 3;
    private const int QualityPresetCustom = 4;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private TMP_Dropdown graphicsDropdown;
    [SerializeField] private TMP_Dropdown shadowDropdown;
    [SerializeField] private TMP_Dropdown aaDropdown;
    [SerializeField] private TMP_Dropdown textureDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider vSyncSlider;
    [SerializeField] private Slider fullscreenSlider;
    [SerializeField] private Toggle motionBlurToggle;
    [SerializeField] private Toggle foliagePhysicsToggle;
    [SerializeField] private Slider foliagePhysicsSlider;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Component postProcessingVolume;
    [SerializeField] private PostProcessingSettings postProcessingSettings;
    [SerializeField] private VegetationWindSettings vegetationWindSettings;

    private readonly List<Resolution> availableResolutions = new();
    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    public bool HasUsableBindings()
    {
        if (aaDropdown != null && textureDropdown != null && ReferenceEquals(aaDropdown, textureDropdown))
        {
            return false;
        }

        return resolutionDropdown != null
            || graphicsDropdown != null
            || shadowDropdown != null
            || aaDropdown != null
            || textureDropdown != null
            || vSyncToggle != null
            || fullscreenToggle != null
            || vSyncSlider != null
            || fullscreenSlider != null
            || foliagePhysicsToggle != null
            || foliagePhysicsSlider != null
            || fovSlider != null;
    }

    public void ConfigureForOptions(TMP_Dropdown resolution, TMP_Dropdown quality, TMP_Dropdown shadows, TMP_Dropdown antiAliasing, TMP_Dropdown textures, Toggle vSync, Toggle fullscreen, Slider fov)
    {
        ConfigureForOptions(resolution, quality, shadows, antiAliasing, textures, vSync, fullscreen, fov, null, null);
    }

    public void ConfigureForOptions(TMP_Dropdown resolution, TMP_Dropdown quality, TMP_Dropdown shadows, TMP_Dropdown antiAliasing, TMP_Dropdown textures,
        Toggle vSync, Toggle fullscreen, Slider fov, Slider vSyncControl, Slider fullscreenControl)
    {
        ConfigureForOptions(resolution, quality, shadows, antiAliasing, textures, vSync, fullscreen, fov, vSyncControl, fullscreenControl, null, null);
    }

    public void ConfigureForOptions(TMP_Dropdown resolution, TMP_Dropdown quality, TMP_Dropdown shadows, TMP_Dropdown antiAliasing, TMP_Dropdown textures,
        Toggle vSync, Toggle fullscreen, Slider fov, Slider vSyncControl, Slider fullscreenControl, Toggle foliagePhysics, Slider foliagePhysicsControl)
    {
        resolutionDropdown = resolution;
        graphicsDropdown = quality;
        shadowDropdown = shadows;
        aaDropdown = antiAliasing;
        textureDropdown = textures;
        vSyncToggle = vSync;
        fullscreenToggle = fullscreen;
        fovSlider = fov;
        vSyncSlider = vSyncControl;
        fullscreenSlider = fullscreenControl;
        foliagePhysicsToggle = foliagePhysics;
        foliagePhysicsSlider = foliagePhysicsControl;
        initialized = false;
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (!HasUsableBindings())
        {
            initialized = true;
            enabled = false;
            return;
        }

        PopulateResolutionOptions();
        PopulateQualityOptions();
        PopulateShadowOptions();
        PopulateAntiAliasingOptions();
        PopulateTextureQualityOptions();
        RegisterQualityPresetListener();
        RegisterManualQualityListeners();
        ConfigureBinarySlider(foliagePhysicsSlider);
        ResolveVegetationWindSettings();
        RestoreSavedValues();
        PreviewFieldOfView();
        initialized = true;
    }

    public void ApplySettings()
    {
        ApplyResolution();

        if (graphicsDropdown != null)
        {
            int preset = Mathf.Clamp(graphicsDropdown.value, QualityPresetLow, QualityPresetCustom);
            int qualityLevel = GetUnityQualityLevelForPreset(preset);
            QualitySettings.SetQualityLevel(qualityLevel, true);
            PlayerPrefs.SetInt(QualityPresetKey, preset);
            PlayerPrefs.SetInt(QualityKey, qualityLevel);
        }

        if (shadowDropdown != null)
        {
            QualitySettings.shadows = (ShadowQuality)Mathf.Clamp(shadowDropdown.value, 0, 2);
            PlayerPrefs.SetInt(ShadowKey, shadowDropdown.value);
        }

        if (aaDropdown != null)
        {
            int option = AntiAliasingSettingsUtility.GetEffectiveOptionIndex(aaDropdown.value);
            AntiAliasingSettingsUtility.Apply(option);
            aaDropdown.SetValueWithoutNotify(option);
            PlayerPrefs.SetInt(AntiAliasingKey, option);
        }

        if (textureDropdown != null)
        {
            int textureQuality = Mathf.Clamp(textureDropdown.value, 0, 3);
            QualitySettings.globalTextureMipmapLimit = textureQuality;
            ResolveVegetationWindSettings();
            vegetationWindSettings?.ApplyTextureQuality(textureQuality, false);
            PlayerPrefs.SetInt(TextureKey, textureQuality);
        }

        if (vSyncToggle != null || vSyncSlider != null)
        {
            bool active = GetBinaryControlValue(vSyncToggle, vSyncSlider);
            QualitySettings.vSyncCount = active ? 1 : 0;
            PlayerPrefs.SetInt(VSyncKey, active ? 1 : 0);
        }

        if (fullscreenToggle != null || fullscreenSlider != null)
        {
            bool active = GetBinaryControlValue(fullscreenToggle, fullscreenSlider);
            Screen.fullScreenMode = active ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt(FullscreenKey, active ? 1 : 0);
        }

        if (foliagePhysicsToggle != null || foliagePhysicsSlider != null)
        {
            ApplyFoliagePhysicsFromControls(true);
        }

        if (fovSlider != null)
        {
            PlayerPrefs.SetFloat(FieldOfViewKey, fovSlider.value);
            PreviewFieldOfView();
        }

        PlayerPrefs.Save();
    }

    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(ResolutionKey);
        PlayerPrefs.DeleteKey(QualityKey);
        PlayerPrefs.DeleteKey(QualityPresetKey);
        PlayerPrefs.DeleteKey(ShadowKey);
        PlayerPrefs.DeleteKey(AntiAliasingKey);
        PlayerPrefs.DeleteKey(TextureKey);
        PlayerPrefs.DeleteKey(VSyncKey);
        PlayerPrefs.DeleteKey(FullscreenKey);
        PlayerPrefs.DeleteKey(FieldOfViewKey);
        PlayerPrefs.DeleteKey(FoliagePhysicsKey);
        PlayerPrefs.Save();

        RestoreSavedValues();
        ApplySettings();
    }

    private void PopulateResolutionOptions()
    {
        if (resolutionDropdown == null)
        {
            return;
        }

        availableResolutions.Clear();
        resolutionDropdown.ClearOptions();

        Resolution[] resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution candidate = resolutions[i];
            bool duplicate = false;
            for (int j = 0; j < availableResolutions.Count; j++)
            {
                Resolution existing = availableResolutions[j];
                if (existing.width == candidate.width && existing.height == candidate.height && existing.refreshRateRatio.value == candidate.refreshRateRatio.value)
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
            {
                availableResolutions.Add(candidate);
            }
        }

        var options = new List<string>(availableResolutions.Count);
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            Resolution resolution = availableResolutions[i];
            options.Add($"{resolution.width} x {resolution.height} ({Mathf.RoundToInt((float)resolution.refreshRateRatio.value)} Hz)");
        }

        resolutionDropdown.AddOptions(LocalizeOptions(options));
    }

    private void PopulateQualityOptions()
    {
        if (graphicsDropdown == null)
        {
            return;
        }

        graphicsDropdown.ClearOptions();
        graphicsDropdown.AddOptions(LocalizeOptions(GetQualityPresetLabels()));
    }

    private void PopulateShadowOptions()
    {
        if (shadowDropdown == null)
        {
            return;
        }

        shadowDropdown.ClearOptions();
        shadowDropdown.AddOptions(LocalizeOptions(new List<string> { "Off", "Hard Shadows", "All Shadows" }));
    }

    private void PopulateAntiAliasingOptions()
    {
        if (aaDropdown == null)
        {
            return;
        }

        aaDropdown.ClearOptions();
        aaDropdown.AddOptions(LocalizeOptions(AntiAliasingSettingsUtility.GetOptionLabels()));
    }

    private void PopulateTextureQualityOptions()
    {
        if (textureDropdown == null)
        {
            return;
        }

        textureDropdown.ClearOptions();
        textureDropdown.AddOptions(LocalizeOptions(new List<string> { "Full Resolution", "Half Resolution", "Quarter Resolution", "Eighth Resolution" }));
    }

    private void RegisterQualityPresetListener()
    {
        if (graphicsDropdown == null)
        {
            return;
        }

        graphicsDropdown.onValueChanged.AddListener(value => ApplyQualityPresetToControls(value, false));
    }

    private void RegisterManualQualityListeners()
    {
        if (shadowDropdown != null) shadowDropdown.onValueChanged.AddListener(_ => SetQualityPresetCustom());
        if (aaDropdown != null) aaDropdown.onValueChanged.AddListener(_ => SetQualityPresetCustom());
        if (textureDropdown != null)
        {
            textureDropdown.onValueChanged.AddListener(_ =>
            {
                SetQualityPresetCustom();
                ApplyVegetationTextureQualityFromControls(false);
            });
        }
        if (vSyncToggle != null) vSyncToggle.onValueChanged.AddListener(_ => SetQualityPresetCustom());
        if (vSyncSlider != null) vSyncSlider.onValueChanged.AddListener(_ => SetQualityPresetCustom());
        if (foliagePhysicsToggle != null)
        {
            foliagePhysicsToggle.onValueChanged.AddListener(_ =>
            {
                SetQualityPresetCustom();
            });
        }

        if (foliagePhysicsSlider != null)
        {
            foliagePhysicsSlider.onValueChanged.AddListener(_ =>
            {
                SetQualityPresetCustom();
            });
        }
    }

    private void SetQualityPresetCustom()
    {
        if (graphicsDropdown != null && graphicsDropdown.options.Count > QualityPresetCustom && graphicsDropdown.value != QualityPresetCustom)
        {
            graphicsDropdown.SetValueWithoutNotify(QualityPresetCustom);
        }
    }

    private void ApplyQualityPresetToControls(int presetIndex, bool updateDropdown)
    {
        if (presetIndex == QualityPresetCustom)
        {
            if (updateDropdown)
            {
                SetDropdownValue(graphicsDropdown, QualityPresetCustom);
            }

            return;
        }

        int preset = Mathf.Clamp(presetIndex, QualityPresetLow, QualityPresetUltra);
        if (updateDropdown)
        {
            SetDropdownValue(graphicsDropdown, preset);
        }

        switch (preset)
        {
            case QualityPresetLow:
                SetDropdownValue(shadowDropdown, 0);
                SetDropdownValue(aaDropdown, FindAntiAliasingOption("Off", "None", "Disabled"));
                SetDropdownValue(textureDropdown, 2);
                SetToggleValue(vSyncToggle, false);
                SetBinarySliderValue(vSyncSlider, false);
                SetToggleValue(foliagePhysicsToggle, false);
                SetBinarySliderValue(foliagePhysicsSlider, false);
                SetPostProcessingPreset(false, false, false, false, false);
                break;
            case QualityPresetMedium:
                SetDropdownValue(shadowDropdown, 1);
                SetDropdownValue(aaDropdown, FindAntiAliasingOption("FXAA", "MSAA 2x", "2x", "Off"));
                SetDropdownValue(textureDropdown, 1);
                SetToggleValue(vSyncToggle, false);
                SetBinarySliderValue(vSyncSlider, false);
                SetToggleValue(foliagePhysicsToggle, true);
                SetBinarySliderValue(foliagePhysicsSlider, true);
                SetPostProcessingPreset(true, false, false, false, false);
                break;
            case QualityPresetHigh:
                SetDropdownValue(shadowDropdown, 2);
                SetDropdownValue(aaDropdown, FindAntiAliasingOption("TAA", "MSAA 4x", "4x", "FXAA"));
                SetDropdownValue(textureDropdown, 0);
                SetToggleValue(vSyncToggle, true);
                SetBinarySliderValue(vSyncSlider, true);
                SetToggleValue(foliagePhysicsToggle, true);
                SetBinarySliderValue(foliagePhysicsSlider, true);
                SetPostProcessingPreset(true, false, true, true, false);
                break;
            case QualityPresetUltra:
                SetDropdownValue(shadowDropdown, 2);
                SetDropdownValue(aaDropdown, FindAntiAliasingOption("TAA", "MSAA 8x", "8x", "MSAA 4x", "4x"));
                SetDropdownValue(textureDropdown, 0);
                SetToggleValue(vSyncToggle, true);
                SetBinarySliderValue(vSyncSlider, true);
                SetToggleValue(foliagePhysicsToggle, true);
                SetBinarySliderValue(foliagePhysicsSlider, true);
                SetPostProcessingPreset(true, true, true, true, true);
                break;
        }

        ApplyVegetationTextureQualityFromControls(false);
    }

    private void SetPostProcessingPreset(bool bloom, bool motionBlur, bool depthOfField, bool chromaticAberration, bool filmGrain)
    {
        if (postProcessingSettings == null)
        {
            postProcessingSettings = GetComponent<PostProcessingSettings>();
        }

        postProcessingSettings?.SetControlValues(bloom, motionBlur, depthOfField, chromaticAberration, filmGrain);
    }

    private static List<string> GetQualityPresetLabels()
    {
        return new List<string> { "Low", "Medium", "High", "Ultra", "Custom" };
    }

    private static List<string> LocalizeOptions(List<string> options)
    {
        if (options == null)
        {
            return null;
        }

        List<string> localizedOptions = new List<string>(options.Count);
        for (int i = 0; i < options.Count; i++)
        {
            localizedOptions.Add(LocalizationManager.GetText(options[i]));
        }

        return localizedOptions;
    }

    private static int DetectRecommendedQualityPreset()
    {
        if (SystemInfo.graphicsMemorySize < 1024 || SystemInfo.systemMemorySize < 4096 || SystemInfo.graphicsShaderLevel < 35)
        {
            return QualityPresetLow;
        }

        int score = 0;
        if (SystemInfo.systemMemorySize >= 16000) score += 2;
        else if (SystemInfo.systemMemorySize >= 8000) score += 1;

        if (SystemInfo.graphicsMemorySize >= 8000) score += 3;
        else if (SystemInfo.graphicsMemorySize >= 4000) score += 2;
        else if (SystemInfo.graphicsMemorySize >= 2000) score += 1;

        if (SystemInfo.processorCount >= 8) score += 2;
        else if (SystemInfo.processorCount >= 4) score += 1;

        if (SystemInfo.graphicsShaderLevel >= 50) score += 2;
        else if (SystemInfo.graphicsShaderLevel >= 45) score += 1;

        if (score >= 8) return QualityPresetUltra;
        if (score >= 5) return QualityPresetHigh;
        if (score >= 3) return QualityPresetMedium;
        return QualityPresetLow;
    }

    private static int GetUnityQualityLevelForPreset(int presetIndex)
    {
        if (QualitySettings.names == null || QualitySettings.names.Length == 0)
        {
            return 0;
        }

        return presetIndex switch
        {
            QualityPresetLow => FindUnityQualityLevel("Low", "Very Low", "Performant", "Performance"),
            QualityPresetMedium => FindUnityQualityLevel("Medium", "Balanced"),
            QualityPresetHigh => FindUnityQualityLevel("High", "Beautiful"),
            QualityPresetUltra => FindUnityQualityLevel("Ultra", "Very High", "Fantastic"),
            _ => Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1)
        };
    }

    private static int FindUnityQualityLevel(params string[] preferredNames)
    {
        for (int p = 0; p < preferredNames.Length; p++)
        {
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                if (QualitySettings.names[i].IndexOf(preferredNames[p], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
        }

        if (preferredNames.Length > 0 && preferredNames[0].IndexOf("low", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return 0;
        }

        if (preferredNames.Length > 0 && preferredNames[0].IndexOf("medium", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return Mathf.Clamp(QualitySettings.names.Length / 2, 0, QualitySettings.names.Length - 1);
        }

        return QualitySettings.names.Length - 1;
    }

    private int FindAntiAliasingOption(params string[] preferredLabels)
    {
        if (aaDropdown == null || aaDropdown.options.Count == 0)
        {
            return 0;
        }

        for (int p = 0; p < preferredLabels.Length; p++)
        {
            for (int i = 0; i < aaDropdown.options.Count; i++)
            {
                if (aaDropdown.options[i].text.IndexOf(preferredLabels[p], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
        }

        return AntiAliasingSettingsUtility.GetDefaultOptionIndex();
    }

    private void RestoreSavedValues()
    {
        bool hasSavedPreset = PlayerPrefs.HasKey(QualityPresetKey);
        int preset = PlayerPrefs.GetInt(QualityPresetKey, DetectRecommendedQualityPreset());
        if (graphicsDropdown != null)
        {
            graphicsDropdown.SetValueWithoutNotify(Mathf.Clamp(preset, 0, Mathf.Max(0, graphicsDropdown.options.Count - 1)));
        }

        if (shadowDropdown != null)
        {
            shadowDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt(ShadowKey, (int)QualitySettings.shadows), 0, 2));
        }

        if (aaDropdown != null)
        {
            int defaultAa = AntiAliasingSettingsUtility.GetDefaultOptionIndex();
            aaDropdown.SetValueWithoutNotify(AntiAliasingSettingsUtility.ClampOptionIndex(PlayerPrefs.GetInt(AntiAliasingKey, defaultAa)));
        }

        if (textureDropdown != null)
        {
            textureDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt(TextureKey, QualitySettings.globalTextureMipmapLimit), 0, 3));
        }

        if (vSyncToggle != null)
        {
            vSyncToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);
        }

        SetBinarySliderValue(vSyncSlider, PlayerPrefs.GetInt(VSyncKey, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1);

        if (fullscreenToggle != null)
        {
            fullscreenToggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1);
        }

        SetBinarySliderValue(fullscreenSlider, PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1);

        bool foliagePhysicsActive = PlayerPrefs.GetInt(FoliagePhysicsKey, 1) == 1;
        if (foliagePhysicsToggle != null)
        {
            foliagePhysicsToggle.SetIsOnWithoutNotify(foliagePhysicsActive);
        }

        SetBinarySliderValue(foliagePhysicsSlider, foliagePhysicsActive);
        ResolveVegetationWindSettings();
        vegetationWindSettings?.SetEnabled(foliagePhysicsActive, false);
        ApplyVegetationTextureQualityFromControls(false);

        if (fovSlider != null)
        {
            fovSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(FieldOfViewKey, fovSlider.value));
        }

        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            int savedIndex = PlayerPrefs.GetInt(ResolutionKey, FindCurrentResolutionIndex());
            resolutionDropdown.SetValueWithoutNotify(Mathf.Clamp(savedIndex, 0, availableResolutions.Count - 1));
        }

        if (!hasSavedPreset && preset != QualityPresetCustom)
        {
            ApplyQualityPresetToControls(preset, false);
        }
    }

    private void ApplyResolution()
    {
        if (resolutionDropdown == null || availableResolutions.Count == 0)
        {
            return;
        }

        int index = Mathf.Clamp(resolutionDropdown.value, 0, availableResolutions.Count - 1);
        Resolution resolution = availableResolutions[index];
        FullScreenMode mode = GetBinaryControlValue(fullscreenToggle, fullscreenSlider) ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);
        PlayerPrefs.SetInt(ResolutionKey, index);
    }

    private static bool GetBinaryControlValue(Toggle toggle, Slider slider)
    {
        if (slider != null)
        {
            return slider.value < 0.5f;
        }

        return toggle != null && toggle.isOn;
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
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = true;
        slider.SetValueWithoutNotify(active ? 0f : 1f);
    }

    private static void SetDropdownValue(TMP_Dropdown dropdown, int value)
    {
        if (dropdown != null && dropdown.options.Count > 0)
        {
            dropdown.SetValueWithoutNotify(Mathf.Clamp(value, 0, dropdown.options.Count - 1));
        }
    }

    private static void SetToggleValue(Toggle toggle, bool value)
    {
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(value);
        }
    }

    private int FindCurrentResolutionIndex()
    {
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            Resolution resolution = availableResolutions[i];
            if (resolution.width == Screen.currentResolution.width && resolution.height == Screen.currentResolution.height)
            {
                return i;
            }
        }

        return availableResolutions.Count - 1;
    }

    private void PreviewFieldOfView()
    {
        if (fovSlider == null || Camera.main == null)
        {
            return;
        }

        Camera.main.fieldOfView = fovSlider.value;
    }

    private void ResolveVegetationWindSettings()
    {
        if (vegetationWindSettings != null)
        {
            return;
        }

        vegetationWindSettings = FindAnyObjectByType<VegetationWindSettings>(FindObjectsInactive.Include);
        if (vegetationWindSettings == null)
        {
            vegetationWindSettings = gameObject.AddComponent<VegetationWindSettings>();
        }
    }

    private void ApplyFoliagePhysicsFromControls(bool save)
    {
        if (foliagePhysicsToggle == null && foliagePhysicsSlider == null)
        {
            return;
        }

        bool active = GetBinaryControlValue(foliagePhysicsToggle, foliagePhysicsSlider);
        ResolveVegetationWindSettings();
        vegetationWindSettings?.SetEnabled(active, save);
        if (save)
        {
            PlayerPrefs.SetInt(FoliagePhysicsKey, active ? 1 : 0);
        }
    }

    private void ApplyVegetationTextureQualityFromControls(bool save)
    {
        if (textureDropdown == null)
        {
            return;
        }

        int textureQuality = Mathf.Clamp(textureDropdown.value, 0, 3);
        ResolveVegetationWindSettings();
        vegetationWindSettings?.ApplyTextureQuality(textureQuality, save);
    }
}
