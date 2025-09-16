using UnityEditor;
using UnityEngine;

/// <summary>
///     Manages loading and saving the ArikoSettings ScriptableObject.
/// </summary>
public static class ArikoSettingsManager
{
    private const string SettingsPath = "Packages/io.github.pixel-clover.ariko/Editor/ArikoSettings.asset";

    /// <summary>
    ///     Loads the ArikoSettings asset from the default path.
    ///     If the asset does not exist, it creates a new one.
    /// </summary>
    /// <returns>The loaded or newly created ArikoSettings instance.</returns>
    public static ArikoSettings LoadSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<ArikoSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ArikoSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
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
