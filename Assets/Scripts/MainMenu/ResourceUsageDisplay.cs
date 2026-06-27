using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

/// <summary>Updates the options-menu RAM and VRAM usage readouts.</summary>
public sealed class ResourceUsageDisplay : MonoBehaviour
{
    private static readonly MethodInfo GraphicsDriverMemoryMethod = typeof(Profiler).GetMethod("GetAllocatedMemoryForGraphicsDriver", BindingFlags.Public | BindingFlags.Static);

    [SerializeField] private Slider ramSlider;
    [SerializeField] private TMP_Text ramText;
    [SerializeField] private Slider vramSlider;
    [SerializeField] private TMP_Text vramText;
    [SerializeField] private float refreshInterval = 0.25f;

    private float nextRefreshTime;

    public void BindFrom(Transform root)
    {
        Transform ramRoot = FindChild(root, "Ram Usage");
        Transform vramRoot = FindChild(root, "Vram Usage");

        ramSlider = FindComponentInNamedChild<Slider>(ramRoot, "Slider");
        vramSlider = FindComponentInNamedChild<Slider>(vramRoot, "Slider");
        ramText = FindUsageText(ramRoot);
        vramText = FindUsageText(vramRoot);

        Refresh();
    }

    private void OnEnable()
    {
        nextRefreshTime = 0f;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
        {
            return;
        }

        Refresh();
    }

    private void Refresh()
    {
        nextRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);

        float totalRamGb = Mathf.Max(0.01f, SystemInfo.systemMemorySize / 1024f);
        float usedRamGb = Mathf.Max(0f, Profiler.GetTotalReservedMemoryLong() / 1073741824f);
        UpdateUsage(ramSlider, ramText, usedRamGb, totalRamGb);

        float totalVramGb = Mathf.Max(0.01f, SystemInfo.graphicsMemorySize / 1024f);
        float usedVramGb = Mathf.Max(0f, GetGraphicsDriverMemoryGb());
        UpdateUsage(vramSlider, vramText, usedVramGb, totalVramGb);
    }

    private static float GetGraphicsDriverMemoryGb()
    {
        if (GraphicsDriverMemoryMethod == null)
        {
            return 0f;
        }

        object value = GraphicsDriverMemoryMethod.Invoke(null, null);
        long bytes = value is long longValue ? longValue : 0L;
        return bytes > 0 ? bytes / 1073741824f : 0f;
    }

    private static void UpdateUsage(Slider slider, TMP_Text text, float usedGb, float totalGb)
    {
        float clampedUsed = Mathf.Clamp(usedGb, 0f, totalGb);
        if (slider != null)
        {
            slider.minValue = 0f;
            slider.maxValue = totalGb;
            slider.SetValueWithoutNotify(clampedUsed);
        }

        if (text != null)
        {
            text.text = $"{FormatGb(usedGb)} / {FormatGb(totalGb)}";
        }
    }

    private static string FormatGb(float value)
    {
        return value < 10f ? $"{value:0.0} GB" : $"{value:0} GB";
    }

    private static TMP_Text FindUsageText(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].transform.name == "Vram" || texts[i].transform.name == "Ram")
            {
                return texts[i];
            }
        }

        return texts.Length > 0 ? texts[texts.Length - 1] : null;
    }

    private static T FindComponentInNamedChild<T>(Transform root, string childName) where T : Component
    {
        Transform child = FindChild(root, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }
}
