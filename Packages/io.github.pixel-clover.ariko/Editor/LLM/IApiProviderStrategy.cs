// csharp
// File: Assets/Ariko/Editor/LLM/IApiProviderStrategy.cs

using System.Collections.Generic;

public interface IApiProviderStrategy
{
    string GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys);
    string GetChatUrl(string modelName, ArikoSettings settings, Dictionary<string, string> apiKeys);
    string GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys);
    string BuildChatRequestBody(string prompt, string modelName);
    string ParseChatResponse(string json);
    List<string> ParseModelsResponse(string json);
}
