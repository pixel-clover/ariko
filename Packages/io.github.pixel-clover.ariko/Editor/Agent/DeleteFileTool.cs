using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;

public class DeleteFileTool : IArikoTool
{
    public string Name => "delete_file";
    public string Description => "Deletes a file from the project. The path must be a valid path within the project's Assets folder.";
    public Dictionary<string, string> Parameters => new()
    {
        { "path", "The project-relative path to the file to delete (e.g., 'Assets/Scripts/MyOldScript.cs')." }
    };

    public Task<string> Execute(ToolExecutionContext context)
    {
        if (!context.Arguments.TryGetValue("path", out var pathObj) || pathObj is not string path)
        {
            return Task.FromResult("Error: 'path' parameter is missing or not a string.");
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult("Error: 'path' parameter cannot be empty.");
        }

        if (!path.StartsWith("Assets/"))
        {
            return Task.FromResult("Error: For safety, path must be within the 'Assets' folder.");
        }

        if (!File.Exists(path))
        {
            return Task.FromResult($"Error: File not found at path: {path}");
        }

        if (AssetDatabase.DeleteAsset(path))
        {
            return Task.FromResult($"Successfully deleted file at path: {path}");
        }
        else
        {
            return Task.FromResult($"Error: Failed to delete file at path: {path}. It might be locked or no longer exist.");
        }
    }
}
