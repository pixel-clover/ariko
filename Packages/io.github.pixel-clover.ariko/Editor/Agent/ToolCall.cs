using System;
using System.Collections.Generic;

/// <summary>
///     Represents a request from the AI to call a specific tool with a set of parameters.
/// </summary>
[Serializable]
public class ToolCall
{
    /// <summary>
    ///     The AI's reasoning for why it is choosing this tool and parameters.
    /// </summary>
    public string thought;

    /// <summary>
    ///     The name of the tool to be executed.
    /// </summary>
    public string tool_name;

    /// <summary>
    ///     A dictionary of parameters to pass to the tool's Execute method.
    /// </summary>
    public Dictionary<string, object> parameters;
}
