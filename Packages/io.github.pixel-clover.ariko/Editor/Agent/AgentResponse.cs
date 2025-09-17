using System.Collections.Generic;
using Newtonsoft.Json;

public class AgentResponse
{
    [JsonProperty("thought")]
    public string Thought { get; set; }

    [JsonProperty("tool_name")]
    public string ToolName { get; set; }

    [JsonProperty("parameters")]
    public Dictionary<string, object> Parameters { get; set; }
}
