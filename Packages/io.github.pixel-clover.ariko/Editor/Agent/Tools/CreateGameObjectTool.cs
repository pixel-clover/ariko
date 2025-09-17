using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Ariko.Editor.Agent.Tools
{
    /// <summary>
    ///     A tool for creating a new GameObject.
    /// </summary>
    public class CreateGameObjectTool : IArikoTool
    {
        /// <inheritdoc />
        public string Name => "CreateGameObject";

        /// <inheritdoc />
        public string Description => "Creates a new GameObject with the given name.";

        /// <inheritdoc />
        public Dictionary<string, string> Parameters => new()
        {
            { "name", "The name of the GameObject to create." }
        };

        /// <inheritdoc />
        public Task<string> Execute(ToolExecutionContext context)
        {
            if (context.Arguments.TryGetValue("name", out var nameObj) && nameObj is string name)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
                // Note: GameObject.Find is not reliable for newly created objects.
                // Consider returning the instance ID or a different way to identify the object.
                return Task.FromResult($"Success: Created new GameObject named '{name}'.");
            }

            return Task.FromResult("Error: Missing required 'name' parameter.");
        }
    }
}
