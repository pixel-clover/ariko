// Packages/io.github.pixel-clover.ariko/Editor/ChatHistoryStorage.cs
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class ChatHistoryStorage
{
    private static readonly string FilePath = Path.Combine(Application.dataPath, "..", "UserSettings", "ArikoChatHistory.json");

    public static void SaveHistory(List<ChatSession> history)
    {
        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonConvert.SerializeObject(history, Formatting.Indented);
            File.WriteAllText(FilePath, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Ariko: Failed to save chat history. Error: {ex.Message}");
        }
    }

    public static List<ChatSession> LoadHistory()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var history = JsonConvert.DeserializeObject<List<ChatSession>>(json);
            return history;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Ariko: Failed to load chat history. Error: {ex.Message}");
            return null;
        }
    }
}
