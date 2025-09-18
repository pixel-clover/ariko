using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
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
        ///     Google AI (for example, Gemini 2.5 Pro).
        /// </summary>
        Google,

        /// <summary>
        ///     OpenAI (for example, GPT-5).
        /// </summary>
        OpenAI,

        /// <summary>
        ///     Ollama for locally hosted models.
        /// </summary>
        Ollama
    }

    private const int RequestTimeout = 30; // seconds
    private readonly Dictionary<AIProvider, IApiProviderStrategy> strategies;
    private CancellationTokenSource cancellationTokenSource;

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
        cancellationTokenSource?.Cancel();
    }

    /// <summary>
    ///     Sends a chat request to the specified AI provider.
    /// </summary>
    /// <param name="messages">The history of messages to send to the model.</param>
    /// <param name="provider">The AI provider to use.</param>
    /// <param name="modelName">The specific model to query.</param>
    /// <param name="settings">The Ariko settings.</param>
    /// <param name="apiKeys">A dictionary of API keys for the providers.</param>
    /// <returns>A web request result containing the AI's response text or an error.</returns>
    public async Task<WebRequestResult<string>> SendChatRequest(List<ChatMessage> messages, AIProvider provider,
        string modelName,
        ArikoSettings settings, Dictionary<string, string> apiKeys)
    {
        if (string.IsNullOrEmpty(modelName))
            return WebRequestResult<string>.Fail("No AI model selected.", ErrorType.Unknown);

        if (messages == null || messages.Count == 0)
            return WebRequestResult<string>.Fail("Cannot send an empty message history.", ErrorType.Unknown);

        if (!strategies.TryGetValue(provider, out var strategy))
            return WebRequestResult<string>.Fail("Provider not supported.", ErrorType.Unknown);

        var urlResult = strategy.GetChatUrl(modelName, settings, apiKeys);
        if (!urlResult.IsSuccess) return WebRequestResult<string>.Fail(urlResult.Error, urlResult.ErrorType);

        var authResult = strategy.GetAuthHeader(settings, apiKeys);
        if (!authResult.IsSuccess) return WebRequestResult<string>.Fail(authResult.Error, authResult.ErrorType);

        var jsonBody = strategy.BuildChatRequestBody(messages, modelName);

        return await SendPostRequest(urlResult.Data, authResult.Data, jsonBody, strategy.ParseChatResponse);
    }

    /// <summary>
    ///     Sends a chat request in streaming mode. Invokes onChunk as text deltas arrive and onComplete on finish.
    /// </summary>
    public void SendChatRequestStreamed(List<ChatMessage> messages, AIProvider provider, string modelName,
        ArikoSettings settings, Dictionary<string, string> apiKeys, Action<string> onChunk,
        Action<WebRequestResult<string>> onComplete)
    {
        if (string.IsNullOrEmpty(modelName))
        {
            onComplete?.Invoke(WebRequestResult<string>.Fail("No AI model selected.", ErrorType.Unknown));
            return;
        }
        if (messages == null || messages.Count == 0)
        {
            onComplete?.Invoke(WebRequestResult<string>.Fail("Cannot send an empty message history.", ErrorType.Unknown));
            return;
        }
        if (!strategies.TryGetValue(provider, out var strategy))
        {
            onComplete?.Invoke(WebRequestResult<string>.Fail("Provider not supported.", ErrorType.Unknown));
            return;
        }

        var urlResult = strategy.GetChatUrl(modelName, settings, apiKeys);
        if (!urlResult.IsSuccess)
        {
            onComplete?.Invoke(WebRequestResult<string>.Fail(urlResult.Error, urlResult.ErrorType));
            return;
        }
        var authResult = strategy.GetAuthHeader(settings, apiKeys);
        if (!authResult.IsSuccess)
        {
            onComplete?.Invoke(WebRequestResult<string>.Fail(authResult.Error, authResult.ErrorType));
            return;
        }

        var jsonBody = strategy.BuildChatRequestBody(messages, modelName);
        // Make a best-effort to enable streaming on providers that support it without changing strategy interface further
        if (provider == AIProvider.OpenAI)
        {
            // Insert "stream":true at root if not present
            if (jsonBody.TrimEnd().EndsWith("}")) jsonBody = jsonBody.TrimEnd().TrimEnd('}') + ",\"stream\":true}";
        }
        else if (provider == AIProvider.Ollama)
        {
            jsonBody = jsonBody.Replace("\"stream\": false", "\"stream\": true");
        }

        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        var request = new UnityWebRequest(urlResult.Data, "POST");
        request.timeout = RequestTimeout;

        var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        var downloadHandler = new DownloadHandlerBuffer();
        request.downloadHandler = downloadHandler;
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(authResult.Data)) request.SetRequestHeader("Authorization", authResult.Data);

        var operation = request.SendWebRequest();
        // Abort request if cancellation is requested
        try
        {
            var token = cancellationTokenSource.Token;
            token.Register(() =>
            {
                try { request.Abort(); } catch { /* ignore */ }
            });
        }
        catch { /* ignore */ }

        var lastLength = 0;
        var aggregate = new StringBuilder();

#if UNITY_EDITOR
        void Tick()
        {
            if (cancellationTokenSource == null)
            {
                UnityEditor.EditorApplication.update -= Tick;
                return;
            }

            // Emit any new data
            var text = downloadHandler.text;
            if (text != null && text.Length > lastLength)
            {
                var chunkRaw = text.Substring(lastLength);
                lastLength = text.Length;
                var parsed = strategy.ParseChatResponseStream(chunkRaw);
                if (!string.IsNullOrEmpty(parsed))
                {
                    aggregate.Append(parsed);
                    try { onChunk?.Invoke(parsed); } catch { /* ignore UI errors */ }
                }
            }

            // Check completion
            if (operation.isDone)
            {
                UnityEditor.EditorApplication.update -= Tick;
                try
                {
                    var finalResult = HandleApiResponse(request, _ => aggregate.ToString());
                    onComplete?.Invoke(finalResult);
                }
                finally
                {
                    request.Dispose();
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
            }
            else if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError || request.result == UnityWebRequest.Result.DataProcessingError)
            {
                UnityEditor.EditorApplication.update -= Tick;
                try
                {
                    var errorResult = HandleApiResponse<string>(request, _ => aggregate.ToString());
                    onComplete?.Invoke(errorResult);
                }
                finally
                {
                    request.Dispose();
                    cancellationTokenSource?.Dispose();
                    cancellationTokenSource = null;
                }
            }
        }
        UnityEditor.EditorApplication.update += Tick;
#endif
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
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        using var request = UnityWebRequest.Get(url);
        request.timeout = RequestTimeout;
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);

        try
        {
            await request.SendWebRequest().AsTask(cancellationTokenSource.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            return WebRequestResult<List<string>>.Fail("Request cancelled by user.", ErrorType.Cancellation);
        }
        finally
        {
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }

        return HandleApiResponse(request, parser);
    }

    private async Task<WebRequestResult<string>> SendPostRequest(string url, string authToken, string jsonBody,
        Func<string, string> parser)
    {
        cancellationTokenSource?.Cancel();
        cancellationTokenSource = new CancellationTokenSource();

        using var request = new UnityWebRequest(url, "POST");
        request.timeout = RequestTimeout;

        var bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        if (!string.IsNullOrEmpty(authToken)) request.SetRequestHeader("Authorization", authToken);

        try
        {
            await request.SendWebRequest().AsTask(cancellationTokenSource.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException)
        {
            return WebRequestResult<string>.Fail("Request cancelled by user.", ErrorType.Cancellation);
        }
        finally
        {
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
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
