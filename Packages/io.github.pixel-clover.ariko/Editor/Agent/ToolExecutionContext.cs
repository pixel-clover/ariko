using System.Collections.Generic;

/// <summary>
///     Provides the context for a tool's execution.
/// </summary>
public class ToolExecutionContext
{
    /// <summary>
    ///     Gets the arguments provided by the LLM for the tool.
    /// </summary>
    public Dictionary<string, object> Arguments { get; set; }

    /// <summary>
    ///     Gets the AI provider being used for the current request.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    ///     Gets the AI model being used for the current request.
    /// </summary>
    public string Model { get; set; }

    /// <summary>
    ///     Gets the Ariko settings.
    /// </summary>
    public ArikoSettings Settings { get; set; }

    /// <summary>
    ///     Gets the dictionary of API keys.
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; }
}
