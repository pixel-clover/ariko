using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Ariko.Editor.Agent.Tools
{
    public class ReadFileTool : IArikoTool
    {
        public string Name => "read_file";
        public string Description => "Reads the entire content of a file at the specified path.";

        public Dictionary<string, string> Parameters => new()
        {
            { "path", "The relative path to the file to be read." }
        };

        public Task<string> Execute(ToolExecutionContext context)
        {
            if (!context.Arguments.TryGetValue("path", out var pathValue) || pathValue is not string path)
            {
                return Task.FromResult("Error: 'path' parameter is missing or not a string.");
            }

            try
            {
                var content = File.ReadAllText(path);
                return Task.FromResult($"File '{path}' read successfully. Content:\n```\n{content}\n```");
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult($"Error: The directory for path '{path}' was not found.");
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult($"Error: The file at path '{path}' was not found.");
            }
            catch (System.Exception e)
            {
                return Task.FromResult($"An unexpected error occurred: {e.Message}");
            }
        }
    }
}
