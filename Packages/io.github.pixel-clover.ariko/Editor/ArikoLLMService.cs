using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ArikoLLMService
{
    public enum AIProvider
    {
        Google,
        OpenAI,
        Ollama
    }

    private readonly Dictionary<AIProvider, IApiProviderStrategy> strategies;

    public ArikoLLMService()
    {
        strategies = new Dictionary<AIProvider, IApiProviderStrategy>
        {
            { AIProvider.Google, new GoogleStrategy() },
            { AIProvider.OpenAI, new OpenAiStrategy() },
            { AIProvider.Ollama, new OllamaStrategy() }
        };
    }

    public void SendChatRequest(string prompt, AIProvider provider, string modelName, ArikoSettings settings,
        Action<WebRequestResult<string>> onComplete)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            onComplete?.Invoke(new WebRequestResult<string>(null, "Error: No AI model selected."));
            return;
        }

        if (!strategies.TryGetValue(provider, out var strategy))
        {
            onComplete?.Invoke(new WebRequestResult<string>(null, "Error: Provider not supported."));
            return;
        }

        var url = strategy.GetChatUrl(modelName, settings);
        var authToken = strategy.GetAuthHeader(settings);
        var jsonBody = strategy.BuildChatRequestBody(prompt, modelName);

        SendPostRequest(url, authToken, jsonBody, onComplete, strategy.ParseChatResponse);
    }

    public void FetchAvailableModels(AIProvider provider, ArikoSettings settings,
        Action<WebRequestResult<List<string>>> onComplete)
    {
        if (!strategies.TryGetValue(provider, out var strategy))
        {
            onComplete?.Invoke(new WebRequestResult<List<string>>(null, "Error: Provider not supported."));
            return;
        }

        var url = strategy.GetModelsUrl(settings);
        var authToken = strategy.GetAuthHeader(settings);

        SendGetRequest(url, authToken, onComplete, strategy.ParseModelsResponse);
    }

    private void SendGetRequest(string url, string authToken, Action<WebRequestResult<List<string>>> onComplete,
        Func<string, List<string>> parser)
    {
        var request = UnityWebRequest.Get(url);
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);
        var op = request.SendWebRequest();
        op.completed += _ => HandleApiResponse(request, onComplete, parser);
    }

    private void SendPostRequest(string url, string authToken, string jsonBody,
        Action<WebRequestResult<string>> onComplete, Func<string, string> parser)
    {
        var request = new UnityWebRequest(url, "POST");
        var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);
        var op = request.SendWebRequest();
        op.completed += _ => HandleApiResponse(request, onComplete, parser);
    }

    private void HandleApiResponse<T>(UnityWebRequest request, Action<WebRequestResult<T>> onComplete,
        Func<string, T> parser)
    {
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var parsedData = parser(request.downloadHandler.text);
                onComplete?.Invoke(new WebRequestResult<T>(parsedData, null));
            }
            catch (Exception ex)
            {
                var error = $"Failed to parse response: {ex.Message}";
                Debug.LogError($"Ariko: {error}\nResponse: {request.downloadHandler.text}");
                onComplete?.Invoke(new WebRequestResult<T>(default, error));
            }
        }
        else
        {
            var error = $"API request failed: {request.error}\nDetails: {request.downloadHandler.text}";
            Debug.LogError($"Ariko: {error}");
            onComplete?.Invoke(new WebRequestResult<T>(default, error));
        }

        request.Dispose();
    }
}
