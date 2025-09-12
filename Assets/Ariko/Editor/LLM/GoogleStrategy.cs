// csharp
// File: Assets/Ariko/Editor/LLM/GoogleStrategy.cs

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GoogleStrategy : IApiProviderStrategy
{
    public string GetModelsUrl(ArikoSettings settings)
    {
        return $"https://generativelanguage.googleapis.com/v1beta/models?key={settings.google_ApiKey}";
    }

    public string GetChatUrl(string modelName, ArikoSettings settings)
    {
        return
            $"https://generativelanguage.googleapis.com/v1beta/{modelName}:generateContent?key={settings.google_ApiKey}";
    }

    public string GetAuthHeader(ArikoSettings settings)
    {
        return null;
    }

    public string BuildChatRequestBody(string prompt, string modelName)
    {
        var payload = new GooglePayload
        {
            contents = new[] { new Content { parts = new[] { new Part { text = prompt } } } }
        };
        return JsonUtility.ToJson(payload);
    }

    public string ParseChatResponse(string json)
    {
        return JsonUtility.FromJson<GoogleResponse>(json).candidates[0].content.parts[0].text;
    }

    public List<string> ParseModelsResponse(string json)
    {
        var allAvailableModels = JsonUtility.FromJson<GoogleModelsResponse>(json).models
            .Select(m => m.name)
            .ToList();

        var filteredModels = allAvailableModels
            .Where(apiModel => KnownModels.Google.Contains(apiModel))
            .ToList();

        return filteredModels;
    }

    [Serializable]
    private class GooglePayload
    {
        public Content[] contents;
    }

    [Serializable]
    private class Content
    {
        public Part[] parts;
    }

    [Serializable]
    private class Part
    {
        public string text;
    }

    [Serializable]
    private struct GoogleResponse
    {
        public Candidate[] candidates;
    }

    [Serializable]
    private struct Candidate
    {
        public Content content;
    }

    [Serializable]
    private struct GoogleModelsResponse
    {
        public GoogleModel[] models;
    }

    [Serializable]
    private struct GoogleModel
    {
        public string name;
    }
}
