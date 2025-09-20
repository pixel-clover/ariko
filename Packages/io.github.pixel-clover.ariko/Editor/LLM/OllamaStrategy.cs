using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
///     Implements the <see cref="IApiProviderStrategy" /> for a local Ollama server.
/// </summary>
public class OllamaStrategy : IApiProviderStrategy
{
    private readonly StringBuilder streamBuffer = new();

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
    public string ParseChatResponseStream(string streamChunk)
    {
        if (string.IsNullOrEmpty(streamChunk)) return string.Empty;

        streamBuffer.Append(streamChunk);
        var content = streamBuffer.ToString();
        var result = new StringBuilder();

        var lastNewlineIndex = content.LastIndexOf('\n');

        // If there's no newline, the whole buffer is a single, potentially incomplete line.
        // We don't process it yet and wait for the next chunk, unless it's a complete JSON object on its own.
        if (lastNewlineIndex == -1)
            try
            {
                // Check if the buffer contains a single, complete JSON object.
                var node = JsonConvert.DeserializeObject<OllamaStreamChunk>(content.Trim());
                var delta = node.Message?.Content ?? "";
                streamBuffer.Clear(); // Parsed successfully, clear the buffer.
                return delta;
            }
            catch (JsonException)
            {
                // Incomplete JSON, wait for more data.
                return string.Empty;
            }

        // We have at least one newline, so we can process the content up to the last newline.
        var processable = content.Substring(0, lastNewlineIndex);
        var remaining = content.Substring(lastNewlineIndex + 1);

        // Process all the complete lines.
        var lines = processable.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
            try
            {
                var node = JsonConvert.DeserializeObject<OllamaStreamChunk>(line.Trim());
                result.Append(node.Message?.Content ?? "");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Ariko] Error parsing Ollama stream chunk: {e.Message}");
            }

        // The 'remaining' part is the new buffer content. It might be an incomplete line or empty.
        streamBuffer.Clear();
        streamBuffer.Append(remaining);

        // Trim runaway buffer
        if (streamBuffer.Length > 1024 * 1024) streamBuffer.Clear();

        return result.ToString();
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

    private struct OllamaStreamChunk
    {
        [JsonProperty("message")] public MessagePayload Message { get; set; }
        [JsonProperty("done")] public bool Done { get; set; }
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
