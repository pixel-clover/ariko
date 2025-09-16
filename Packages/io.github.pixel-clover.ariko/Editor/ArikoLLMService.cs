using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
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

    private const int RequestTimeout = 30; // seconds
    private readonly Dictionary<AIProvider, IApiProviderStrategy> strategies;
    private UnityWebRequest activeRequest;

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
        if (activeRequest != null && !activeRequest.isDone) activeRequest.Abort();
    }

    public async Task<WebRequestResult<string>> SendChatRequest(string prompt, AIProvider provider, string modelName,
        ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        if (string.IsNullOrEmpty(modelName))
            return WebRequestResult<string>.Fail("No AI model selected.", ErrorType.Unknown);

        if (!strategies.TryGetValue(provider, out var strategy))
            return WebRequestResult<string>.Fail("Provider not supported.", ErrorType.Unknown);

        var urlResult = strategy.GetChatUrl(modelName, settings, apiKeys);
        if (!urlResult.IsSuccess) return WebRequestResult<string>.Fail(urlResult.Error, urlResult.ErrorType);

        var authResult = strategy.GetAuthHeader(settings, apiKeys);
        if (!authResult.IsSuccess) return WebRequestResult<string>.Fail(authResult.Error, authResult.ErrorType);

        var jsonBody = strategy.BuildChatRequestBody(prompt, modelName);

        return await SendPostRequest(urlResult.Data, authResult.Data, jsonBody, strategy.ParseChatResponse);
    }

    public async Task<WebRequestResult<List<string>>> FetchAvailableModels(AIProvider provider, ArikoSettings settings,
        Dictionary<string, string> apiKeys)
    {
        if (!strategies.TryGetValue(provider, out var strategy))
            return WebRequestResult<List<string>>.Fail("Provider not supported.", ErrorType.Unknown);

        var urlResult = strategy.GetModelsUrl(settings, apiKeys);
        if (!urlResult.IsSuccess)
            return WebRequestResult<List<string>>.Fail(urlResult.Error, urlResult.ErrorType);

        var authResult = strategy.GetAuthHeader(settings, apiKeys);
        if (!authResult.IsSuccess)
            return WebRequestResult<List<string>>.Fail(authResult.Error, authResult.ErrorType);

        return await SendGetRequest(urlResult.Data, authResult.Data, strategy.ParseModelsResponse);
    }

    private async Task<WebRequestResult<List<string>>> SendGetRequest(string url, string authToken,
        Func<string, List<string>> parser)
    {
        using var request = UnityWebRequest.Get(url);
        request.timeout = RequestTimeout;
        activeRequest = request;
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);

        try
        {
            await request.SendWebRequest().AsTask();
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            return WebRequestResult<List<string>>.Fail("Request cancelled by user.", ErrorType.Cancellation);
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
        request.timeout = RequestTimeout;
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
            return WebRequestResult<string>.Fail("Request cancelled by user.", ErrorType.Cancellation);
        }
        finally
        {
            activeRequest = null;
        }

        return HandleApiResponse(request, parser);
    }

    private WebRequestResult<T> HandleApiResponse<T>(UnityWebRequest request, Func<string, T> parser)
    {
        switch (request.result)
        {
            case UnityWebRequest.Result.Success:
                try
                {
                    var parsedData = parser(request.downloadHandler.text);
                    return WebRequestResult<T>.Success(parsedData);
                }
                catch (JsonException ex)
                {
                    var error = $"Failed to parse JSON response: {ex.Message}";
                    Debug.LogError($"Ariko: {error}\nResponse: {request.downloadHandler.text}");
                    return WebRequestResult<T>.Fail(error, ErrorType.Parsing);
                }
                catch (Exception ex)
                {
                    var error = $"An unexpected error occurred during parsing: {ex.Message}";
                    Debug.LogError($"Ariko: {error}\nResponse: {request.downloadHandler.text}");
                    return WebRequestResult<T>.Fail(error, ErrorType.Unknown);
                }

            case UnityWebRequest.Result.ConnectionError:
                var networkError = $"Network error: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    networkError += $"\nDetails: {request.downloadHandler.text}";
                Debug.LogError($"Ariko: {networkError}");
                return WebRequestResult<T>.Fail(networkError, ErrorType.Network);

            case UnityWebRequest.Result.ProtocolError:
                var httpError = $"HTTP error: {request.responseCode}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    httpError += $"\nDetails: {request.downloadHandler.text}";
                Debug.LogError($"Ariko: {httpError}");
                return WebRequestResult<T>.Fail(httpError, ErrorType.Http);

            case UnityWebRequest.Result.DataProcessingError:
                var dataProcessingError = $"Data processing error: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    dataProcessingError += $"\nDetails: {request.downloadHandler.text}";
                Debug.LogError($"Ariko: {dataProcessingError}");
                return WebRequestResult<T>.Fail(dataProcessingError, ErrorType.Unknown);

            default:
                var defaultError = $"Unknown error: {request.error}";
                if (!string.IsNullOrEmpty(request.downloadHandler?.text))
                    defaultError += $"\nDetails: {request.downloadHandler.text}";
                Debug.LogError($"Ariko: {defaultError}");
                return WebRequestResult<T>.Fail(defaultError, ErrorType.Unknown);
        }
    }
}
