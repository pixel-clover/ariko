using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using Object = UnityEngine.Object;

/// <summary>
///     Manages the logic for the Ariko chat window, including sending requests to the LLM,
///     managing chat history, and handling context from the Unity Editor.
/// </summary>
public class ArikoChatController
{
    private readonly AgentService agentService;
    private readonly Dictionary<string, string> apiKeys = new();
    private readonly ContextBuilder contextBuilder = new();
    private readonly ArikoLLMService llmService;
    private readonly SessionService sessionService;
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
    public Action<ChatMessage, ChatSession> OnMessageAdded;

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

    private ToolRegistry toolRegistry;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ArikoChatController" /> class.
    /// </summary>
    /// <param name="settings">The Ariko settings asset.</param>
    public ArikoChatController(ArikoSettings settings)
    {
        this.settings = settings;
        llmService = new ArikoLLMService();
        sessionService = new SessionService(settings);
        ReloadToolRegistry(settings.selectedWorkMode);

        // Forward session events and manage per-session manual context
        sessionService.OnChatCleared += () =>
        {
            ManuallyAttachedAssets.Clear();
            OnChatCleared?.Invoke();
        };
        sessionService.OnChatReloaded += () =>
        {
            ManuallyAttachedAssets.Clear();
            OnChatReloaded?.Invoke();
        };
        sessionService.OnHistoryChanged += () => OnHistoryChanged?.Invoke();

        // Initialize AgentService with delegates
        agentService = new AgentService(
            llmService,
            this.settings,
            apiKeys,
            () => ActiveSession,
            () => contextBuilder.BuildContextString(AutoContext, Selection.activeObject, ManuallyAttachedAssets),
            () => toolRegistry,
            isPending => OnResponseStatusChanged?.Invoke(isPending),
            (msg, sess) => OnMessageAdded?.Invoke(msg, sess),
            toolCall => OnToolCallConfirmationRequested?.Invoke(toolCall)
        );

        LoadApiKeysFromEnvironment();
    }

    // --- Properties for Chat History ---
    /// <summary>
    ///     Gets the list of all chat sessions.
    /// </summary>
    public List<ChatSession> ChatHistory => sessionService.ChatHistory;

    /// <summary>
    ///     Gets the currently active chat session.
    /// </summary>
    public ChatSession ActiveSession => sessionService.ActiveSession;

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

        var sessionForThisMessage = ActiveSession;

        // Add user message to session and notify view
        var userMessage = new ChatMessage { Role = "User", Content = text };
        var wasEmpty = sessionForThisMessage.Messages.Count == 0;
        sessionForThisMessage.Messages.Add(userMessage);
        OnMessageAdded?.Invoke(userMessage, sessionForThisMessage);
        if (wasEmpty)
        {
            AutoNameSessionFromText(sessionForThisMessage, text);
        }

        if (settings.selectedWorkMode == "Agent")
        {
            await agentService.SendAgentRequest(selectedProvider, selectedModel);
        }
        else
        {
            OnResponseStatusChanged?.Invoke(true);

            var messagesToSend = BuildMessagesWithContext(sessionForThisMessage);

            var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
            var result = await llmService.SendChatRequest(messagesToSend, provider, selectedModel, settings, apiKeys);

            string newContent;
            if (result.IsSuccess)
                newContent = result.Data;
            else
                newContent = GetFormattedErrorMessage(result.Error, result.ErrorType);
            var finalMessage = new ChatMessage { Role = "Ariko", Content = newContent, IsError = !result.IsSuccess };
            sessionForThisMessage.Messages.Add(finalMessage);
            OnMessageAdded?.Invoke(finalMessage, sessionForThisMessage);

            OnResponseStatusChanged?.Invoke(false);
        }
    }

    /// <summary>
    ///     Sends a message to the assistant and streams the response chunk-by-chunk.
    /// </summary>
    public async Task SendMessageToAssistantStreamed(string text, string selectedProvider, string selectedModel,
        Action<string> onChunk, Action<bool, string> onFinished)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var sessionForThisMessage = ActiveSession;

        // Add user message to session and notify view
        var userMessage = new ChatMessage { Role = "User", Content = text };
        var wasEmpty = sessionForThisMessage.Messages.Count == 0;
        sessionForThisMessage.Messages.Add(userMessage);
        OnMessageAdded?.Invoke(userMessage, sessionForThisMessage);
        if (wasEmpty)
        {
            AutoNameSessionFromText(sessionForThisMessage, text);
        }

        OnResponseStatusChanged?.Invoke(true);

        var messagesToSend = BuildMessagesWithContext(sessionForThisMessage);
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);

        var aggregate = new StringBuilder();
        void HandleChunk(string delta)
        {
            aggregate.Append(delta);
            onChunk?.Invoke(delta);
        }

        void HandleComplete(WebRequestResult<string> result)
        {
            try
            {
                string content;
                var isError = !result.IsSuccess;
                if (result.IsSuccess)
                {
                    content = aggregate.ToString();
                }
                else
                {
                    content = GetFormattedErrorMessage(result.Error, result.ErrorType);
                }
                var finalMessage = new ChatMessage { Role = "Ariko", Content = content, IsError = isError };
                sessionForThisMessage.Messages.Add(finalMessage);
                OnMessageAdded?.Invoke(finalMessage, sessionForThisMessage);

                onFinished?.Invoke(result.IsSuccess, result.IsSuccess ? null : content);
            }
            finally
            {
                OnResponseStatusChanged?.Invoke(false);
            }
        }

        // Fire and forget streaming request; callbacks will finalize state
        llmService.SendChatRequestStreamed(messagesToSend, provider, selectedModel, settings, apiKeys, HandleChunk,
            HandleComplete);
    }

    private List<ChatMessage> BuildMessagesWithContext(ChatSession sessionForThisMessage)
    {
        var messagesToSend = new List<ChatMessage>();
        var context = contextBuilder.BuildContextString(AutoContext, Selection.activeObject, ManuallyAttachedAssets);

        // Add system prompt and context as the first message
        if (!string.IsNullOrEmpty(settings.systemPrompt) || !string.IsNullOrEmpty(context))
        {
            var systemContent = new StringBuilder();
            if (!string.IsNullOrEmpty(settings.systemPrompt)) systemContent.AppendLine(settings.systemPrompt);
            if (!string.IsNullOrEmpty(context))
            {
                systemContent.AppendLine("\n--- Context ---");
                systemContent.AppendLine(context);
            }

            messagesToSend.Add(new ChatMessage { Role = "System", Content = systemContent.ToString() });
        }

        // Add all messages from the current session.
        messagesToSend.AddRange(sessionForThisMessage.Messages);
        return messagesToSend;
    }


    /// <summary>
    ///     Responds to a tool execution confirmation request from the user.
    /// </summary>
    /// <param name="userApproved">True if the user approved the tool execution; otherwise, false.</param>
    /// <param name="selectedProvider">The AI provider to use for the subsequent request.</param>
    /// <param name="selectedModel">The specific model to use for the subsequent request.</param>
    public async void RespondToToolConfirmation(bool userApproved, string selectedProvider, string selectedModel)
    {
        agentService.RespondToToolConfirmation(userApproved, selectedProvider, selectedModel);
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

    /// <summary>
    ///     Gets the last selected model for the given provider from settings.
    /// </summary>
    /// <param name="providerName">Provider name as string matching ArikoLLMService.AIProvider enum.</param>
    /// <returns>Model id or null.</returns>
    public string GetSelectedModelForProvider(string providerName)
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerName);
        switch (provider)
        {
            case ArikoLLMService.AIProvider.Google:
                return settings.google_SelectedModel;
            case ArikoLLMService.AIProvider.OpenAI:
                return settings.openAI_SelectedModel;
            case ArikoLLMService.AIProvider.Ollama:
                return settings.ollama_SelectedModel;
            default:
                return null;
        }
    }

    /// <summary>
    ///     Sets the last selected model for the given provider and persists settings.
    /// </summary>
    /// <param name="providerName">Provider name as string matching ArikoLLMService.AIProvider enum.</param>
    /// <param name="modelName">Model id/name to persist.</param>
    public void SetSelectedModelForProvider(string providerName, string modelName)
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerName);
        switch (provider)
        {
            case ArikoLLMService.AIProvider.Google:
                settings.google_SelectedModel = modelName;
                break;
            case ArikoLLMService.AIProvider.OpenAI:
                settings.openAI_SelectedModel = modelName;
                break;
            case ArikoLLMService.AIProvider.Ollama:
                settings.ollama_SelectedModel = modelName;
                break;
        }

        ArikoSettingsManager.SaveSettings(settings);
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

    private void AutoNameSessionFromText(ChatSession session, string text)
    {
        if (session == null || string.IsNullOrWhiteSpace(text)) return;
        if (!string.IsNullOrEmpty(session.SessionName) && !session.SessionName.StartsWith("Chat started at")) return;
        var name = GenerateSessionName(text);
        session.SessionName = name;
        OnHistoryChanged?.Invoke();
    }

    private string GenerateSessionName(string text)
    {
        var t = text.Trim();
        var newlineIdx = t.IndexOf('\n');
        if (newlineIdx >= 0) t = t.Substring(0, newlineIdx);
        if (t.Length > 60) t = t.Substring(0, 60);
        var words = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        var maxWords = Math.Min(6, words.Length);
        for (int i = 0; i < maxWords; i++)
        {
            var w = words[i];
            if (w.Length > 0)
                words[i] = char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w.Substring(1) : "");
        }
        var title = string.Join(" ", words, 0, maxWords);
        return title;
    }

    public void RenameSession(ChatSession session, string newName)
    {
        if (session == null) return;
        var name = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return;
        session.SessionName = name;
        OnHistoryChanged?.Invoke();
    }


    /// <summary>
    ///     Clears the current chat session and starts a new one.
    /// </summary>
    public void ClearChat()
    {
        sessionService.ClearChat();
    }

    /// <summary>
    ///     Switches the active chat to the specified session.
    /// </summary>
    /// <param name="session">The chat session to make active.</param>
    public void SwitchToSession(ChatSession session)
    {
        sessionService.SwitchToSession(session);
    }

    /// <summary>
    ///     Deletes a specific chat session from the history.
    /// </summary>
    /// <param name="session">The chat session to delete.</param>
    public void DeleteSession(ChatSession session)
    {
        sessionService.DeleteSession(session);
    }

    /// <summary>
    ///     Deletes all chat sessions from the history.
    /// </summary>
    public void ClearAllHistory()
    {
        sessionService.ClearAllHistory();
    }

    /// <summary>
    ///     Cancels the currently ongoing AI assistant request.
    /// </summary>
    public void CancelCurrentRequest()
    {
        llmService.CancelRequest();
    }
}
