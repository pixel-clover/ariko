using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ariko.Editor.Agent.Tools;

/// <summary>
///     Manages the collection of available tools for the Ariko agent.
/// </summary>
public class ToolRegistry
{
    private readonly Dictionary<string, IArikoTool> tools = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ToolRegistry" /> class
    ///     and registers tools based on the current work mode and settings.
    /// </summary>
    public ToolRegistry(ArikoSettings settings, string workMode)
    {
        // Agentic tools are only available in "Agent" mode.
        if (workMode == "Agent")
        {
            RegisterTool(new CreateGameObjectTool());
            RegisterTool(new CreateFileTool());
            RegisterTool(new ModifyFileTool());
            RegisterTool(new ReadFileTool());

            // Destructive tools are further gated by a user setting.
            if (settings.enableDeleteTools) RegisterTool(new DeleteFileTool());
        }
        // In "Ask" mode, no tools are registered.
    }

    /// <summary>
    ///     Registers a tool with the registry.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    public void RegisterTool(IArikoTool tool)
    {
        tools[tool.Name] = tool;
    }

    /// <summary>
    ///     Retrieves a tool from the registry by its name.
    /// </summary>
    /// <param name="name">The name of the tool to retrieve.</param>
    /// <returns>The tool instance, or null if not found.</returns>
    public IArikoTool GetTool(string name)
    {
        tools.TryGetValue(name, out var tool);
        return tool;
    }

    /// <summary>
    ///     Generates a string that describes all registered tools,
    ///     formatted for inclusion in a prompt for the LLM.
    /// </summary>
    /// <returns>A string containing the definitions of all tools.</returns>
    public string GetToolDefinitionsForPrompt()
    {
        var builder = new StringBuilder();
        builder.AppendLine("You have access to the following tools. Use them to fulfill the user's request.");
        foreach (var tool in tools.Values)
        {
            builder.AppendLine($"Tool: {tool.Name}");
            builder.AppendLine($"Description: {tool.Description}");
            var paramString = string.Join(", ", tool.Parameters.Select(p => $"{p.Key} ({p.Value})"));
            builder.AppendLine($"Parameters: {paramString}");
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
