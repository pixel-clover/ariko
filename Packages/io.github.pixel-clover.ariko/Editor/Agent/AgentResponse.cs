using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
///     Represents the response from an agent, which may include a thought process, a tool to use, and parameters for that
///     tool.
/// </summary>
public class AgentResponse
{
    /// <summary>
    ///     Gets or sets the thought process of the agent.
    /// </summary>
    [JsonProperty("thought")]
    public string Thought { get; set; }

    /// <summary>
    ///     Gets or sets the name of the tool to be used.
    /// </summary>
    [JsonProperty("tool_name")]
    public string ToolName { get; set; }

    /// <summary>
    ///     Gets or sets the parameters for the tool.
    /// </summary>
    [JsonProperty("parameters")]
    public Dictionary<string, object> Parameters { get; set; }
}
