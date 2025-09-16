using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
///     Implements the <see cref="IApiProviderStrategy" /> for the OpenAI API.
/// </summary>
public class OpenAiStrategy : IApiProviderStrategy
{
    /// <inheritdoc />
    public WebRequestResult<string> GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        return WebRequestResult<string>.Success("https://api.openai.com/v1/models");
    }

    /// <inheritdoc />
    public WebRequestResult<string> GetChatUrl(string modelName, ArikoSettings settings,
        Dictionary<string, string> apiKeys)
    {
        return WebRequestResult<string>.Success("https://api.openai.com/v1/chat/completions");
    }

    /// <inheritdoc />
    public WebRequestResult<string> GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        if (!apiKeys.TryGetValue("OpenAI", out var key) || string.IsNullOrEmpty(key))
            return WebRequestResult<string>.Fail("OpenAI API key is missing.", ErrorType.Auth);

        return WebRequestResult<string>.Success($"Bearer {key}");
    }

    /// <inheritdoc />
    public string BuildChatRequestBody(string prompt, string modelName)
    {
        var payload = new OpenAIPayload
        {
            Model = modelName,
            Messages = new[] { new MessagePayload { Role = "user", Content = prompt } }
        };
        return JsonConvert.SerializeObject(payload);
    }

    /// <inheritdoc />
    public string ParseChatResponse(string json)
    {
        var response = JsonConvert.DeserializeObject<OpenAIResponse>(json);
        return response.Choices?.FirstOrDefault()?.Message?.Content ?? "No content found in response.";
    }

    /// <inheritdoc />
    public List<string> ParseModelsResponse(string json)
    {
        var allAvailableModels = JsonConvert.DeserializeObject<OpenAIModelsResponse>(json).Data
            .Select(m => m.Id)
            .ToList();

        var filteredModels = allAvailableModels
            .Where(apiModel => KnownModels.OpenAI.Contains(apiModel))
            .ToList();

        return filteredModels;
    }

    private class MessagePayload
    {
        [JsonProperty("role")] public string Role { get; set; }

        [JsonProperty("content")] public string Content { get; set; }
    }

    private class OpenAIPayload
    {
        [JsonProperty("model")] public string Model { get; set; }

        [JsonProperty("messages")] public MessagePayload[] Messages { get; set; }
    }

    private class OpenAIResponse
    {
        [JsonProperty("choices")] public Choice[] Choices { get; set; }
    }

    private class Choice
    {
        [JsonProperty("message")] public Message Message { get; set; }
    }

    private class Message
    {
        [JsonProperty("content")] public string Content { get; set; }
    }

    private class OpenAIModelsResponse
    {
        [JsonProperty("data")] public OpenAIModel[] Data { get; set; }
    }

    private class OpenAIModel
    {
        [JsonProperty("id")] public string Id { get; set; }
    }
}
