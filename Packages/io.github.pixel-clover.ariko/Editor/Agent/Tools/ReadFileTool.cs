using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Ariko.Editor.Agent.Tools
{
    /// <summary>
    /// A tool for reading the entire content of a file at a specified path.
    /// </summary>
    public class ReadFileTool : IArikoTool
    {
        /// <inheritdoc />
        public string Name => "read_file";

        /// <inheritdoc />
        public string Description => "Reads the entire content of a file at the specified path.";

        /// <inheritdoc />
        public Dictionary<string, string> Parameters => new()
        {
            { "path", "The relative path to the file to be read." }
        };

        /// <inheritdoc />
        public Task<string> Execute(ToolExecutionContext context)
        {
            if (!context.Arguments.TryGetValue("path", out var pathValue) || pathValue is not string path)
                return Task.FromResult("Error: 'path' parameter is missing or not a string.");

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
            catch (Exception e)
            {
                return Task.FromResult($"An unexpected error occurred: {e.Message}");
            }
        }
    }
}
