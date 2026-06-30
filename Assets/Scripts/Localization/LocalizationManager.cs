using System;
using TMPro;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    private const string LanguagePrefsKey = "Localization.Language";

    public static event Action LanguageChanged;
    public static LocalizationManager Instance { get; private set; }
    public static GameLanguage CurrentLanguage => Instance != null ? Instance.currentLanguage : GameLanguage.English;

    [SerializeField] private LocalizationTable table;
    [SerializeField] private GameLanguage currentLanguage = GameLanguage.English;
    [SerializeField] private TMP_Dropdown languageDropdown;

    private bool suppressDropdownCallback;

    public LocalizationTable Table => table;

    public static string Get(string key)
    {
        if (Instance == null || Instance.table == null)
        {
            return key;
        }

        return Instance.table.GetText(key, Instance.currentLanguage);
    }

    public void SetTable(LocalizationTable localizationTable)
    {
        table = localizationTable;
        if (table != null && !PlayerPrefs.HasKey(LanguagePrefsKey))
        {
            currentLanguage = table.defaultLanguage;
        }

        RefreshDropdown();
        NotifyLanguageChanged();
    }

    public void SetLanguage(GameLanguage language)
    {
        if (currentLanguage == language)
        {
            return;
        }

        currentLanguage = language;
        PlayerPrefs.SetInt(LanguagePrefsKey, (int)currentLanguage);
        PlayerPrefs.Save();
        RefreshDropdown();
        NotifyLanguageChanged();
    }

    public void SetLanguageByDropdownIndex(int index)
    {
        if (suppressDropdownCallback)
        {
            return;
        }

        SetLanguage((GameLanguage)Mathf.Clamp(index, 0, Enum.GetValues(typeof(GameLanguage)).Length - 1));
    }

    public void BindDropdown(TMP_Dropdown dropdown)
    {
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(SetLanguageByDropdownIndex);
        }

        languageDropdown = dropdown;
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.AddListener(SetLanguageByDropdownIndex);
        }

        RefreshDropdown();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadLanguage();
        BindDropdown(languageDropdown);
    }

    private void Start()
    {
        NotifyLanguageChanged();
    }

    private void OnDestroy()
    {
        if (languageDropdown != null)
        {
            languageDropdown.onValueChanged.RemoveListener(SetLanguageByDropdownIndex);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void LoadLanguage()
    {
        int defaultValue = table != null ? (int)table.defaultLanguage : (int)currentLanguage;
        int savedValue = PlayerPrefs.GetInt(LanguagePrefsKey, defaultValue);
        currentLanguage = (GameLanguage)Mathf.Clamp(savedValue, 0, Enum.GetValues(typeof(GameLanguage)).Length - 1);
    }

    private void RefreshDropdown()
    {
        if (languageDropdown == null)
        {
            return;
        }

        suppressDropdownCallback = true;
        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new System.Collections.Generic.List<string> { "English", "Indonesia" });
        languageDropdown.SetValueWithoutNotify((int)currentLanguage);
        languageDropdown.RefreshShownValue();
        suppressDropdownCallback = false;
    }

    private void NotifyLanguageChanged()
    {
        if (suppressDropdownCallback)
        {
            return;
        }

        LanguageChanged?.Invoke();
    }
}
