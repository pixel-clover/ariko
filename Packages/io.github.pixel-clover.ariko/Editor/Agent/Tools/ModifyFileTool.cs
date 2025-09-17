using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Ariko.Editor.Agent.Tools
{
    public class ModifyFileTool : IArikoTool
    {
        public string Name => "ModifyFile";
        public string Description => "Modifies an existing file with a prompt.";

        public Dictionary<string, string> Parameters => new()
        {
            { "filePath", "The path of the file to modify, relative to the Assets folder." },
            { "prompt", "A prompt describing the modifications to make." }
        };

        public async Task<string> Execute(ToolExecutionContext context)
        {
            if (!context.Arguments.TryGetValue("filePath", out var filePathObj) || !(filePathObj is string filePath) ||
                !context.Arguments.TryGetValue("prompt", out var promptObj) || !(promptObj is string prompt))
                return "Error: Missing required parameters 'filePath' or 'prompt'.";

            var fullPath = Path.Combine(Application.dataPath, filePath.Replace("Assets/", ""));

            if (!PathUtility.IsPathSafe(fullPath, out var safePath) || !safePath.StartsWith(Application.dataPath))
            {
                return "Error: Path is outside the Assets folder.";
            }

            if (!File.Exists(fullPath)) return $"Error: File not found at '{fullPath}'.";

            var fileContent = await File.ReadAllTextAsync(fullPath);
            var llmService = new ArikoLLMService();

            var requestPrompt =
                $"Modify the following C# script file based on the prompt.\n\n[File Content]\n{fileContent}\n\n[Prompt]\n{prompt}\n\nOnly output the modified file content, nothing else.";

            var messages = new List<ChatMessage> { new ChatMessage { Role = "user", Content = requestPrompt } };

            var result = await llmService.SendChatRequest(
                messages,
                (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), context.Provider),
                context.Model,
                context.Settings,
                context.ApiKeys
            );

            if (result.IsSuccess)
            {
                var backupPath = fullPath + ".bak";
                try
                {
                    File.Move(fullPath, backupPath);
                    await File.WriteAllTextAsync(fullPath, result.Data);
                    File.Delete(backupPath);
                    AssetDatabase.Refresh();
                    return $"Success: Modified file at '{fullPath}'.";
                }
                catch (Exception e)
                {
                    if (File.Exists(backupPath))
                    {
                        File.Move(backupPath, fullPath);
                    }
                    return $"Error: Failed to modify file. Original file restored. Details: {e.Message}";
                }
            }

            return $"Error: {result.Error}";
        }
    }
}
