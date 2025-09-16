using UnityEditor;
using UnityEngine;

public static class ArikoSettingsManager
{
    private const string SettingsPath = "Packages/io.github.pixel-clover.ariko/Editor/ArikoSettings.asset";

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

    public static void SaveSettings(ArikoSettings settings)
    {
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
