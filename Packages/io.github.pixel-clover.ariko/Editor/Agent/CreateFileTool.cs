using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;

/// <summary>
///     A tool for creating a new file.
/// </summary>
public class CreateFileTool : IArikoTool
{
    /// <inheritdoc />
    public string Name => "CreateFile";

    /// <inheritdoc />
    public string Description => "Creates a new file with the given content. Useful for creating new C# scripts.";

    /// <inheritdoc />
    public Dictionary<string, string> Parameters => new()
    {
        { "filePath", "The path of the file to create. Should be relative to the Assets folder." },
        { "content", "The content of the file to create." }
    };

    /// <inheritdoc />
    public Task<string> Execute(ToolExecutionContext context)
    {
        if (context.Arguments.TryGetValue("filePath", out var filePathObj) && filePathObj is string filePath &&
            context.Arguments.TryGetValue("content", out var contentObj) && contentObj is string content)
            try
            {
                var fullPath = Path.Combine("Assets", filePath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

                File.WriteAllText(fullPath, content);
                AssetDatabase.Refresh();
                return Task.FromResult($"Success: Created new file at '{fullPath}'.");
            }
            catch (Exception e)
            {
                return Task.FromResult($"Error: {e.Message}");
            }

        return Task.FromResult("Error: Missing required 'filePath' or 'content' parameter.");
    }
}
