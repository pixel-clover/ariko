// csharp
// File: Assets/Ariko/Editor/LLM/IApiProviderStrategy.cs

using System.Collections.Generic;

public interface IApiProviderStrategy
{
    string GetModelsUrl(ArikoSettings settings);
    string GetChatUrl(string modelName, ArikoSettings settings);
    string GetAuthHeader(ArikoSettings settings);
    string BuildChatRequestBody(string prompt, string modelName);
    string ParseChatResponse(string json);
    List<string> ParseModelsResponse(string json);
}
