using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Applies graphics controls from the MainMenu canvas.</summary>
public sealed class GraphicsSettingsManager : MonoBehaviour
{
    private const string ResolutionKey = "Graphics.Resolution";
    private const string QualityKey = "Graphics.Quality";
    private const string ShadowKey = "Graphics.Shadows";
    private const string AntiAliasingKey = "Graphics.AntiAliasing";
    private const string TextureKey = "Graphics.TextureQuality";
    private const string VSyncKey = "Graphics.VSync";
    private const string FullscreenKey = "Graphics.Fullscreen";
    private const string FieldOfViewKey = "Graphics.FieldOfView";

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
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Component postProcessingVolume;

    private readonly List<Resolution> availableResolutions = new();
    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    public void ConfigureForOptions(TMP_Dropdown resolution, TMP_Dropdown quality, TMP_Dropdown shadows, TMP_Dropdown antiAliasing, TMP_Dropdown textures, Toggle vSync, Toggle fullscreen, Slider fov)
    {
        ConfigureForOptions(resolution, quality, shadows, antiAliasing, textures, vSync, fullscreen, fov, null, null);
    }

    public void ConfigureForOptions(TMP_Dropdown resolution, TMP_Dropdown quality, TMP_Dropdown shadows, TMP_Dropdown antiAliasing, TMP_Dropdown textures,
        Toggle vSync, Toggle fullscreen, Slider fov, Slider vSyncControl, Slider fullscreenControl)
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
        initialized = false;
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        PopulateResolutionOptions();
        PopulateQualityOptions();
        PopulateShadowOptions();
        PopulateAntiAliasingOptions();
        PopulateTextureQualityOptions();
        RestoreSavedValues();
        PreviewFieldOfView();
        initialized = true;
    }

    public void ApplySettings()
    {
        ApplyResolution();

        if (graphicsDropdown != null)
        {
            QualitySettings.SetQualityLevel(graphicsDropdown.value, true);
            PlayerPrefs.SetInt(QualityKey, graphicsDropdown.value);
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
            QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(textureDropdown.value, 0, 3);
            PlayerPrefs.SetInt(TextureKey, textureDropdown.value);
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
        PlayerPrefs.DeleteKey(ShadowKey);
        PlayerPrefs.DeleteKey(AntiAliasingKey);
        PlayerPrefs.DeleteKey(TextureKey);
        PlayerPrefs.DeleteKey(VSyncKey);
        PlayerPrefs.DeleteKey(FullscreenKey);
        PlayerPrefs.DeleteKey(FieldOfViewKey);
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

        resolutionDropdown.AddOptions(options);
    }

    private void PopulateQualityOptions()
    {
        if (graphicsDropdown == null)
        {
            return;
        }

        graphicsDropdown.ClearOptions();
        graphicsDropdown.AddOptions(new List<string>(QualitySettings.names));
    }

    private void PopulateShadowOptions()
    {
        if (shadowDropdown == null)
        {
            return;
        }

        shadowDropdown.ClearOptions();
        shadowDropdown.AddOptions(new List<string> { "Off", "Hard Shadows", "All Shadows" });
    }

    private void PopulateAntiAliasingOptions()
    {
        if (aaDropdown == null)
        {
            return;
        }

        aaDropdown.ClearOptions();
        aaDropdown.AddOptions(AntiAliasingSettingsUtility.GetOptionLabels());
    }

    private void PopulateTextureQualityOptions()
    {
        if (textureDropdown == null)
        {
            return;
        }

        textureDropdown.ClearOptions();
        textureDropdown.AddOptions(new List<string> { "Full Resolution", "Half Resolution", "Quarter Resolution", "Eighth Resolution" });
    }

    private void RestoreSavedValues()
    {
        if (graphicsDropdown != null)
        {
            graphicsDropdown.SetValueWithoutNotify(Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, Mathf.Max(0, graphicsDropdown.options.Count - 1)));
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

        if (fovSlider != null)
        {
            fovSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(FieldOfViewKey, fovSlider.value));
        }

        if (resolutionDropdown != null && availableResolutions.Count > 0)
        {
            int savedIndex = PlayerPrefs.GetInt(ResolutionKey, FindCurrentResolutionIndex());
            resolutionDropdown.SetValueWithoutNotify(Mathf.Clamp(savedIndex, 0, availableResolutions.Count - 1));
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
}
