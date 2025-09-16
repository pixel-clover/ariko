using System.Collections.Generic;
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
    public string Execute(Dictionary<string, object> arguments)
    {
        if (arguments.TryGetValue("name", out var nameObj) && nameObj is string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create GameObject via Ariko");
            Selection.activeGameObject = go;
            return $"Success: Created new GameObject named '{name}'.";
        }

        return "Error: Missing required 'name' parameter.";
    }
}
