using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

public enum AntiAliasingModeOption
{
    Off,
    Msaa2x,
    Msaa4x,
    Msaa8x,
    Fxaa,
    Taa,
    Ssaa15x,
    Ssaa2x,
    Fsr,
    Dlss,
    Dlaa
}

public static class AntiAliasingSettingsUtility
{
    private static readonly float DefaultRenderScale = 1f;

    private static readonly AntiAliasingModeOption[] Options =
    {
        AntiAliasingModeOption.Off,
        AntiAliasingModeOption.Msaa2x,
        AntiAliasingModeOption.Msaa4x,
        AntiAliasingModeOption.Msaa8x,
        AntiAliasingModeOption.Fxaa,
        AntiAliasingModeOption.Taa,
        AntiAliasingModeOption.Ssaa15x,
        AntiAliasingModeOption.Ssaa2x,
        AntiAliasingModeOption.Fsr,
        AntiAliasingModeOption.Dlss,
        AntiAliasingModeOption.Dlaa
    };

    public static int OptionCount => Options.Length;

    public static List<string> GetOptionLabels()
    {
        var labels = new List<string>(Options.Length);
        for (int i = 0; i < Options.Length; i++)
        {
            AntiAliasingModeOption option = Options[i];
            string label = GetLabel(option);
            if (!IsSupported(option))
            {
                label += " (Unsupported)";
            }

            labels.Add(label);
        }

        return labels;
    }

    public static int GetDefaultOptionIndex()
    {
        return QualitySettings.antiAliasing switch
        {
            2 => GetIndex(AntiAliasingModeOption.Msaa2x),
            4 => GetIndex(AntiAliasingModeOption.Msaa4x),
            8 => GetIndex(AntiAliasingModeOption.Msaa8x),
            _ => GetIndex(AntiAliasingModeOption.Off)
        };
    }

    public static int ClampOptionIndex(int index)
    {
        return Mathf.Clamp(index, 0, Options.Length - 1);
    }

    public static int GetEffectiveOptionIndex(int index)
    {
        int clampedIndex = ClampOptionIndex(index);
        return IsSupported(clampedIndex) ? clampedIndex : GetIndex(AntiAliasingModeOption.Off);
    }

    public static void Apply(int optionIndex)
    {
        AntiAliasingModeOption option = Options[GetEffectiveOptionIndex(optionIndex)];

        ResetRuntimeAntiAliasing();

        switch (option)
        {
            case AntiAliasingModeOption.Msaa2x:
                ApplyMsaa(2);
                break;
            case AntiAliasingModeOption.Msaa4x:
                ApplyMsaa(4);
                break;
            case AntiAliasingModeOption.Msaa8x:
                ApplyMsaa(8);
                break;
            case AntiAliasingModeOption.Fxaa:
                ApplyFxaa();
                break;
            case AntiAliasingModeOption.Taa:
                ApplyCameraAntialiasing("TemporalAntiAliasing", "TAA");
                break;
            case AntiAliasingModeOption.Ssaa15x:
                ApplySsaa(1.5f);
                break;
            case AntiAliasingModeOption.Ssaa2x:
                ApplySsaa(2f);
                break;
            case AntiAliasingModeOption.Fsr:
                ApplyUpscaler("Fsr", "FSR");
                break;
            case AntiAliasingModeOption.Dlss:
                ApplyUpscaler("DLSS", "DeepLearningSuperSampling");
                break;
            case AntiAliasingModeOption.Dlaa:
                ApplyCameraAntialiasing("DLAA", "DeepLearningAntiAliasing");
                break;
            default:
                ApplyMsaa(0);
                break;
        }
    }

    public static bool IsSupported(int optionIndex)
    {
        return IsSupported(Options[ClampOptionIndex(optionIndex)]);
    }

    private static bool IsSupported(AntiAliasingModeOption option)
    {
        return option switch
        {
            AntiAliasingModeOption.Off => true,
            AntiAliasingModeOption.Msaa2x => true,
            AntiAliasingModeOption.Msaa4x => true,
            AntiAliasingModeOption.Msaa8x => true,
            AntiAliasingModeOption.Fxaa => true,
            AntiAliasingModeOption.Taa => HasCameraAntialiasingMode("TemporalAntiAliasing", "TAA"),
            AntiAliasingModeOption.Ssaa15x => HasRenderScale(),
            AntiAliasingModeOption.Ssaa2x => HasRenderScale(),
            AntiAliasingModeOption.Fsr => HasUpscaler("Fsr", "FSR"),
            AntiAliasingModeOption.Dlss => HasUpscaler("DLSS", "DeepLearningSuperSampling"),
            AntiAliasingModeOption.Dlaa => HasCameraAntialiasingMode("DLAA", "DeepLearningAntiAliasing"),
            _ => false
        };
    }

    private static int GetIndex(AntiAliasingModeOption option)
    {
        for (int i = 0; i < Options.Length; i++)
        {
            if (Options[i] == option)
            {
                return i;
            }
        }

        return 0;
    }

    private static string GetLabel(AntiAliasingModeOption option)
    {
        return option switch
        {
            AntiAliasingModeOption.Msaa2x => "MSAA 2x",
            AntiAliasingModeOption.Msaa4x => "MSAA 4x",
            AntiAliasingModeOption.Msaa8x => "MSAA 8x",
            AntiAliasingModeOption.Fxaa => "FXAA",
            AntiAliasingModeOption.Taa => "TAA",
            AntiAliasingModeOption.Ssaa15x => "SSAA 1.5x",
            AntiAliasingModeOption.Ssaa2x => "SSAA 2x",
            AntiAliasingModeOption.Fsr => "FSR",
            AntiAliasingModeOption.Dlss => "DLSS",
            AntiAliasingModeOption.Dlaa => "DLAA",
            _ => "Off"
        };
    }

    private static void ResetRuntimeAntiAliasing()
    {
        ApplyMsaa(0);
        SetRenderScale(DefaultRenderScale);
        DisableFxaa();
        ApplyCameraAntialiasing("None", "NoAntialiasing", "NoAntiAliasing", "Off", "Disabled");
    }

    private static void ApplyMsaa(int samples)
    {
        QualitySettings.antiAliasing = samples;
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].allowMSAA = samples > 0;
        }

        SetRenderPipelineMember("msaaSampleCount", samples);
        SetRenderPipelineMember("msaaSamples", samples);
    }

    private static void ApplyFxaa()
    {
        QualitySettings.antiAliasing = 0;
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            ApplyCameraAntialiasing("FastApproximateAntialiasing", "FXAA");
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        RuntimeFxaaEffect effect = mainCamera.GetComponent<RuntimeFxaaEffect>();
        if (effect == null)
        {
            effect = mainCamera.gameObject.AddComponent<RuntimeFxaaEffect>();
        }

        effect.hideFlags = HideFlags.DontSave;
        effect.enabled = true;
    }

    private static void DisableFxaa()
    {
        RuntimeFxaaEffect[] effects = UnityEngine.Object.FindObjectsByType<RuntimeFxaaEffect>(FindObjectsInactive.Include);
        for (int i = 0; i < effects.Length; i++)
        {
            if (effects[i] != null)
            {
                effects[i].enabled = false;
            }
        }
    }

    private static bool HasRenderScale()
    {
        return GetRenderPipelineMember("renderScale") != null;
    }

    private static void ApplySsaa(float scale)
    {
        ApplyMsaa(0);
        SetRenderScale(Mathf.Clamp(scale, 1f, 2f));
    }

    private static bool HasUpscaler(params string[] enumNames)
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null)
        {
            return false;
        }

        MemberInfo member = GetRenderPipelineMember("upscalingFilter")
            ?? GetRenderPipelineMember("upscaler")
            ?? GetRenderPipelineMember("fsrOverrideSharpness");
        if (member == null)
        {
            return false;
        }

        Type memberType = GetMemberType(member);
        if (memberType == typeof(float))
        {
            return ContainsName(enumNames, "Fsr") || ContainsName(enumNames, "FSR");
        }

        return TryParseEnum(memberType, enumNames, out _);
    }

    private static void ApplyUpscaler(params string[] enumNames)
    {
        ApplyMsaa(0);
        SetRenderScale(0.77f);
        SetRenderPipelineEnumMember("upscalingFilter", enumNames);
        SetRenderPipelineEnumMember("upscaler", enumNames);
    }

    private static void SetRenderScale(float scale)
    {
        SetRenderPipelineMember("renderScale", scale);
        SetRenderPipelineMember("resolutionScale", scale);
    }

    private static bool HasCameraAntialiasingMode(params string[] enumNames)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Component additionalData = GetAdditionalCameraData(camera);
        if (additionalData == null)
        {
            return false;
        }

        MemberInfo member = GetMember(additionalData.GetType(), "antialiasing")
            ?? GetMember(additionalData.GetType(), "antialiasingMode");
        return member != null && TryParseEnum(GetMemberType(member), enumNames, out _);
    }

    private static void ApplyCameraAntialiasing(params string[] enumNames)
    {
        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            Component additionalData = GetAdditionalCameraData(cameras[i]);
            if (additionalData == null)
            {
                continue;
            }

            SetEnumMember(additionalData, "antialiasing", enumNames);
            SetEnumMember(additionalData, "antialiasingMode", enumNames);
        }
    }

    private static Component GetAdditionalCameraData(Camera camera)
    {
        if (camera == null)
        {
            return null;
        }

        Component[] components = camera.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
            {
                continue;
            }

            string typeName = component.GetType().Name;
            if (typeName.Contains("AdditionalCameraData"))
            {
                return component;
            }
        }

        return null;
    }

    private static bool SetRenderPipelineEnumMember(string memberName, params string[] enumNames)
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        return asset != null && SetEnumMember(asset, memberName, enumNames);
    }

    private static bool SetRenderPipelineMember(string memberName, object value)
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        if (asset == null)
        {
            return false;
        }

        MemberInfo member = GetMember(asset.GetType(), memberName);
        if (member == null)
        {
            return false;
        }

        return SetMemberValue(asset, member, value);
    }

    private static MemberInfo GetRenderPipelineMember(string memberName)
    {
        RenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline;
        return asset == null ? null : GetMember(asset.GetType(), memberName);
    }

    private static bool SetEnumMember(object target, string memberName, params string[] enumNames)
    {
        if (target == null)
        {
            return false;
        }

        MemberInfo member = GetMember(target.GetType(), memberName);
        if (member == null || !TryParseEnum(GetMemberType(member), enumNames, out object value))
        {
            return false;
        }

        return SetMemberValue(target, member, value);
    }

    private static bool TryParseEnum(Type enumType, string[] names, out object value)
    {
        value = null;
        if (enumType == null || !enumType.IsEnum)
        {
            return false;
        }

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            if (Enum.TryParse(enumType, name, true, out object parsed))
            {
                value = parsed;
                return true;
            }
        }

        return false;
    }

    private static bool SetMemberValue(object target, MemberInfo member, object value)
    {
        try
        {
            if (member is PropertyInfo property && property.CanWrite)
            {
                property.SetValue(target, ConvertValue(value, property.PropertyType));
                return true;
            }

            if (member is FieldInfo field)
            {
                field.SetValue(target, ConvertValue(value, field.FieldType));
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static object ConvertValue(object value, Type targetType)
    {
        if (targetType == typeof(float))
        {
            return Convert.ToSingle(value);
        }

        if (targetType == typeof(int))
        {
            return Convert.ToInt32(value);
        }

        return value;
    }

    private static Type GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => null
        };
    }

    private static MemberInfo GetMember(Type type, string memberName)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetProperty(memberName, flags) as MemberInfo ?? type.GetField(memberName, flags);
    }

    private static bool ContainsName(string[] names, string expected)
    {
        for (int i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
