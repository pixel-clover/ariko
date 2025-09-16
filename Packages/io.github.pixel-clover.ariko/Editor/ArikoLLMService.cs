using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
///     Service class for handling communication with various Large Language Model (LLM) providers.
/// </summary>
public class ArikoLLMService
{
    /// <summary>
    ///     Defines the supported AI providers.
    /// </summary>
    public enum AIProvider
    {
        /// <summary>
        ///     Google AI (e.g., Gemini).
        /// </summary>
        Google,

        /// <summary>
        ///     OpenAI (e.g., GPT-4).
        /// </summary>
        OpenAI,

        /// <summary>
        ///     Ollama for locally hosted models.
        /// </summary>
        Ollama
    }

    private const int RequestTimeout = 30; // seconds
    private readonly Dictionary<AIProvider, IApiProviderStrategy> strategies;
    private UnityWebRequest activeRequest;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArikoLLMService" /> class.
    /// </summary>
    public ArikoLLMService()
    {
        strategies = new Dictionary<AIProvider, IApiProviderStrategy>
        {
            { AIProvider.Google, new GoogleStrategy() },
            { AIProvider.OpenAI, new OpenAiStrategy() },
            { AIProvider.Ollama, new OllamaStrategy() }
        };
    }

    /// <summary>
    ///     Cancels the currently active web request, if any.
    /// </summary>
    public void CancelRequest()
    {
        if (activeRequest != null && !activeRequest.isDone) activeRequest.Abort();
    }

    /// <summary>
    ///     Sends a chat request to the specified AI provider.
    /// </summary>
    /// <param name="prompt">The complete prompt to send to the model.</param>
    /// <param name="provider">The AI provider to use.</param>
    /// <param name="modelName">The specific model to query.</param>
    /// <param name="settings">The Ariko settings.</param>
    /// <param name="apiKeys">A dictionary of API keys for the providers.</param>
    /// <returns>A web request result containing the AI's response text or an error.</returns>
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

    /// <summary>
    ///     Fetches the list of available models from the specified AI provider.
    /// </summary>
    /// <param name="provider">The AI provider to query.</param>
    /// <param name="settings">The Ariko settings.</param>
    /// <param name="apiKeys">A dictionary of API keys for the providers.</param>
    /// <returns>A web request result containing a list of model names or an error.</returns>
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
