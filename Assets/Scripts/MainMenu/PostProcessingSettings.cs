using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Enables and disables individual overrides in the assigned URP volume.</summary>
public sealed class PostProcessingSettings : MonoBehaviour
{
    private const string BloomKey = "PostProcessing.Bloom";
    private const string MotionBlurKey = "PostProcessing.MotionBlur";
    private const string DepthOfFieldKey = "PostProcessing.DepthOfField";
    private const string ChromaticAberrationKey = "PostProcessing.ChromaticAberration";
    private const string FilmGrainKey = "PostProcessing.FilmGrain";

    [SerializeField] private Component postProcessingVolume;
    [SerializeField] private Toggle bloomToggle;
    [SerializeField] private Toggle motionBlurToggle;
    [SerializeField] private Toggle dofToggle;
    [SerializeField] private Toggle chromaticAberrationToggle;
    [SerializeField] private Toggle filmGrainToggle;
    [SerializeField] private Slider bloomSlider;
    [SerializeField] private Slider motionBlurSlider;
    [SerializeField] private Slider dofSlider;
    [SerializeField] private Slider chromaticAberrationSlider;
    [SerializeField] private Slider filmGrainSlider;

    private readonly Dictionary<string, bool> defaultStates = new();
    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    public void ConfigureForOptions(Component volume, Toggle bloom, Toggle motionBlur, Toggle depthOfField, Toggle chromaticAberration, Toggle filmGrain)
    {
        ConfigureForOptions(volume, bloom, motionBlur, depthOfField, chromaticAberration, filmGrain, null, null, null, null, null);
    }

    public void ConfigureForOptions(Component volume, Toggle bloom, Toggle motionBlur, Toggle depthOfField, Toggle chromaticAberration, Toggle filmGrain,
        Slider bloomControl, Slider motionBlurControl, Slider depthOfFieldControl, Slider chromaticAberrationControl, Slider filmGrainControl)
    {
        postProcessingVolume = volume;
        bloomToggle = bloom;
        motionBlurToggle = motionBlur;
        dofToggle = depthOfField;
        chromaticAberrationToggle = chromaticAberration;
        filmGrainToggle = filmGrain;
        bloomSlider = bloomControl;
        motionBlurSlider = motionBlurControl;
        dofSlider = depthOfFieldControl;
        chromaticAberrationSlider = chromaticAberrationControl;
        filmGrainSlider = filmGrainControl;
        initialized = false;
        Initialize();
    }

    private void Initialize()
    {
        if (initialized)
        {
            return;
        }

        CacheDefaultStates();
        RestoreSavedValues();
        initialized = true;
    }

    public void ApplySettings()
    {
        ApplyOverride(bloomToggle, bloomSlider, BloomKey, "Bloom");
        ApplyOverride(motionBlurToggle, motionBlurSlider, MotionBlurKey, "MotionBlur");
        ApplyOverride(dofToggle, dofSlider, DepthOfFieldKey, "DepthOfField");
        ApplyOverride(chromaticAberrationToggle, chromaticAberrationSlider, ChromaticAberrationKey, "ChromaticAberration");
        ApplyOverride(filmGrainToggle, filmGrainSlider, FilmGrainKey, "FilmGrain", "Grain");
        PlayerPrefs.Save();
    }

    private void CacheDefaultStates()
    {
        foreach (object component in GetPostProcessingComponents())
        {
            defaultStates[component.GetType().Name] = GetComponentActive(component);
        }
    }

    private void RestoreSavedValues()
    {
        RestoreControl(bloomToggle, bloomSlider, BloomKey, "Bloom");
        RestoreControl(motionBlurToggle, motionBlurSlider, MotionBlurKey, "MotionBlur");
        RestoreControl(dofToggle, dofSlider, DepthOfFieldKey, "DepthOfField");
        RestoreControl(chromaticAberrationToggle, chromaticAberrationSlider, ChromaticAberrationKey, "ChromaticAberration");
        RestoreControl(filmGrainToggle, filmGrainSlider, FilmGrainKey, "FilmGrain", "Grain");
    }

    private void RestoreControl(Toggle toggle, Slider slider, string preferenceKey, params string[] componentNames)
    {
        if (toggle == null && slider == null)
        {
            return;
        }

        bool defaultState = false;
        for (int i = 0; i < componentNames.Length; i++)
        {
            if (defaultStates.TryGetValue(componentNames[i], out bool value))
            {
                defaultState = value;
                break;
            }
        }
        bool active = PlayerPrefs.GetInt(preferenceKey, defaultState ? 1 : 0) == 1;
        if (toggle != null)
        {
            toggle.SetIsOnWithoutNotify(active);
        }

        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(active ? 0f : 1f);
        }
    }

    private void ApplyOverride(Toggle toggle, Slider slider, string preferenceKey, params string[] componentNames)
    {
        if (toggle == null && slider == null)
        {
            return;
        }

        bool active = slider != null ? slider.value < 0.5f : toggle.isOn;
        PlayerPrefs.SetInt(preferenceKey, active ? 1 : 0);
        foreach (object component in GetPostProcessingComponents())
        {
            for (int i = 0; i < componentNames.Length; i++)
            {
                if (component.GetType().Name == componentNames[i])
                {
                    SetComponentActive(component, active);
                    return;
                }
            }
        }

    }

    public void ResetToDefault()
    {
        PlayerPrefs.DeleteKey(BloomKey);
        PlayerPrefs.DeleteKey(MotionBlurKey);
        PlayerPrefs.DeleteKey(DepthOfFieldKey);
        PlayerPrefs.DeleteKey(ChromaticAberrationKey);
        PlayerPrefs.DeleteKey(FilmGrainKey);
        PlayerPrefs.Save();

        RestoreSavedValues();
        ApplySettings();
    }

    private IEnumerable GetPostProcessingComponents()
    {
        if (postProcessingVolume == null)
        {
            yield break;
        }

        object profile = ReadMember(postProcessingVolume, "profile") ?? ReadMember(postProcessingVolume, "sharedProfile");
        object components = profile == null ? null : ReadMember(profile, "components") ?? ReadMember(profile, "settings");
        IEnumerable collection = components as IEnumerable;
        if (collection == null)
        {
            yield break;
        }

        foreach (object component in collection)
        {
            if (component != null)
            {
                yield return component;
            }
        }
    }

    private static bool GetComponentActive(object component)
    {
        object value = ReadMember(component, "active");
        return value is bool && (bool)value;
    }

    private static void SetComponentActive(object component, bool active)
    {
        System.Type type = component.GetType();
        PropertyInfo property = type.GetProperty("active", BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            property.SetValue(component, active);
            return;
        }

        FieldInfo field = type.GetField("active", BindingFlags.Instance | BindingFlags.Public);
        if (field != null)
        {
            field.SetValue(component, active);
        }
    }

    private static object ReadMember(object target, string memberName)
    {
        System.Type type = target.GetType();
        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
        if (property != null && property.CanRead)
        {
            return property.GetValue(target);
        }

        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
        return field == null ? null : field.GetValue(target);
    }
}
