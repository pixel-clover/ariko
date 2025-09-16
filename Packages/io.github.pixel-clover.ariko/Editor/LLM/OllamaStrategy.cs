using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

public class OllamaStrategy : IApiProviderStrategy
{
    public string GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        return $"{settings.ollama_Url}/api/tags";
    }

    public string GetChatUrl(string modelName, ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        return $"{settings.ollama_Url}/api/chat";
    }

    public string GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        return null;
    }

    public string BuildChatRequestBody(string prompt, string modelName)
    {
        var payload = new OllamaPayload
        {
            Model = modelName,
            Messages = new[] { new MessagePayload { Role = "user", Content = prompt } },
            Stream = false
        };
        return JsonConvert.SerializeObject(payload);
    }

    public string ParseChatResponse(string json)
    {
        var response = JsonConvert.DeserializeObject<OllamaResponse>(json);
        return response.Message.Content;
    }

    public List<string> ParseModelsResponse(string json)
    {
        var response = JsonConvert.DeserializeObject<OllamaModelsResponse>(json);
        return response.Models.Select(m => m.Name).ToList();
    }

    private class MessagePayload
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    private class OllamaPayload
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public MessagePayload[] Messages { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; }
    }

    private struct OllamaResponse
    {
        [JsonProperty("message")]
        public MessagePayload Message { get; set; }
    }

    private struct OllamaModelsResponse
    {
        [JsonProperty("models")]
        public List<OllamaModel> Models { get; set; }
    }

    private struct OllamaModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
