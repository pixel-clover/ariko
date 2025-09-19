using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
///     Handles saving and loading the chat history to and from a JSON file in the UserSettings folder.
/// </summary>
public static class ChatHistoryStorage
{
    private static readonly string FilePath =
        Path.Combine(Application.dataPath, "..", "UserSettings", "ArikoChatHistory.json");

    /// <summary>
    ///     Saves the provided chat history to a JSON file.
    /// </summary>
    /// <param name="history">The list of chat sessions to save.</param>
    public static void SaveHistory(List<ChatSession> history)
    {
        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            var json = JsonConvert.SerializeObject(history, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ariko: Failed to save chat history. Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Loads the chat history from a JSON file.
    /// </summary>
    /// <returns>A list of chat sessions, or null if the file doesn't exist or an error occurs.</returns>
    public static List<ChatSession> LoadHistory()
    {
        if (!File.Exists(FilePath)) return null;

        try
        {
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return null;

            var history = JsonConvert.DeserializeObject<List<ChatSession>>(json);
            return history;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Ariko: Failed to load chat history. Error: {ex.Message}");
            return null;
        }
    }
}
