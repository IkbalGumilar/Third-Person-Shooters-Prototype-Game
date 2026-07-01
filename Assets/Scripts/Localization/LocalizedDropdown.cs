using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Dropdown))]
public class LocalizedDropdown : MonoBehaviour
{
    public string[] optionKeys;
    public bool useCurrentOptionsAsKeyIfEmpty = true;

    private TMP_Dropdown dropdown;

    private void Awake()
    {
        dropdown = GetComponent<TMP_Dropdown>();
        EnsureOptionKeys();
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
        if (dropdown == null)
        {
            dropdown = GetComponent<TMP_Dropdown>();
        }

        if (dropdown == null || optionKeys == null)
        {
            return;
        }

        int count = Mathf.Min(dropdown.options.Count, optionKeys.Length);
        for (int i = 0; i < count; i++)
        {
            string key = optionKeys[i];
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            dropdown.options[i].text = LocalizationManager.Get(key);
        }

        dropdown.RefreshShownValue();
    }

    private void EnsureOptionKeys()
    {
        if (!useCurrentOptionsAsKeyIfEmpty || dropdown == null)
        {
            return;
        }

        if (optionKeys != null && optionKeys.Length == dropdown.options.Count)
        {
            bool hasAnyKey = false;
            for (int i = 0; i < optionKeys.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(optionKeys[i]))
                {
                    hasAnyKey = true;
                    break;
                }
            }

            if (hasAnyKey)
            {
                return;
            }
        }

        optionKeys = new string[dropdown.options.Count];
        for (int i = 0; i < dropdown.options.Count; i++)
        {
            optionKeys[i] = dropdown.options[i].text;
        }
    }
}
