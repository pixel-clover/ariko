using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Manages loading and saving the ArikoSettings ScriptableObject.
/// </summary>
public static class ArikoSettingsManager
{
    private const string DefaultSettingsPath = "Assets/Ariko/Settings/ArikoSettings.asset";

    /// <summary>
    ///     Loads the ArikoSettings asset from the project.
    ///     If the asset does not exist, it creates a new one at the default path.
    /// </summary>
    /// <returns>The loaded or newly created ArikoSettings instance.</returns>
    public static ArikoSettings LoadSettings()
    {
        var guids = AssetDatabase.FindAssets("t:ArikoSettings");
        ArikoSettings settings = null;

        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            settings = AssetDatabase.LoadAssetAtPath<ArikoSettings>(path);
        }

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ArikoSettings>();
            Directory.CreateDirectory(Path.GetDirectoryName(DefaultSettingsPath));
            AssetDatabase.CreateAsset(settings, DefaultSettingsPath);
            AssetDatabase.SaveAssets();
        }

        if (settings.syntaxTheme == null)
        {
            var themeGuids = AssetDatabase.FindAssets("t:SyntaxTheme");
            if (themeGuids.Length > 0)
                settings.syntaxTheme =
                    AssetDatabase.LoadAssetAtPath<SyntaxTheme>(AssetDatabase.GUIDToAssetPath(themeGuids[0]));
        }

        if (settings.languageDefinitions == null || settings.languageDefinitions.Count == 0)
        {
            settings.languageDefinitions = new List<LanguageDefinition>();
            var langGuids = AssetDatabase.FindAssets("t:LanguageDefinition");
            foreach (var guid in langGuids)
            {
                var langDef = AssetDatabase.LoadAssetAtPath<LanguageDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (langDef != null) settings.languageDefinitions.Add(langDef);
            }
        }

        return settings;
    }

    /// <summary>
    ///     Saves the specified ArikoSettings asset to disk.
    /// </summary>
    /// <param name="settings">The settings object to save.</param>
    public static void SaveSettings(ArikoSettings settings)
    {
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
