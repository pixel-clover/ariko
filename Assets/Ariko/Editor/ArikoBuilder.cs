using System.IO;
using UnityEditor;
using UnityEngine;

public class ArikoBuilder
{
    [MenuItem("Ariko/Build Package")]
    public static void BuildPackage()
    {
        var assetPaths = new[] { "Assets/Ariko" };
        var buildDirectory = "Builds";
        var exportPath = Path.Combine(buildDirectory, "Ariko.unitypackage");

        Debug.Log($"Exporting package from multiple paths to '{exportPath}'");

        if (!Directory.Exists(buildDirectory)) Directory.CreateDirectory(buildDirectory);

        AssetDatabase.ExportPackage(
            assetPaths,
            exportPath,
            ExportPackageOptions.Recurse);

        Debug.Log($"Export complete. File is at: {exportPath}");
    }
}
