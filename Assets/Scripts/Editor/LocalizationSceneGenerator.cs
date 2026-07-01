using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LocalizationSceneGenerator
{
    private const string DefaultTablePath = "Assets/Scripts/ScripTableObject/LocalizationData/Localization Table.asset";

    [MenuItem("Tools/Localization/Generate From Open Scene")]
    public static void GenerateFromOpenScene()
    {
        bool continueScan = EditorUtility.DisplayDialog(
            "Localization",
            "This will scan every TMP_Text in the active scene and add missing LocalizedText components. Continue?",
            "Generate",
            "Cancel");

        if (!continueScan)
        {
            return;
        }

        Generate(null, "open scene");
    }

    [MenuItem("Tools/Localization/Generate From Selected Objects")]
    public static void GenerateFromSelectedObjects()
    {
        if (Selection.transforms == null || Selection.transforms.Length == 0)
        {
            EditorUtility.DisplayDialog("Localization", "Select one or more UI objects in the Hierarchy first.", "OK");
            return;
        }

        Generate(Selection.transforms, "selected objects");
    }

    [MenuItem("Tools/Localization/Generate From Selected Objects", true)]
    public static bool CanGenerateFromSelectedObjects()
    {
        return Selection.transforms != null && Selection.transforms.Length > 0;
    }

    private static void Generate(IReadOnlyList<Transform> selectedRoots, string sourceLabel)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("Localization", "No active scene found.", "OK");
            return;
        }

        LocalizationTable table = FindOrCreateTable();
        if (table == null)
        {
            EditorUtility.DisplayDialog("Localization", "Could not find or create a Localization Table asset.", "OK");
            return;
        }

        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        Dictionary<string, LocalizedTextEntry> entriesByKey = BuildEntryLookup(table);
        HashSet<string> usedKeys = new HashSet<string>(entriesByKey.Keys);

        int scanned = 0;
        int addedComponents = 0;
        int addedDropdowns = 0;
        int addedEntries = 0;
        int updatedEnglish = 0;
        int skipped = 0;

        Undo.RecordObject(table, "Generate Localization Entries");

        TMP_Dropdown[] dropdowns = Resources.FindObjectsOfTypeAll<TMP_Dropdown>();
        foreach (TMP_Dropdown dropdown in dropdowns)
        {
            if (!IsSceneObject(dropdown.gameObject, scene))
            {
                continue;
            }

            if (selectedRoots != null && !IsUnderAnyRoot(dropdown.transform, selectedRoots))
            {
                continue;
            }

            LocalizedDropdown localizedDropdown = dropdown.GetComponent<LocalizedDropdown>();
            if (localizedDropdown == null)
            {
                localizedDropdown = Undo.AddComponent<LocalizedDropdown>(dropdown.gameObject);
                localizedDropdown.useCurrentOptionsAsKeyIfEmpty = false;
                addedDropdowns++;
            }

            if (localizedDropdown.optionKeys == null || localizedDropdown.optionKeys.Length != dropdown.options.Count)
            {
                localizedDropdown.optionKeys = new string[dropdown.options.Count];
            }

            for (int i = 0; i < dropdown.options.Count; i++)
            {
                string english = NormalizeText(dropdown.options[i].text);
                if (ShouldSkipText(english))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(localizedDropdown.optionKeys[i]))
                {
                    localizedDropdown.optionKeys[i] = MakeUniqueKey(scene.name, dropdown.transform, usedKeys, $"option.{i + 1}");
                    EditorUtility.SetDirty(localizedDropdown);
                }

                AddOrUpdateEntry(entriesByKey, localizedDropdown.optionKeys[i], english, ref addedEntries, ref updatedEnglish);
                usedKeys.Add(localizedDropdown.optionKeys[i]);
            }
        }

        foreach (TMP_Text text in texts)
        {
            if (!IsSceneText(text, scene))
            {
                continue;
            }

            if (IsDropdownRuntimeLabel(text))
            {
                skipped++;
                continue;
            }

            if (selectedRoots != null && !IsUnderAnyRoot(text.transform, selectedRoots))
            {
                continue;
            }

            string english = NormalizeText(text.text);
            if (ShouldSkipText(english))
            {
                skipped++;
                continue;
            }

            scanned++;

            LocalizedText localizedText = text.GetComponent<LocalizedText>();
            if (localizedText == null)
            {
                localizedText = Undo.AddComponent<LocalizedText>(text.gameObject);
                localizedText.useCurrentTextAsKeyIfEmpty = false;
                addedComponents++;
            }

            if (string.IsNullOrWhiteSpace(localizedText.key))
            {
                localizedText.key = MakeUniqueKey(scene.name, text.transform, usedKeys);
                localizedText.useCurrentTextAsKeyIfEmpty = false;
                EditorUtility.SetDirty(localizedText);
            }

            usedKeys.Add(localizedText.key);

            AddOrUpdateEntry(entriesByKey, localizedText.key, english, ref addedEntries, ref updatedEnglish);
        }

        table.entries = ToSortedArray(entriesByKey);
        table.ClearLookupCache();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        EditorUtility.DisplayDialog(
            "Localization",
            $"Source: {sourceLabel}\nScene: {scene.name}\nScanned TMP texts: {scanned}\nSkipped value texts: {skipped}\nAdded LocalizedText components: {addedComponents}\nAdded LocalizedDropdown components: {addedDropdowns}\nAdded table entries: {addedEntries}\nUpdated empty English entries: {updatedEnglish}",
            "OK");
    }

    [MenuItem("Tools/Localization/Select Localization Table")]
    public static void SelectLocalizationTable()
    {
        LocalizationTable table = FindOrCreateTable();
        if (table == null)
        {
            return;
        }

        Selection.activeObject = table;
        EditorGUIUtility.PingObject(table);
    }

    private static LocalizationTable FindOrCreateTable()
    {
        LocalizationTable table = AssetDatabase.LoadAssetAtPath<LocalizationTable>(DefaultTablePath);
        if (table != null)
        {
            return table;
        }

        string[] guids = AssetDatabase.FindAssets("t:LocalizationTable");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<LocalizationTable>(path);
        }

        EnsureFolder("Assets/Scripts/ScripTableObject");
        EnsureFolder("Assets/Scripts/ScripTableObject/LocalizationData");

        table = ScriptableObject.CreateInstance<LocalizationTable>();
        AssetDatabase.CreateAsset(table, DefaultTablePath);
        AssetDatabase.SaveAssets();
        return table;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        string parent = System.IO.Path.GetDirectoryName(folderPath).Replace("\\", "/");
        string folder = System.IO.Path.GetFileName(folderPath);
        AssetDatabase.CreateFolder(parent, folder);
    }

    private static Dictionary<string, LocalizedTextEntry> BuildEntryLookup(LocalizationTable table)
    {
        Dictionary<string, LocalizedTextEntry> lookup = new Dictionary<string, LocalizedTextEntry>();
        if (table.entries == null)
        {
            return lookup;
        }

        foreach (LocalizedTextEntry entry in table.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            lookup[entry.key] = entry;
        }

        return lookup;
    }

    private static LocalizedTextEntry[] ToSortedArray(Dictionary<string, LocalizedTextEntry> entriesByKey)
    {
        List<LocalizedTextEntry> entries = new List<LocalizedTextEntry>(entriesByKey.Values);
        entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
        return entries.ToArray();
    }

    private static bool IsSceneText(TMP_Text text, Scene activeScene)
    {
        if (text == null || text.gameObject == null)
        {
            return false;
        }

        return IsSceneObject(text.gameObject, activeScene);
    }

    private static bool IsSceneObject(GameObject gameObject, Scene activeScene)
    {
        if (gameObject == null)
        {
            return false;
        }

        if (EditorUtility.IsPersistent(gameObject))
        {
            return false;
        }

        return gameObject.scene == activeScene;
    }

    private static bool IsDropdownRuntimeLabel(TMP_Text text)
    {
        TMP_Dropdown dropdown = text.GetComponentInParent<TMP_Dropdown>(true);
        if (dropdown == null)
        {
            return false;
        }

        return text == dropdown.captionText || text == dropdown.itemText;
    }

    private static bool IsUnderAnyRoot(Transform transform, IReadOnlyList<Transform> roots)
    {
        for (int i = 0; i < roots.Count; i++)
        {
            Transform root = roots[i];
            if (root != null && (transform == root || transform.IsChildOf(root)))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n").Trim();
    }

    private static bool ShouldSkipText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        string noTags = Regex.Replace(text, "<.*?>", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(noTags))
        {
            return true;
        }

        if (Regex.IsMatch(noTags, @"^[\d\s.,/%+\-:()]+$"))
        {
            return true;
        }

        return false;
    }

    private static string MakeUniqueKey(string sceneName, Transform transform, HashSet<string> usedKeys)
    {
        return MakeUniqueKey(sceneName, transform, usedKeys, null);
    }

    private static string MakeUniqueKey(string sceneName, Transform transform, HashSet<string> usedKeys, string suffix)
    {
        string rawKey = string.IsNullOrWhiteSpace(suffix)
            ? $"{sceneName}.{GetHierarchyPath(transform)}"
            : $"{sceneName}.{GetHierarchyPath(transform)}.{suffix}";
        string baseKey = SanitizeKey(rawKey);
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            baseKey = "localized.text";
        }

        string key = baseKey;
        int index = 2;
        while (usedKeys.Contains(key))
        {
            key = $"{baseKey}_{index}";
            index++;
        }

        return key;
    }

    private static void AddOrUpdateEntry(
        Dictionary<string, LocalizedTextEntry> entriesByKey,
        string key,
        string english,
        ref int addedEntries,
        ref int updatedEnglish)
    {
        if (!entriesByKey.TryGetValue(key, out LocalizedTextEntry entry))
        {
            entry = new LocalizedTextEntry
            {
                key = key,
                english = english,
                indonesian = english
            };

            entriesByKey.Add(entry.key, entry);
            addedEntries++;
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.english))
        {
            entry.english = english;
            updatedEnglish++;
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        Stack<string> names = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join(".", names);
    }

    private static string SanitizeKey(string rawKey)
    {
        string lower = rawKey.ToLowerInvariant();
        StringBuilder builder = new StringBuilder(lower.Length);

        bool lastWasSeparator = false;
        foreach (char c in lower)
        {
            bool valid = char.IsLetterOrDigit(c);
            if (valid)
            {
                builder.Append(c);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('.');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().Trim('.');
    }
}
