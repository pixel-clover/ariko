using System.Collections.Generic;

public interface IApiProviderStrategy
{
    WebRequestResult<string> GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys);
    WebRequestResult<string> GetChatUrl(string modelName, ArikoSettings settings, Dictionary<string, string> apiKeys);
    WebRequestResult<string> GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys);
    string BuildChatRequestBody(string prompt, string modelName);
    string ParseChatResponse(string json);
    List<string> ParseModelsResponse(string json);
}
