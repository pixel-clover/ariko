using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
///     Manages the logic for the Ariko chat window, including sending requests to the LLM,
///     managing chat history, and handling context from the Unity Editor.
/// </summary>
public class ArikoChatController
{
    private readonly Dictionary<string, string> apiKeys = new();
    private readonly ArikoLLMService llmService;
    private readonly ArikoSettings settings;

    /// <summary>
    ///     Event triggered when the chat is cleared, instructing the view to clear the message display.
    /// </summary>
    public Action OnChatCleared;

    /// <summary>
    ///     Event triggered when a different chat session is loaded, instructing the view to reload messages.
    /// </summary>
    public Action OnChatReloaded;

    // --- Events for the View to subscribe to ---
    /// <summary>
    ///     Event triggered when an error occurs, providing the error message.
    /// </summary>
    public Action<string> OnError;

    /// <summary>
    ///     Event triggered when the chat history changes (e.g., a session is added or deleted).
    /// </summary>
    public Action OnHistoryChanged;

    /// <summary>
    ///     Event triggered when a new message is added to the active session.
    /// </summary>
    public Action<ChatMessage> OnMessageAdded;

    /// <summary>
    ///     Event triggered when the list of available models for a provider has been fetched.
    /// </summary>
    public Action<List<string>> OnModelsFetched;

    /// <summary>
    ///     Event triggered when the assistant's response status changes.
    /// </summary>
    /// <remarks>
    ///     The boolean parameter is true if the assistant is currently generating a response, and false otherwise.
    /// </remarks>
    public Action<bool> OnResponseStatusChanged; // isPending

    /// <summary>
    ///     Event triggered when an agent requests to execute a tool, requiring user confirmation.
    /// </summary>
    public Action<ToolCall> OnToolCallConfirmationRequested;

    private ToolCall pendingToolCall;
    private ToolRegistry toolRegistry;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArikoChatController" /> class.
    /// </summary>
    /// <param name="settings">The Ariko settings asset.</param>
    public ArikoChatController(ArikoSettings settings)
    {
        this.settings = settings;
        llmService = new ArikoLLMService();
        ReloadToolRegistry(settings.selectedWorkMode);

        ChatHistory = ChatHistoryStorage.LoadHistory() ?? new List<ChatSession>();
        if (ChatHistory.Count == 0)
        {
            // If no history, create a new one
            ActiveSession = new ChatSession();
            ChatHistory.Add(ActiveSession);
        }
        else
        {
            // Otherwise, the first session in the list is the most recent one
            ActiveSession = ChatHistory[0];
        }

        LoadApiKeysFromEnvironment();
    }

    // --- Properties for Chat History ---
    /// <summary>
    ///     Gets the list of all chat sessions.
    /// </summary>
    public List<ChatSession> ChatHistory { get; }

    /// <summary>
    ///     Gets the currently active chat session.
    /// </summary>
    public ChatSession ActiveSession { get; private set; }

    /// <summary>
    ///     Gets the list of assets manually attached to the current chat context.
    /// </summary>
    public List<Object> ManuallyAttachedAssets { get; } = new();

    /// <summary>
    ///     Gets or sets a value indicating whether to automatically include the current selection as context.
    /// </summary>
    public bool AutoContext { get; set; } = true;

    /// <summary>
    ///     Loads API keys from environment variables.
    /// </summary>
    public void LoadApiKeysFromEnvironment()
    {
        apiKeys["Google"] = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
        apiKeys["OpenAI"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL");
        if (!string.IsNullOrEmpty(ollamaUrl)) settings.ollama_Url = ollamaUrl;
    }

    /// <summary>
    ///     Gets the API key for a specific provider.
    /// </summary>
    /// <param name="provider">The name of the AI provider (e.g., "Google", "OpenAI").</param>
    /// <returns>The API key for the specified provider, or null if not found.</returns>
    public string GetApiKey(string provider)
    {
        return apiKeys.TryGetValue(provider, out var key) ? key : null;
    }

    /// <summary>
    ///     Sets the API key for a specific provider.
    /// </summary>
    /// <param name="provider">The name of the AI provider.</param>
    /// <param name="key">The API key.</param>
    public void SetApiKey(string provider, string key)
    {
        apiKeys[provider] = key;
    }

    public void ReloadToolRegistry(string workMode)
    {
        toolRegistry = new ToolRegistry(settings, workMode);
    }

    /// <summary>
    ///     Sends a message from the user to the AI assistant.
    /// </summary>
    /// <param name="text">The user's message text.</param>
    /// <param name="selectedProvider">The AI provider to use for the request.</param>
    /// <param name="selectedModel">The specific model to use for the request.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendMessageToAssistant(string text, string selectedProvider, string selectedModel)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Add user message to session and notify view
        var userMessage = new ChatMessage { Role = "User", Content = text };
        ActiveSession.Messages.Add(userMessage);
        OnMessageAdded?.Invoke(userMessage);

        if (settings.selectedWorkMode == "Agent")
        {
            await SendAgentRequest(selectedProvider, selectedModel);
        }
        else
        {
            OnResponseStatusChanged?.Invoke(true);

            var messagesToSend = new List<ChatMessage>();
            var context = BuildContextString();

            // Add system prompt and context as the first message
            if (!string.IsNullOrEmpty(settings.systemPrompt) || !string.IsNullOrEmpty(context))
            {
                var systemContent = new StringBuilder();
                if(!string.IsNullOrEmpty(settings.systemPrompt))
                {
                    systemContent.AppendLine(settings.systemPrompt);
                }
                if(!string.IsNullOrEmpty(context))
                {
                    systemContent.AppendLine("\n--- Context ---");
                    systemContent.AppendLine(context);
                }
                messagesToSend.Add(new ChatMessage { Role = "System", Content = systemContent.ToString() });
            }

            // Add all messages from the current session.
            // The user's latest message is already in this list.
            messagesToSend.AddRange(ActiveSession.Messages);

            var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
            var result = await llmService.SendChatRequest(messagesToSend, provider, selectedModel, settings, apiKeys);

            string newContent;
            if (result.IsSuccess)
                newContent = result.Data;
            else
                newContent = GetFormattedErrorMessage(result.Error, result.ErrorType);
            var finalMessage = new ChatMessage { Role = "Ariko", Content = newContent, IsError = !result.IsSuccess };
            ActiveSession.Messages.Add(finalMessage);
            OnMessageAdded?.Invoke(finalMessage);

            OnResponseStatusChanged?.Invoke(false);
        }
    }

    private async Task SendAgentRequest(string selectedProvider, string selectedModel)
    {
        OnResponseStatusChanged?.Invoke(true);

        var messagesToSend = new List<ChatMessage>();
        var toolDefinitions = toolRegistry.GetToolDefinitionsForPrompt();

        var systemPromptBuilder = new StringBuilder();
        systemPromptBuilder.AppendLine(settings.agentSystemPrompt);
        systemPromptBuilder.AppendLine(toolDefinitions);

        var context = BuildContextString();
        if (!string.IsNullOrEmpty(context))
        {
            systemPromptBuilder.AppendLine("\n--- Context ---");
            systemPromptBuilder.AppendLine(context);
        }

        messagesToSend.Add(new ChatMessage { Role = "System", Content = systemPromptBuilder.ToString() });

        // Add all messages from the current session.
        messagesToSend.AddRange(ActiveSession.Messages);

        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.SendChatRequest(messagesToSend, provider, selectedModel, settings, apiKeys);
        OnResponseStatusChanged?.Invoke(false);

        if (result.IsSuccess)
        {
            if (TryParseToolCall(result.Data, out var toolCall))
            {
                pendingToolCall = toolCall;
                OnToolCallConfirmationRequested?.Invoke(toolCall);
            }
            else
            {
                // It's a conversational response
                var finalMessage = new ChatMessage { Role = "Ariko", Content = result.Data };
                ActiveSession.Messages.Add(finalMessage);
                OnMessageAdded?.Invoke(finalMessage);
            }
        }
        else
        {
            var errorMessage = GetFormattedErrorMessage(result.Error, result.ErrorType);
            var finalMessage = new ChatMessage { Role = "Ariko", Content = errorMessage, IsError = true };
            ActiveSession.Messages.Add(finalMessage);
            OnMessageAdded?.Invoke(finalMessage);
        }
    }

    /// <summary>
    ///     Responds to a tool execution confirmation request from the user.
    /// </summary>
    /// <param name="userApproved">True if the user approved the tool execution; otherwise, false.</param>
    /// <param name="selectedProvider">The AI provider to use for the subsequent request.</param>
    /// <param name="selectedModel">The specific model to use for the subsequent request.</param>
    public async void RespondToToolConfirmation(bool userApproved, string selectedProvider, string selectedModel)
    {
        if (pendingToolCall == null) return;

        var toolCall = pendingToolCall;
        pendingToolCall = null; // Clear the pending call

        if (userApproved)
        {
            var tool = toolRegistry.GetTool(toolCall.tool_name);
            string executionResult;
            if (tool != null)
            {
                var context = new ToolExecutionContext
                {
                    Arguments = toolCall.parameters,
                    Provider = selectedProvider,
                    Model = selectedModel,
                    Settings = settings,
                    ApiKeys = apiKeys
                };
                executionResult = await tool.Execute(context);
            }
            else
            {
                executionResult = $"Error: Tool '{toolCall.tool_name}' not found.";
            }
            var resultMessage = new ChatMessage { Role = "User", Content = $"Observation: {executionResult}" };
            ActiveSession.Messages.Add(resultMessage);
            OnMessageAdded?.Invoke(resultMessage);

            // Send the observation back to the LLM to continue the loop
            await SendAgentRequest(selectedProvider, selectedModel);
        }
        else
        {
            var denialMessage = new ChatMessage { Role = "User", Content = "Observation: User denied the action." };
            ActiveSession.Messages.Add(denialMessage);
            OnMessageAdded?.Invoke(denialMessage);
            // Inform the LLM that the user denied the action
            await SendAgentRequest(selectedProvider, selectedModel);
        }
    }

    private bool TryParseToolCall(string response, out ToolCall toolCall)
    {
        toolCall = null;
        try
        {
            var match = Regex.Match(response, @"```json\s*([\s\S]*?)\s*```", RegexOptions.Singleline);
            var jsonToParse = match.Success ? match.Groups[1].Value : response;

            var agentResponse = JsonConvert.DeserializeObject<AgentResponse>(jsonToParse);

            if (string.IsNullOrWhiteSpace(agentResponse?.ToolName) ||
                toolRegistry.GetTool(agentResponse.ToolName) == null) return false;

            toolCall = new ToolCall
            {
                thought = agentResponse.Thought,
                tool_name = agentResponse.ToolName,
                parameters = agentResponse.Parameters ?? new Dictionary<string, object>()
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Fetches the available models for the currently selected AI provider.
    /// </summary>
    /// <param name="selectedProvider">The AI provider to fetch models from.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task FetchModelsForCurrentProvider(string selectedProvider)
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.FetchAvailableModels(provider, settings, apiKeys);
        if (result.IsSuccess)
        {
            OnModelsFetched?.Invoke(result.Data);
        }
        else
        {
            var errorMessage = GetFormattedErrorMessage(result.Error, result.ErrorType);
            OnError?.Invoke(errorMessage);
            OnModelsFetched?.Invoke(new List<string> { "Error" });
        }
    }

    private string GetFormattedErrorMessage(string error, ErrorType errorType)
    {
        switch (errorType)
        {
            case ErrorType.Auth:
                return $"Authentication Error: {error}\nPlease check your API key or settings.";
            case ErrorType.Network:
                return $"Network Error: {error}\nPlease check your internet connection.";
            case ErrorType.Http:
                return $"API Error: {error}";
            case ErrorType.Parsing:
                return $"Response Parsing Error: {error}\nThe data from the server was in an unexpected format.";
            case ErrorType.Cancellation:
                return "Request was cancelled.";
            default:
                return $"An unknown error occurred: {error}";
        }
    }


    /// <summary>
    ///     Clears the current chat session and starts a new one.
    /// </summary>
    public void ClearChat()
    {
        // Don't create a new session if the current one is empty. Just clear the display.
        if (ActiveSession != null && ActiveSession.Messages.Count == 0)
        {
            OnChatCleared?.Invoke();
            return;
        }

        ActiveSession = new ChatSession();
        ChatHistory.Insert(0, ActiveSession);

        // Enforce history size limit (if set)
        if (settings.chatHistorySize > 0 && ChatHistory.Count > settings.chatHistorySize)
            ChatHistory.RemoveAt(ChatHistory.Count - 1);

        ManuallyAttachedAssets.Clear();
        OnChatCleared?.Invoke(); // Tells view to clear the scrollview
        OnHistoryChanged?.Invoke(); // Tells view to update history list
    }

    /// <summary>
    ///     Switches the active chat to the specified session.
    /// </summary>
    /// <param name="session">The chat session to make active.</param>
    public void SwitchToSession(ChatSession session)
    {
        if (session == null || session == ActiveSession) return;

        ActiveSession = session;
        ManuallyAttachedAssets.Clear(); // Context is per-session for now
        OnChatReloaded?.Invoke();
        OnHistoryChanged?.Invoke(); // To update selection highlight
    }

    /// <summary>
    ///     Deletes a specific chat session from the history.
    /// </summary>
    /// <param name="session">The chat session to delete.</param>
    public void DeleteSession(ChatSession session)
    {
        if (session == null || !ChatHistory.Contains(session)) return;

        var wasActiveSession = session == ActiveSession;
        ChatHistory.Remove(session);

        if (wasActiveSession)
        {
            // If we deleted the active session, switch to the most recent one or create a new one
            if (ChatHistory.Any())
                SwitchToSession(ChatHistory[0]);
            else
                ClearChat(); // Creates a new empty session
        }
        else
        {
            // If we deleted a non-active session, we just need to update the history panel
            OnHistoryChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Deletes all chat sessions from the history.
    /// </summary>
    public void ClearAllHistory()
    {
        ChatHistory.Clear();
        // After clearing everything, create a new empty session to start with
        ClearChat();
    }

    /// <summary>
    ///     Cancels the currently ongoing AI assistant request.
    /// </summary>
    public void CancelCurrentRequest()
    {
        llmService.CancelRequest();
    }

    private string BuildContextString()
    {
        var contextBuilder = new StringBuilder();
        if (AutoContext && Selection.activeObject != null)
        {
            contextBuilder.AppendLine("--- Current Selection Context ---");
            AppendAssetInfo(Selection.activeObject, contextBuilder);
        }

        if (ManuallyAttachedAssets.Any())
        {
            contextBuilder.AppendLine("--- Manually Attached Context ---");
            foreach (var asset in ManuallyAttachedAssets) AppendAssetInfo(asset, contextBuilder);
        }

        return contextBuilder.ToString();
    }

    private void AppendAssetInfo(Object asset, StringBuilder builder)
    {
        if (asset is MonoScript script)
            builder.AppendLine($"[File: {script.name}.cs]\n```csharp\n{script.text}\n```");
        else if (asset is TextAsset textAsset)
            builder.AppendLine($"[File: {textAsset.name}]\n```\n{textAsset.text}\n```");
        else
            builder.AppendLine(
                $"[Asset: {asset.name} ({asset.GetType().Name}) at path {AssetDatabase.GetAssetPath(asset)}]");
        builder.AppendLine();
    }
}
