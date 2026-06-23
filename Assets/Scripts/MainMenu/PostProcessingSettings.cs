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

    private readonly Dictionary<string, bool> defaultStates = new();
    private bool initialized;

    private void Start()
    {
        Initialize();
    }

    public void ConfigureForOptions(Component volume, Toggle bloom, Toggle motionBlur, Toggle depthOfField, Toggle chromaticAberration, Toggle filmGrain)
    {
        postProcessingVolume = volume;
        bloomToggle = bloom;
        motionBlurToggle = motionBlur;
        dofToggle = depthOfField;
        chromaticAberrationToggle = chromaticAberration;
        filmGrainToggle = filmGrain;
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
        ApplyOverride(bloomToggle, BloomKey, "Bloom");
        ApplyOverride(motionBlurToggle, MotionBlurKey, "MotionBlur");
        ApplyOverride(dofToggle, DepthOfFieldKey, "DepthOfField");
        ApplyOverride(chromaticAberrationToggle, ChromaticAberrationKey, "ChromaticAberration");
        ApplyOverride(filmGrainToggle, FilmGrainKey, "FilmGrain", "Grain");
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
        RestoreToggle(bloomToggle, BloomKey, "Bloom");
        RestoreToggle(motionBlurToggle, MotionBlurKey, "MotionBlur");
        RestoreToggle(dofToggle, DepthOfFieldKey, "DepthOfField");
        RestoreToggle(chromaticAberrationToggle, ChromaticAberrationKey, "ChromaticAberration");
        RestoreToggle(filmGrainToggle, FilmGrainKey, "FilmGrain", "Grain");
    }

    private void RestoreToggle(Toggle toggle, string preferenceKey, params string[] componentNames)
    {
        if (toggle == null)
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
        toggle.SetIsOnWithoutNotify(PlayerPrefs.GetInt(preferenceKey, defaultState ? 1 : 0) == 1);
    }

    private void ApplyOverride(Toggle toggle, string preferenceKey, params string[] componentNames)
    {
        if (toggle == null)
        {
            return;
        }

        PlayerPrefs.SetInt(preferenceKey, toggle.isOn ? 1 : 0);
        foreach (object component in GetPostProcessingComponents())
        {
            for (int i = 0; i < componentNames.Length; i++)
            {
                if (component.GetType().Name == componentNames[i])
                {
                    SetComponentActive(component, toggle.isOn);
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
