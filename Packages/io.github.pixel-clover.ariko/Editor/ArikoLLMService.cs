using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class ArikoLLMService
{
    private UnityWebRequest activeRequest;

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

    public void CancelRequest()
    {
        if (activeRequest != null && !activeRequest.isDone)
        {
            activeRequest.Abort();
        }
    }

    public async Task<WebRequestResult<string>> SendChatRequest(string prompt, AIProvider provider, string modelName,
        ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        if (string.IsNullOrEmpty(modelName)) return new WebRequestResult<string>(null, "Error: No AI model selected.");

        if (!strategies.TryGetValue(provider, out var strategy))
            return new WebRequestResult<string>(null, "Error: Provider not supported.");

        var url = strategy.GetChatUrl(modelName, settings, apiKeys);
        var authToken = strategy.GetAuthHeader(settings, apiKeys);
        var jsonBody = strategy.BuildChatRequestBody(prompt, modelName);

        return await SendPostRequest(url, authToken, jsonBody, strategy.ParseChatResponse);
    }

    public async Task<WebRequestResult<List<string>>> FetchAvailableModels(AIProvider provider, ArikoSettings settings,
        Dictionary<string, string> apiKeys)
    {
        if (!strategies.TryGetValue(provider, out var strategy))
            return new WebRequestResult<List<string>>(null, "Error: Provider not supported.");

        var url = strategy.GetModelsUrl(settings, apiKeys);
        var authToken = strategy.GetAuthHeader(settings, apiKeys);

        return await SendGetRequest(url, authToken, strategy.ParseModelsResponse);
    }

    private async Task<WebRequestResult<List<string>>> SendGetRequest(string url, string authToken,
        Func<string, List<string>> parser)
    {
        using var request = UnityWebRequest.Get(url);
        activeRequest = request;
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);

        try
        {
            await request.SendWebRequest().AsTask();
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            // This catches the exception thrown when the task is cancelled by Abort().
            return new WebRequestResult<List<string>>(default, "Request cancelled by user.");
        }
        finally
        {
            activeRequest = null;
        }

        return HandleApiResponse(request, parser);
    }

    private async Task<WebRequestResult<string>> SendPostRequest(string url, string authToken, string jsonBody,
        Func<string, string> parser)
    {
        using var request = new UnityWebRequest(url, "POST");
        activeRequest = request;

        var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);

        try
        {
            await request.SendWebRequest().AsTask();
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            return new WebRequestResult<string>(default, "Request cancelled by user.");
        }
        finally
        {
            activeRequest = null;
        }

        return HandleApiResponse(request, parser);
    }

    private WebRequestResult<T> HandleApiResponse<T>(UnityWebRequest request, Func<string, T> parser)
    {
        if (request.result == UnityWebRequest.Result.Aborted)
        {
            return new WebRequestResult<T>(default, "Request cancelled by user.");
        }

        if (request.result == UnityWebRequest.Result.Success)
            try
            {
                var parsedData = parser(request.downloadHandler.text);
                return new WebRequestResult<T>(parsedData, null);
            }
            catch (Exception ex)
            {
                var error = $"Failed to parse response: {ex.Message}";
                Debug.LogError($"Ariko: {error}\nResponse: {request.downloadHandler.text}");
                return new WebRequestResult<T>(default, error);
            }

        var requestError = $"API request failed: {request.error}\nDetails: {request.downloadHandler.text}";
        Debug.LogError($"Ariko: {requestError}");
        return new WebRequestResult<T>(default, requestError);
    }
}
