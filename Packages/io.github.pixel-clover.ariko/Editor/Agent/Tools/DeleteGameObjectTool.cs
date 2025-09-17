using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Ariko.Editor.Agent.Tools
{
    /// <summary>
    ///     A tool for deleting a GameObject.
    /// </summary>
    public class DeleteGameObjectTool : IArikoTool
    {
        /// <inheritdoc />
        public string Name => "DeleteGameObject";

        /// <inheritdoc />
        public string Description => "Deletes a GameObject with the given name.";

        /// <inheritdoc />
        public Dictionary<string, string> Parameters => new()
        {
            { "name", "The name of the GameObject to delete." }
        };

        /// <inheritdoc />
        public Task<string> Execute(ToolExecutionContext context)
        {
            if (context.Arguments.TryGetValue("name", out var nameObj) && nameObj is string name)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                    return Task.FromResult($"Success: Deleted GameObject named '{name}'.");
                }

                return Task.FromResult($"Error: GameObject named '{name}' not found.");
            }

            return Task.FromResult("Error: Missing required 'name' parameter.");
        }
    }
}
