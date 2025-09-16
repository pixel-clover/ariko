using UnityEditor;
using UnityEngine;

/// <summary>
///     Adds a context menu item to Unity components to ask Ariko for an explanation.
/// </summary>
public static class ComponentContextMenu
{
    [MenuItem("CONTEXT/Component/Ask Ariko to Explain")]
    private static void ExplainComponentWithAriko(MenuCommand command)
    {
        var component = command.context as Component;
        if (component == null)
        {
            Debug.LogError("Ariko: Could not find Component from context.");
            return;
        }

        var componentType = component.GetType();
        var componentName = componentType.Name;

        // 1. Show the Ariko Window
        var arikoWindow = EditorWindow.GetWindow<ArikoWindow>("Ariko Assistant");

        // 2. Prepare the prompt and send it
        var prompt =
            $"Explain the Unity {componentName} component. Describe its key properties and provide a C# example for its usage.";

        // The window and its controller might not be initialized yet, so we wait for the next editor update.
        EditorApplication.delayCall += () =>
        {
            if (arikoWindow != null && arikoWindow.controller != null)
                arikoWindow.SendExternalMessage(prompt);
            else
                Debug.LogError("Ariko: The Ariko window is not available to process the request.");
        };
    }
}
