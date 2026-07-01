using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    public bool useCurrentTextAsKeyIfEmpty = true;
    [Header("Responsive Text")]
    public bool applyAutoSize = true;
    public float minFontSize = 10f;
    public bool capMaxFontSizeToCurrent = true;
    public bool enableWordWrapping = true;
    public TextOverflowModes overflowMode = TextOverflowModes.Ellipsis;

    private TMP_Text targetText;
    private bool ignoreLocalization;
    private float initialFontSize;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
        initialFontSize = targetText != null ? targetText.fontSize : 0f;
        ignoreLocalization = IsDropdownRuntimeLabel();
        if (useCurrentTextAsKeyIfEmpty && string.IsNullOrWhiteSpace(key) && targetText != null)
        {
            key = targetText.text;
        }

        Refresh();
    }

    private void OnEnable()
    {
        if (ignoreLocalization)
        {
            return;
        }

        LocalizationManager.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.LanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        if (ignoreLocalization)
        {
            return;
        }

        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (targetText == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        targetText.text = LocalizationManager.Get(key);
        ApplyResponsiveTextSettings();
    }

    private void ApplyResponsiveTextSettings()
    {
        if (!applyAutoSize || targetText == null)
        {
            return;
        }

        float maxFontSize = initialFontSize > 0f ? initialFontSize : targetText.fontSize;
        targetText.enableAutoSizing = true;
        targetText.fontSizeMin = Mathf.Max(1f, minFontSize);
        if (capMaxFontSizeToCurrent)
        {
            targetText.fontSizeMax = Mathf.Max(targetText.fontSizeMin, maxFontSize);
        }

        targetText.enableWordWrapping = enableWordWrapping;
        targetText.overflowMode = overflowMode;
    }

    private bool IsDropdownRuntimeLabel()
    {
        if (targetText == null)
        {
            return false;
        }

        TMP_Dropdown dropdown = targetText.GetComponentInParent<TMP_Dropdown>(true);
        if (dropdown == null)
        {
            return false;
        }

        return targetText == dropdown.captionText || targetText == dropdown.itemText;
    }
}
