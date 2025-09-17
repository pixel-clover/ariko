using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Ariko.Editor.Agent.Tools
{
    /// <summary>
    ///     A tool for modifying an existing file.
    /// </summary>
    public class ModifyFileTool : IArikoTool
    {
        /// <inheritdoc />
        public string Name => "ModifyFile";

        /// <inheritdoc />
        public string Description => "Modifies an existing file by replacing a section of its content.";

        /// <inheritdoc />
        public Dictionary<string, string> Parameters => new()
        {
            { "filePath", "The path of the file to modify. Should be relative to the Assets folder." },
            { "startLine", "The line number to start replacing from." },
            { "endLine", "The line number to stop replacing at." },
            { "content", "The new content to insert." }
        };

        /// <inheritdoc />
        public async Task<string> Execute(ToolExecutionContext context)
        {
            if (context.Arguments.TryGetValue("filePath", out var filePathObj) && filePathObj is string filePath &&
                context.Arguments.TryGetValue("startLine", out var startLineObj) && int.TryParse(startLineObj.ToString(), out var startLine) &&
                context.Arguments.TryGetValue("endLine", out var endLineObj) && int.TryParse(endLineObj.ToString(), out var endLine) &&
                context.Arguments.TryGetValue("content", out var contentObj) && contentObj is string content)
            {
                try
                {
                    var fullPath = Path.Combine("Assets", filePath);
                    if (File.Exists(fullPath))
                    {
                        var lines = new List<string>(File.ReadAllLines(fullPath));
                        var originalContent = string.Join("\n", lines.GetRange(startLine - 1, endLine - startLine + 1));

                        Undo.RegisterCompleteObjectUndo(AssetDatabase.LoadAssetAtPath<TextAsset>(fullPath), "Modify File");

                        lines.RemoveRange(startLine - 1, endLine - startLine + 1);
                        lines.InsertRange(startLine - 1, content.Split('\n'));

                        await File.WriteAllLinesAsync(fullPath, lines);

                        AssetDatabase.Refresh();
                        return $"Success: Modified file at '{fullPath}'.";
                    }

                    return $"Error: File not found at '{fullPath}'.";
                }
                catch (Exception e)
                {
                    return $"Error: {e.Message}";
                }
            }

            return "Error: Missing or invalid required parameters.";
        }
    }
}
