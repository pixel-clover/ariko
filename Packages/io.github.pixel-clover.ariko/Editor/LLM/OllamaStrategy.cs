using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

/// <summary>
///     Implements the <see cref="IApiProviderStrategy" /> for a local Ollama server.
/// </summary>
public class OllamaStrategy : IApiProviderStrategy
{
    /// <inheritdoc />
    public WebRequestResult<string> GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        if (string.IsNullOrEmpty(settings.ollama_Url))
            return WebRequestResult<string>.Fail("Ollama URL is not configured in settings.", ErrorType.Auth);
        return WebRequestResult<string>.Success($"{settings.ollama_Url}/api/tags");
    }

    /// <inheritdoc />
    public WebRequestResult<string> GetChatUrl(string modelName, ArikoSettings settings,
        Dictionary<string, string> apiKeys)
    {
        if (string.IsNullOrEmpty(settings.ollama_Url))
            return WebRequestResult<string>.Fail("Ollama URL is not configured in settings.", ErrorType.Auth);
        return WebRequestResult<string>.Success($"{settings.ollama_Url}/api/chat");
    }

    /// <inheritdoc />
    public WebRequestResult<string> GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        return WebRequestResult<string>.Success(null);
    }

    /// <inheritdoc />
    public string BuildChatRequestBody(List<ChatMessage> messages, string modelName)
    {
        var payload = new OllamaPayload
        {
            Model = modelName,
            Messages = messages.Select(m =>
            {
                var role = "user"; // Default to "user"
                if (m.Role.Equals("Ariko", StringComparison.OrdinalIgnoreCase) ||
                    m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                    role = "assistant";
                else if (m.Role.Equals("System", StringComparison.OrdinalIgnoreCase)) role = "system";

                return new MessagePayload { Role = role, Content = m.Content };
            }).ToArray(),
            Stream = false
        };
        return JsonConvert.SerializeObject(payload);
    }

    /// <inheritdoc />
    public string ParseChatResponse(string json)
    {
        var response = JsonConvert.DeserializeObject<OllamaResponse>(json);
        return response.Message.Content;
    }

    /// <inheritdoc />
    public List<string> ParseModelsResponse(string json)
    {
        var response = JsonConvert.DeserializeObject<OllamaModelsResponse>(json);
        return response.Models.Select(m => m.Name).ToList();
    }

    private class MessagePayload
    {
        [JsonProperty("role")] public string Role { get; set; }

        [JsonProperty("content")] public string Content { get; set; }
    }

    private class OllamaPayload
    {
        [JsonProperty("model")] public string Model { get; set; }

        [JsonProperty("messages")] public MessagePayload[] Messages { get; set; }

        [JsonProperty("stream")] public bool Stream { get; set; }
    }

    private struct OllamaResponse
    {
        [JsonProperty("message")] public MessagePayload Message { get; set; }
    }

    private struct OllamaModelsResponse
    {
        [JsonProperty("models")] public List<OllamaModel> Models { get; set; }
    }

    private struct OllamaModel
    {
        [JsonProperty("name")] public string Name { get; set; }
    }
}
