using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

public class GoogleStrategy : IApiProviderStrategy
{
    public string GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        apiKeys.TryGetValue("Google", out var key);
        return $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";
    }

    public string GetChatUrl(string modelName, ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        apiKeys.TryGetValue("Google", out var key);
        return
            $"https://generativelanguage.googleapis.com/v1beta/{modelName}:generateContent?key={key}";
    }

    public string GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        return null;
    }

    public string BuildChatRequestBody(string prompt, string modelName)
    {
        var payload = new GooglePayload
        {
            Contents = new[] { new Content { Parts = new[] { new Part { Text = prompt } } } }
        };
        return JsonConvert.SerializeObject(payload);
    }

    public string ParseChatResponse(string json)
    {
        // Handle cases where the response might not have candidates.
        var response = JsonConvert.DeserializeObject<GoogleResponse>(json);
        return response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ??
               "Error: No content found in response.";
    }

    public List<string> ParseModelsResponse(string json)
    {
        var allAvailableModels = JsonConvert.DeserializeObject<GoogleModelsResponse>(json).Models
            .Select(m => m.Name)
            .ToList();

        var filteredModels = allAvailableModels
            .Where(apiModel => KnownModels.Google.Contains(apiModel))
            .ToList();

        return filteredModels;
    }

    private class GooglePayload
    {
        [JsonProperty("contents")] public Content[] Contents { get; set; }
    }

    private class Content
    {
        [JsonProperty("parts")] public Part[] Parts { get; set; }
    }

    private class Part
    {
        [JsonProperty("text")] public string Text { get; set; }
    }

    private class GoogleResponse
    {
        [JsonProperty("candidates")] public Candidate[] Candidates { get; set; }
    }

    private class Candidate
    {
        [JsonProperty("content")] public Content Content { get; set; }
    }

    private class GoogleModelsResponse
    {
        [JsonProperty("models")] public GoogleModel[] Models { get; set; }
    }

    private class GoogleModel
    {
        [JsonProperty("name")] public string Name { get; set; }
    }
}
