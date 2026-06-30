using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    public string key;
    public bool useCurrentTextAsKeyIfEmpty = true;

    private TMP_Text targetText;

    private void Awake()
    {
        targetText = GetComponent<TMP_Text>();
        if (useCurrentTextAsKeyIfEmpty && string.IsNullOrWhiteSpace(key) && targetText != null)
        {
            key = targetText.text;
        }

        Refresh();
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
            targetText = GetComponent<TMP_Text>();
        }

        if (targetText == null || string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        targetText.text = LocalizationManager.Get(key);
    }
}
