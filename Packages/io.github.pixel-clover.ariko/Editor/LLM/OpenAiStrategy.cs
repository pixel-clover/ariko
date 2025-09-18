using System;
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
    public string BuildChatRequestBody(List<ChatMessage> messages, string modelName)
    {
        var payload = new OpenAIPayload
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
            }).ToArray()
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
    public string ParseChatResponseStream(string streamChunk)
    {
        // OpenAI streaming typically sends Server-Sent Events with lines starting with "data: {json}"
        if (string.IsNullOrEmpty(streamChunk)) return string.Empty;
        var result = string.Empty;
        var lines = streamChunk.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (!line.StartsWith("data:")) continue;
            var payload = line.Substring("data:".Length).Trim();
            if (payload == "[DONE]") continue;
            try
            {
                var node = JsonConvert.DeserializeObject<OpenAIStreamChunk>(payload);
                var delta = node?.Choices != null && node.Choices.Length > 0 ? node.Choices[0]?.Delta?.Content : null;
                if (!string.IsNullOrEmpty(delta)) result += delta;
            }
            catch
            {
                // Ignore partial/broken JSON fragments
            }
        }
        return result;
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

        [JsonProperty("stream", NullValueHandling = NullValueHandling.Ignore)] public bool? Stream { get; set; }
    }

    private class OpenAIResponse
    {
        [JsonProperty("choices")] public Choice[] Choices { get; set; }
    }

    private class Choice
    {
        [JsonProperty("message")] public Message Message { get; set; }
        [JsonProperty("delta")] public Delta Delta { get; set; }
    }

    private class Message
    {
        [JsonProperty("content")] public string Content { get; set; }
    }

    private class Delta
    {
        [JsonProperty("content")] public string Content { get; set; }
    }

    private class OpenAIStreamChunk
    {
        [JsonProperty("choices")] public Choice[] Choices { get; set; }
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
