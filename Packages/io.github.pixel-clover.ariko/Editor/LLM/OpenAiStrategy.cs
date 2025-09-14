// csharp
// File: Assets/Ariko/Editor/LLM/OpenAiStrategy.cs

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OpenAiStrategy : IApiProviderStrategy
{
    public string GetModelsUrl(ArikoSettings settings)
    {
        return "https://api.openai.com/v1/models";
    }

    public string GetChatUrl(string modelName, ArikoSettings settings)
    {
        return "https://api.openai.com/v1/chat/completions";
    }

    public string GetAuthHeader(ArikoSettings settings)
    {
        return $"Bearer {settings.openAI_ApiKey}";
    }

    public string BuildChatRequestBody(string prompt, string modelName)
    {
        var payload = new OpenAIPayload
        {
            model = modelName,
            messages = new[] { new MessagePayload { role = "user", content = prompt } }
        };
        return JsonUtility.ToJson(payload);
    }

    public string ParseChatResponse(string json)
    {
        return JsonUtility.FromJson<OpenAIResponse>(json).choices[0].message.content;
    }

    public List<string> ParseModelsResponse(string json)
    {
        var allAvailableModels = JsonUtility.FromJson<OpenAIModelsResponse>(json).data
            .Select(m => m.id)
            .ToList();

        var filteredModels = allAvailableModels
            .Where(apiModel => KnownModels.OpenAI.Contains(apiModel))
            .ToList();

        return filteredModels;
    }

    [Serializable]
    private class MessagePayload
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OpenAIPayload
    {
        public string model;
        public MessagePayload[] messages;
    }

    [Serializable]
    private struct OpenAIResponse
    {
        public Choice[] choices;
    }

    [Serializable]
    private struct Choice
    {
        public Message message;
    }

    [Serializable]
    private struct Message
    {
        public string content;
    }

    [Serializable]
    private struct OpenAIModelsResponse
    {
        public OpenAIModel[] data;
    }

    [Serializable]
    private struct OpenAIModel
    {
        public string id;
    }
}
