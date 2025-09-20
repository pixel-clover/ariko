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

        try
        {
            streamBuffer.Append(streamChunk);
            var bufferStr = streamBuffer.ToString();

            var lines = bufferStr.Split(new[] { '\n' });
            var result = new StringBuilder();

            // Process all but the last line segment.
            for (var i = 0; i < lines.Length - 1; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                try
                {
                    var node = JsonConvert.DeserializeObject<OllamaStreamChunk>(line);
                    var delta = node.Message?.Content;
                    if (!string.IsNullOrEmpty(delta)) result.Append(delta);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Ariko] Error parsing Ollama stream chunk: {e.Message}");
                }
            }

            // Attempt to process the last line segment.
            var lastLine = lines.Length > 0 ? lines.Last().Trim() : string.Empty;
            if (!string.IsNullOrEmpty(lastLine))
            {
                try
                {
                    var node = JsonConvert.DeserializeObject<OllamaStreamChunk>(lastLine);
                    var delta = node.Message?.Content;
                    if (!string.IsNullOrEmpty(delta)) result.Append(delta);

                    // If we successfully parsed it, clear the buffer.
                    streamBuffer.Clear();
                }
                catch (JsonException)
                {
                    // It's an incomplete JSON object, so keep it in the buffer.
                    streamBuffer.Clear();
                    streamBuffer.Append(lastLine);
                }
            }
            else
            {
                // If the last line is empty, it means the buffer ended with a newline, so it's empty now.
                streamBuffer.Clear();
            }

            // Trim runaway buffer
            if (streamBuffer.Length > 1024 * 1024) streamBuffer.Clear();

            return result.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ariko] Error parsing Ollama stream chunk: {e.Message}");
            return string.Empty;
        }
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
