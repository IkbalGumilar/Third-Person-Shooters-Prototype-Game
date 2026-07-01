using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Text))]
public sealed class LocalizedLegacyText : MonoBehaviour
{
    [SerializeField] private string englishText;
    [Header("Responsive Text")]
    [SerializeField] private bool applyBestFit = true;
    [SerializeField] private int minFontSize = 10;
    private Text targetText;
    private int initialFontSize;

    public void SetEnglishText(string value)
    {
        englishText = value;
        Refresh();
    }

    private void Awake()
    {
        targetText = GetComponent<Text>();
        initialFontSize = targetText != null ? targetText.fontSize : 0;
    }

    private void OnEnable()
    {
        LocalizationManager.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.LanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        if (targetText == null)
        {
            targetText = GetComponent<Text>();
        }

        if (targetText != null)
        {
            targetText.text = LocalizationManager.GetText(englishText);
            ApplyResponsiveTextSettings();
        }
    }

    private void ApplyResponsiveTextSettings()
    {
        if (!applyBestFit || targetText == null)
        {
            return;
        }

        int maxFontSize = initialFontSize > 0 ? initialFontSize : targetText.fontSize;
        targetText.resizeTextForBestFit = true;
        targetText.resizeTextMinSize = Mathf.Max(1, minFontSize);
        targetText.resizeTextMaxSize = Mathf.Max(targetText.resizeTextMinSize, maxFontSize);
        targetText.horizontalOverflow = HorizontalWrapMode.Wrap;
        targetText.verticalOverflow = VerticalWrapMode.Truncate;
    }
}
