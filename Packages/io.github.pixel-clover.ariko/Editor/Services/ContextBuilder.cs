using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
///     Responsible for building a context string from the current Unity Editor selection and manually attached assets.
/// </summary>
public class ContextBuilder
{
    /// <summary>
    ///     Builds a formatted string containing context from the current selection and manually attached assets.
    /// </summary>
    /// <param name="autoContext">Whether to include context from the current selection.</param>
    /// <param name="currentSelection">The currently selected object in the Unity Editor.</param>
    /// <param name="manuallyAttachedAssets">A collection of assets that have been manually attached.</param>
    /// <returns>A formatted string containing the context information.</returns>
    public string BuildContextString(bool autoContext, Object currentSelection,
        IEnumerable<Object> manuallyAttachedAssets)
    {
        var contextBuilder = new StringBuilder();
        if (autoContext && currentSelection != null)
        {
            contextBuilder.AppendLine("--- Current Selection Context ---");
            AppendAssetInfo(currentSelection, contextBuilder);
        }

        if (manuallyAttachedAssets != null && manuallyAttachedAssets.Any())
        {
            contextBuilder.AppendLine("--- Manually Attached Context ---");
            foreach (var asset in manuallyAttachedAssets) AppendAssetInfo(asset, contextBuilder);
        }

        return contextBuilder.ToString();
    }

    private static void AppendAssetInfo(Object asset, StringBuilder builder)
    {
        if (asset is MonoScript script)
            builder.AppendLine($"[File: {script.name}.cs]\n```csharp\n{script.text}\n```");
        else if (asset is TextAsset textAsset)
            builder.AppendLine($"[File: {textAsset.name}]\n```\n{textAsset.text}\n```");
        else
            builder.AppendLine(
                $"[Asset: {asset.name} ({asset.GetType().Name}) at path {AssetDatabase.GetAssetPath(asset)}]");
        builder.AppendLine();
    }
}
