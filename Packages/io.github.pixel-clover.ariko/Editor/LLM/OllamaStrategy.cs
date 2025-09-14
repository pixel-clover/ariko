using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OllamaStrategy : IApiProviderStrategy
{
    public string GetModelsUrl(ArikoSettings settings)
    {
        return $"{settings.ollama_Url}/api/tags";
    }

    public string GetChatUrl(string modelName, ArikoSettings settings)
    {
        return $"{settings.ollama_Url}/api/chat";
    }

    public string GetAuthHeader(ArikoSettings settings)
    {
        return null;
    }

    public string BuildChatRequestBody(string prompt, string modelName)
    {
        var payload = new OllamaPayload
        {
            model = modelName,
            messages = new[] { new MessagePayload { role = "user", content = prompt } }
        };
        return JsonUtility.ToJson(payload);
    }

    public string ParseChatResponse(string json)
    {
        return JsonUtility.FromJson<OllamaResponse>(json).message.content;
    }

    public List<string> ParseModelsResponse(string json)
    {
        return JsonUtility.FromJson<OllamaModelsResponse>(json).models.Select(m => m.name).ToList();
    }

    [Serializable]
    private class MessagePayload
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class OllamaPayload
    {
        public string model;
        public MessagePayload[] messages;
        public bool stream;
    }

    [Serializable]
    private struct OllamaResponse
    {
        public MessagePayload message;
    }

    [Serializable]
    private struct OllamaModelsResponse
    {
        public OllamaModel[] models;
    }

    [Serializable]
    private struct OllamaModel
    {
        public string name;
    }
}
