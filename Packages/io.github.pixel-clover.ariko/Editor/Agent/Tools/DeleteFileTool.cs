using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Ariko.Editor.Agent.Tools
{
    /// <summary>
    ///     A tool for deleting a file.
    /// </summary>
    public class DeleteFileTool : IArikoTool
    {
        /// <inheritdoc />
        public string Name => "DeleteFile";

        /// <inheritdoc />
        public string Description => "Deletes a file at the given path.";

        /// <inheritdoc />
        public Dictionary<string, string> Parameters => new()
        {
            { "filePath", "The path of the file to delete. Should be relative to the Assets folder." }
        };

        /// <inheritdoc />
        public Task<string> Execute(ToolExecutionContext context)
        {
            if (context.Arguments.TryGetValue("filePath", out var filePathObj) && filePathObj is string filePath)
                try
                {
                    var fullPath = Path.Combine(Application.dataPath, filePath.Replace("Assets/", ""));
                    if (!PathUtility.IsPathSafe(fullPath, out var safePath) ||
                        !safePath.StartsWith(Application.dataPath))
                        return Task.FromResult("Error: Path is outside the Assets folder.");
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        AssetDatabase.Refresh();
                        return Task.FromResult($"Success: Deleted file at '{fullPath}'.");
                    }

                    return Task.FromResult($"Error: File not found at '{fullPath}'.");
                }
                catch (Exception e)
                {
                    return Task.FromResult($"Error: {e.Message}");
                }

            return Task.FromResult("Error: Missing required 'filePath' parameter.");
        }
    }
}
