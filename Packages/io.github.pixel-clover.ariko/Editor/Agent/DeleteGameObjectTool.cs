using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class DeleteGameObjectTool : IArikoTool
{
    public string Name => "delete_game_object";
    public string Description => "Deletes a GameObject from the current scene by its name. This action cannot be undone.";
    public Dictionary<string, string> Parameters => new()
    {
        { "name", "The name of the GameObject to delete." }
    };

    public Task<string> Execute(ToolExecutionContext context)
    {
        if (!context.Arguments.TryGetValue("name", out var nameObj) || nameObj is not string name)
        {
            return Task.FromResult("Error: 'name' parameter is missing or not a string.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult("Error: 'name' parameter cannot be empty.");
        }

        // Note: GameObject.Find can be unreliable if multiple objects share the same name.
        // It will only find the first active GameObject with that name.
        var gameObject = GameObject.Find(name);
        if (gameObject == null)
        {
            return Task.FromResult($"Error: GameObject with name '{name}' not found in the current scene.");
        }

        Object.DestroyImmediate(gameObject);
        return Task.FromResult($"Successfully deleted GameObject: {name}");
    }
}
