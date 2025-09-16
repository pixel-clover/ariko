using System.Collections.Generic;
using System.Linq;
using System.Text;

public class ToolRegistry
{
    private readonly Dictionary<string, IArikoTool> tools = new();

    public ToolRegistry()
    {
        // Discover and register all tools. You can use reflection or manual registration.
        RegisterTool(new CreateGameObjectTool());
        // Register other tools here...
    }

    public void RegisterTool(IArikoTool tool)
    {
        tools[tool.Name] = tool;
    }

    public IArikoTool GetTool(string name)
    {
        tools.TryGetValue(name, out var tool);
        return tool;
    }

    // This generates the text that describes the tools to the LLM.
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
