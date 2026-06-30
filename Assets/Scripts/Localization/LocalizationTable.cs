using System;
using System.Collections.Generic;
using UnityEngine;

public enum GameLanguage
{
    English,
    Indonesian
}

[Serializable]
public class LocalizedTextEntry
{
    public string key;
    [TextArea(1, 4)] public string english;
    [TextArea(1, 4)] public string indonesian;
}

[CreateAssetMenu(fileName = "Localization Table", menuName = "Localization/Localization Table")]
public class LocalizationTable : ScriptableObject
{
    public GameLanguage defaultLanguage = GameLanguage.English;
    public LocalizedTextEntry[] entries;

    private Dictionary<string, LocalizedTextEntry> lookup;

    public string GetText(string key, GameLanguage language)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        LocalizedTextEntry entry = GetEntry(key);
        if (entry == null)
        {
            return key;
        }

        string text = language == GameLanguage.Indonesian ? entry.indonesian : entry.english;
        if (string.IsNullOrEmpty(text))
        {
            text = language == GameLanguage.Indonesian ? entry.english : entry.indonesian;
        }

        return string.IsNullOrEmpty(text) ? key : text;
    }

    private LocalizedTextEntry GetEntry(string key)
    {
        EnsureLookup();
        lookup.TryGetValue(key, out LocalizedTextEntry entry);
        return entry;
    }

    private void EnsureLookup()
    {
        if (lookup != null)
        {
            return;
        }

        lookup = new Dictionary<string, LocalizedTextEntry>();
        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Length; i++)
        {
            LocalizedTextEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            lookup[entry.key] = entry;
        }
    }

    private void OnValidate()
    {
        lookup = null;
    }
}
