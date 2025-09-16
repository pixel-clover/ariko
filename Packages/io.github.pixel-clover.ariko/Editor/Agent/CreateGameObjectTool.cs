using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
///     A tool for creating a new GameObject in the current scene.
/// </summary>
public class CreateGameObjectTool : IArikoTool
{
    /// <inheritdoc />
    public string Name => "CreateGameObject";

    /// <inheritdoc />
    public string Description => "Creates a new empty GameObject in the current scene.";

    /// <inheritdoc />
    public Dictionary<string, string> Parameters => new()
    {
        { "name", "The desired name for the new GameObject." }
    };

    /// <inheritdoc />
    public Task<string> Execute(ToolExecutionContext context)
    {
        if (context.Arguments.TryGetValue("name", out var nameObj) && nameObj is string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create GameObject via Ariko");
            Selection.activeGameObject = go;
            return Task.FromResult($"Success: Created new GameObject named '{name}'.");
        }

        return Task.FromResult("Error: Missing required 'name' parameter.");
    }
}
