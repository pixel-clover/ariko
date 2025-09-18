using System.Text;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ContextBuilder
{
    public string BuildContextString(bool autoContext, Object currentSelection, IEnumerable<Object> manuallyAttachedAssets)
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
            foreach (var asset in manuallyAttachedAssets)
            {
                AppendAssetInfo(asset, contextBuilder);
            }
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
