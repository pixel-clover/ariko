using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ArikoChatController
{
    private readonly Dictionary<string, string> apiKeys = new();
    private readonly ArikoLLMService llmService;
    private readonly ArikoSettings settings;
    private readonly ToolRegistry toolRegistry;
    public Action OnChatCleared; // Tells the view to clear the message display
    public Action OnChatReloaded; // Tells the view to reload with messages from ActiveSession

    // --- Events for the View to subscribe to ---
    public Action<string> OnError;
    public Action OnHistoryChanged; // Tells the view to update the history panel
    public Action<ChatMessage> OnMessageAdded; // Replaces the old OnMessageAdded
    public Action<List<string>> OnModelsFetched;
    public Action<bool> OnResponseStatusChanged; // isPending
    public Action<ToolCall> OnToolCallConfirmationRequested;
    private ToolCall pendingToolCall;


    public ArikoChatController(ArikoSettings settings)
    {
        this.settings = settings;
        llmService = new ArikoLLMService();
        toolRegistry = new ToolRegistry();

        ChatHistory = ChatHistoryStorage.LoadHistory();
        if (ChatHistory == null || ChatHistory.Count == 0)
        {
            // If no history, create a new one
            ChatHistory = new List<ChatSession>();
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
    public List<ChatSession> ChatHistory { get; }
    public ChatSession ActiveSession { get; private set; }

    public List<Object> ManuallyAttachedAssets { get; } = new();
    public bool AutoContext { get; set; } = true;

    public void LoadApiKeysFromEnvironment()
    {
        apiKeys["Google"] = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
        apiKeys["OpenAI"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL");
        if (!string.IsNullOrEmpty(ollamaUrl)) settings.ollama_Url = ollamaUrl;
    }

    public string GetApiKey(string provider)
    {
        return apiKeys.TryGetValue(provider, out var key) ? key : null;
    }

    public void SetApiKey(string provider, string key)
    {
        apiKeys[provider] = key;
    }

    public async Task SendMessageToAssistant(string text, string selectedProvider, string selectedModel)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Add user message to session and notify view
        var userMessage = new ChatMessage { Role = "User", Content = text };
        ActiveSession.Messages.Add(userMessage);
        OnMessageAdded?.Invoke(userMessage);

        if (settings.selectedWorkMode == "Agent")
        {
            await SendAgentRequest(text, selectedProvider, selectedModel);
        }
        else
        {
            var context = BuildContextString();
            var systemPrompt = ActiveSession.Messages.Count <= 1 && !string.IsNullOrEmpty(settings.systemPrompt)
                ? settings.systemPrompt + "\n\n"
                : "";
            var prompt = $"{systemPrompt}{context}\n\nUser Question:\n{text}";

            OnResponseStatusChanged?.Invoke(true);

            var thinkingMessage = new ChatMessage { Role = "Ariko", Content = "..." };
            ActiveSession.Messages.Add(thinkingMessage);
            OnMessageAdded?.Invoke(thinkingMessage);

            var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
            var result = await llmService.SendChatRequest(prompt, provider, selectedModel, settings, apiKeys);

            string newContent;
            if (result.IsSuccess)
                newContent = result.Data;
            else
                newContent = GetFormattedErrorMessage(result.Error, result.ErrorType);

            ActiveSession.Messages.Remove(thinkingMessage);
            var finalMessage = new ChatMessage { Role = "Ariko", Content = newContent, IsError = !result.IsSuccess };
            ActiveSession.Messages.Add(finalMessage);
            OnMessageAdded?.Invoke(finalMessage);

            OnResponseStatusChanged?.Invoke(false);
        }
    }

    private async Task SendAgentRequest(string text, string selectedProvider, string selectedModel)
    {
        OnResponseStatusChanged?.Invoke(true);

        var thinkingMessage = new ChatMessage { Role = "Ariko", Content = "..." };
        ActiveSession.Messages.Add(thinkingMessage);
        OnMessageAdded?.Invoke(thinkingMessage);

        var toolDefinitions = toolRegistry.GetToolDefinitionsForPrompt();
        var systemPrompt = settings.agentSystemPrompt;
        var prompt = $"{systemPrompt}\n\n{toolDefinitions}\n\nUser Request:\n{text}";

        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.SendChatRequest(prompt, provider, selectedModel, settings, apiKeys);

        ActiveSession.Messages.Remove(thinkingMessage);
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

    public async void RespondToToolConfirmation(bool userApproved, string selectedProvider, string selectedModel)
    {
        if (pendingToolCall == null) return;

        var toolCall = pendingToolCall;
        pendingToolCall = null; // Clear the pending call

        if (userApproved)
        {
            var thinkingMessage = new ChatMessage
                { Role = "Ariko", Content = $"Executing tool: {toolCall.tool_name}..." };
            ActiveSession.Messages.Add(thinkingMessage);
            OnMessageAdded?.Invoke(thinkingMessage);

            var tool = toolRegistry.GetTool(toolCall.tool_name);
            string executionResult;
            if (tool != null)
                executionResult = tool.Execute(toolCall.parameters);
            else
                executionResult = $"Error: Tool '{toolCall.tool_name}' not found.";

            ActiveSession.Messages.Remove(thinkingMessage);
            var resultMessage = new ChatMessage { Role = "User", Content = $"Observation: {executionResult}" };
            ActiveSession.Messages.Add(resultMessage);
            OnMessageAdded?.Invoke(resultMessage);

            // Send the observation back to the LLM to continue the loop
            await SendAgentRequest($"Observation: {executionResult}", selectedProvider, selectedModel);
        }
        else
        {
            var denialMessage = new ChatMessage { Role = "User", Content = "Observation: User denied the action." };
            ActiveSession.Messages.Add(denialMessage);
            OnMessageAdded?.Invoke(denialMessage);
            // Inform the LLM that the user denied the action
            await SendAgentRequest("Observation: User denied the action.", selectedProvider, selectedModel);
        }
    }

    private bool TryParseToolCall(string response, out ToolCall toolCall)
    {
        toolCall = null;
        var jsonMatch = Regex.Match(response, @"\{.*\}", RegexOptions.Singleline);
        if (!jsonMatch.Success) return false;

        var json = jsonMatch.Value;

        try
        {
            // Simple manual parsing. Not robust, but avoids a full JSON library dependency.
            var tempCall = new ToolCall();

            var thoughtMatch = Regex.Match(json, @"""thought""\s*:\s*""([^""]*)""");
            if (thoughtMatch.Success) tempCall.thought = thoughtMatch.Groups[1].Value;

            var toolNameMatch = Regex.Match(json, @"""tool_name""\s*:\s*""([^""]*)""");
            if (!toolNameMatch.Success) return false; // tool_name is mandatory
            tempCall.tool_name = toolNameMatch.Groups[1].Value;

            var paramsMatch = Regex.Match(json, @"""parameters""\s*:\s*\{([^\}]*)\}");
            if (paramsMatch.Success)
            {
                tempCall.parameters = new Dictionary<string, object>();
                var paramBody = paramsMatch.Groups[1].Value;
                var paramPairs = Regex.Matches(paramBody, @"""([^""]+)""\s*:\s*([^,]+)");
                foreach (Match pair in paramPairs)
                {
                    var key = pair.Groups[1].Value;
                    var valueStr = pair.Groups[2].Value.Trim();

                    // Try to parse value as different types
                    if (valueStr.StartsWith("\"") && valueStr.EndsWith("\""))
                        tempCall.parameters[key] = valueStr.Substring(1, valueStr.Length - 2);
                    else if (int.TryParse(valueStr, out var intVal))
                        tempCall.parameters[key] = intVal;
                    else if (float.TryParse(valueStr, out var floatVal))
                        tempCall.parameters[key] = floatVal;
                    else if (bool.TryParse(valueStr, out var boolVal))
                        tempCall.parameters[key] = boolVal;
                    else
                        tempCall.parameters[key] = valueStr; // as string
                }
            }
            else
            {
                tempCall.parameters = new Dictionary<string, object>(); // empty params
            }

            toolCall = tempCall;
            return true;
        }
        catch
        {
            return false;
        }
    }

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


    // Called when the "New Chat" button is clicked
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

    public void SwitchToSession(ChatSession session)
    {
        if (session == null || session == ActiveSession) return;

        ActiveSession = session;
        ManuallyAttachedAssets.Clear(); // Context is per-session for now
        OnChatReloaded?.Invoke();
        OnHistoryChanged?.Invoke(); // To update selection highlight
    }

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

    public void ClearAllHistory()
    {
        ChatHistory.Clear();
        // After clearing everything, create a new empty session to start with
        ClearChat();
    }

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
