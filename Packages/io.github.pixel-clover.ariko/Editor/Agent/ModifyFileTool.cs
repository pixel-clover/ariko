using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

/// <summary>
///     A tool for modifying an existing file using an AI prompt.
/// </summary>
public class ModifyFileTool : IArikoTool
{
    /// <inheritdoc />
    public string Name => "ModifyFile";

    /// <inheritdoc />
    public string Description =>
        "Modifies an existing file based on a user prompt. Use this for adding methods, refactoring, or other code modifications.";

    /// <inheritdoc />
    public Dictionary<string, string> Parameters => new()
    {
        { "filePath", "The path of the file to modify. Should be relative to the Assets folder." },
        { "prompt", "The user's instruction on how to modify the file." }
    };

    /// <inheritdoc />
    public async Task<string> Execute(ToolExecutionContext context)
    {
        if (context.Arguments.TryGetValue("filePath", out var filePathObj) && filePathObj is string filePath &&
            context.Arguments.TryGetValue("prompt", out var promptObj) && promptObj is string userPrompt)
            try
            {
                var fullPath = Path.Combine(Application.dataPath, filePath);
                if (!File.Exists(fullPath)) return $"Error: File not found at '{fullPath}'.";

                var originalContent = File.ReadAllText(fullPath);

                var llmService = new ArikoLLMService();

                // Construct a more detailed prompt for the LLM
                var systemPrompt = "You are an expert C# programmer. The user wants to modify a C# file. " +
                                   "You will be given the full content of the file and the user's request. " +
                                   "You must return the *entire* modified file content. Do not add any extra text or explanations outside of the code. " +
                                   "Preserve the original formatting as much as possible.";

                var fullPrompt = $"{systemPrompt}\\n\\n" +
                                 $"File Path: {filePath}\\n\\n" +
                                 $"User Request: {userPrompt}\\n\\n" +
                                 $"Original File Content:\\n```csharp\\n{originalContent}\\n```";

                var result = await llmService.SendChatRequest(
                    fullPrompt,
                    (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), context.Provider),
                    context.Model,
                    context.Settings,
                    context.ApiKeys
                );

                if (result.IsSuccess)
                {
                    var modifiedContent = result.Data;
                    // The LLM might return the code wrapped in markdown, so we need to clean it up.
                    modifiedContent = modifiedContent.Trim();
                    if (modifiedContent.StartsWith("```csharp"))
                        modifiedContent = modifiedContent.Substring("```csharp".Length);
                    if (modifiedContent.EndsWith("```"))
                        modifiedContent = modifiedContent.Substring(0, modifiedContent.Length - "```".Length);
                    modifiedContent = modifiedContent.Trim();

                    File.WriteAllText(fullPath, modifiedContent);
                    AssetDatabase.Refresh();
                    return $"Success: Modified file at '{fullPath}'.";
                }

                return $"Error: LLM request failed: {result.Error}";
            }
            catch (Exception e)
            {
                return $"Error: {e.Message}";
            }

        return "Error: Missing required 'filePath' or 'prompt' parameter.";
    }
}
