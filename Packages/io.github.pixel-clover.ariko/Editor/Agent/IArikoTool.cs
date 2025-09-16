using System.Collections.Generic;

/// <summary>
///     Defines the interface for a tool that can be executed by the Ariko agent.
/// </summary>
public interface IArikoTool
{
    /// <summary>
    ///     Gets the name of the tool. This should be a unique identifier.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets a description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    ///     Gets a dictionary defining the parameters the tool accepts.
    ///     The key is the parameter name and the value is a description of the parameter.
    /// </summary>
    Dictionary<string, string> Parameters { get; }

    /// <summary>
    ///     Executes the tool's action with the given arguments.
    /// </summary>
    /// <param name="arguments">A dictionary of arguments for the tool, where the key is the parameter name.</param>
    /// <returns>A string result of the execution, to be sent back to the LLM as an observation.</returns>
    string Execute(Dictionary<string, object> arguments);
}
