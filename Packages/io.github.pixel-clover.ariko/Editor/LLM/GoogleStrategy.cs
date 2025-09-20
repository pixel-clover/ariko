// C#
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
///     Implements the <see cref="IApiProviderStrategy" /> for the Google AI (Gemini) API.
/// </summary>
public class GoogleStrategy : IApiProviderStrategy
{
    private readonly StringBuilder streamBuffer = new StringBuilder();

    /// <inheritdoc />
    public WebRequestResult<string> GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        if (!apiKeys.TryGetValue("Google", out var key) || string.IsNullOrEmpty(key))
            return WebRequestResult<string>.Fail("Google API key is missing.", ErrorType.Auth);

        return WebRequestResult<string>.Success(
            $"https://generativelanguage.googleapis.com/v1beta/models?key={key}");
    }

    /// <inheritdoc />
    public WebRequestResult<string> GetChatUrl(string modelName, ArikoSettings settings,
        Dictionary<string, string> apiKeys)
    {
        if (!apiKeys.TryGetValue("Google", out var key) || string.IsNullOrEmpty(key))
            return WebRequestResult<string>.Fail("Google API key is missing.", ErrorType.Auth);

        return WebRequestResult<string>.Success(
            $"https://generativelanguage.googleapis.com/v1beta/{modelName}:generateContent?key={key}");
    }

    /// <inheritdoc />
    public WebRequestResult<string> GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        // Google API uses API key in URL, not in header
        return WebRequestResult<string>.Success(null);
    }

    /// <inheritdoc />
    public string BuildChatRequestBody(List<ChatMessage> messages, string modelName)
    {
        var contents = messages.Select(m =>
        {
            // Google uses "model" for the assistant's role and does not have a "system" role.
            // The first user message is treated as system instructions.
            var role = m.Role.Equals("Ariko", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
            return new Content { Role = role, Parts = new[] { new Part { Text = m.Content } } };
        }).ToArray();

        var payload = new GooglePayload { Contents = contents };
        return JsonConvert.SerializeObject(payload);
    }

    /// <inheritdoc />
    public string ParseChatResponse(string json)
    {
        // Handle cases where the response might not have candidates.
        var response = JsonConvert.DeserializeObject<GoogleResponse>(json);
        return response.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ??
               "No content found in response.";
    }

    /// <inheritdoc />
    public string ParseChatResponseStream(string streamChunk)
    {
        if (string.IsNullOrEmpty(streamChunk)) return string.Empty;

        try
        {
            streamBuffer.Append(streamChunk);
            var bufferStr = streamBuffer.ToString();
            var firstOpen = bufferStr.IndexOf('{');
            if (firstOpen == -1)
            {
                // No JSON start yet; prevent runaway buffer
                if (streamBuffer.Length > 1024 * 64) streamBuffer.Clear();
                return string.Empty;
            }

            // Iterate over candidate closing brace positions and attempt parse
            for (int end = bufferStr.IndexOf('}', firstOpen); end >= 0; end = bufferStr.IndexOf('}', end + 1))
            {
                var candidate = bufferStr.Substring(firstOpen, end - firstOpen + 1);
                try
                {
                    var parsed = ParseChatResponse(candidate);
                    // Remove processed portion from buffer
                    streamBuffer.Remove(0, end + 1);
                    return parsed;
                }
                catch (JsonException)
                {
                    // Candidate still incomplete or invalid JSON; try next closing brace
                }
            }

            // No complete JSON parsed yet. Trim if buffer grows too large.
            if (streamBuffer.Length > 1024 * 1024)
                streamBuffer.Clear();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Ariko] Error parsing Google stream chunk: {e.Message}");
        }

        return string.Empty;
    }

    /// <inheritdoc />
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
        [JsonProperty("role")] public string Role { get; set; }
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
