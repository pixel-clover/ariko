using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class CreateGameObjectTool : IArikoTool
{
    public string Name => "CreateGameObject";
    public string Description => "Creates a new empty GameObject in the current scene.";

    public Dictionary<string, string> Parameters => new()
    {
        { "name", "The desired name for the new GameObject." }
    };

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
