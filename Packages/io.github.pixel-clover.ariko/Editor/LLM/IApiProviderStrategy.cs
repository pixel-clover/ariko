using System.Collections.Generic;

/// <summary>
///     Defines the strategy for interacting with a specific AI provider's API.
///     This includes building URLs, creating request bodies, and parsing responses.
/// </summary>
public interface IApiProviderStrategy
{
    /// <summary>
    ///     Gets the URL for fetching the list of available models from the provider.
    /// </summary>
    /// <param name="settings">The Ariko settings.</param>
    /// <param name="apiKeys">A dictionary of API keys.</param>
    /// <returns>A web request result containing the models URL or an error.</returns>
    WebRequestResult<string> GetModelsUrl(ArikoSettings settings, Dictionary<string, string> apiKeys);

    /// <summary>
    ///     Gets the URL for sending a chat request to the provider.
    /// </summary>
    /// <param name="modelName">The name of the model to use.</param>
    /// <param name="settings">The Ariko settings.</param>
    /// <param name="apiKeys">A dictionary of API keys.</param>
    /// <returns>A web request result containing the chat URL or an error.</returns>
    WebRequestResult<string> GetChatUrl(string modelName, ArikoSettings settings, Dictionary<string, string> apiKeys);

    /// <summary>
    ///     Gets the authorization header value for the provider.
    /// </summary>
    /// <param name="settings">The Ariko settings.</param>
    /// <param name="apiKeys">A dictionary of API keys.</param>
    /// <returns>A web request result containing the auth token or an error.</returns>
    WebRequestResult<string> GetAuthHeader(ArikoSettings settings, Dictionary<string, string> apiKeys);

    /// <summary>
    ///     Builds the JSON body for a chat request.
    /// </summary>
    /// <param name="messages">The history of messages to send to the model.</param>
    /// <param name="modelName">The name of the model being used.</param>
    /// <returns>A JSON string representing the request body.</returns>
    string BuildChatRequestBody(List<ChatMessage> messages, string modelName);

    /// <summary>
    ///     Parses the JSON response from a chat request to extract the content.
    /// </summary>
    /// <param name="json">The JSON response from the API.</param>
    /// <returns>The extracted chat message content.</returns>
    string ParseChatResponse(string json);

    /// <summary>
    ///     Parses the JSON response from a models request to extract the list of model names.
    /// </summary>
    /// <param name="json">The JSON response from the API.</param>
    /// <returns>A list of available model names.</returns>
    List<string> ParseModelsResponse(string json);
}
