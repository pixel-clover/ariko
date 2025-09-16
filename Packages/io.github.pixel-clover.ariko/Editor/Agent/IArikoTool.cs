using System.Collections.Generic;

public interface IArikoTool
{
    string Name { get; }

    string Description { get; }

    // Define the parameters the tool accepts
    Dictionary<string, string> Parameters { get; }

    // The method that performs the action
    string Execute(Dictionary<string, object> arguments);
}
