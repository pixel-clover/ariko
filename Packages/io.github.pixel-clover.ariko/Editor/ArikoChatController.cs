using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ArikoChatController
{
    private readonly Dictionary<string, string> apiKeys = new();
    private readonly ArikoLLMService llmService;
    private readonly ArikoSettings settings;

    // --- Properties for Chat History ---
    public List<ChatSession> ChatHistory { get; private set; }
    public ChatSession ActiveSession { get; private set; }

    // --- Events for the View to subscribe to ---
    public Action<string> OnError;
    public Action<List<string>> OnModelsFetched;
    public Action<bool> OnResponseStatusChanged; // isPending
    public Action OnChatCleared; // Tells the view to clear the message display
    public Action<ChatMessage> OnMessageAdded; // Replaces the old OnMessageAdded
    public Action OnChatReloaded; // Tells the view to reload with messages from ActiveSession
    public Action OnHistoryChanged; // Tells the view to update the history panel

    public ArikoChatController(ArikoSettings settings)
    {
        this.settings = settings;
        llmService = new ArikoLLMService();
        ChatHistory = new List<ChatSession>();

        // Start with a clean session
        ActiveSession = new ChatSession();
        ChatHistory.Add(ActiveSession);

        LoadApiKeysFromEnvironment();
    }

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

    public async void SendMessageToAssistant(string text, string selectedProvider, string selectedModel)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // 1. Add user message to session and notify view
        var userMessage = new ChatMessage { Role = "User", Content = text };
        ActiveSession.Messages.Add(userMessage);
        OnMessageAdded?.Invoke(userMessage);

        var context = BuildContextString();
        var systemPrompt = ActiveSession.Messages.Count <= 1 && !string.IsNullOrEmpty(settings.systemPrompt)
            ? settings.systemPrompt + "\n\n"
            : "";
        var prompt = $"{systemPrompt}{context}\n\nUser Question:\n{text}";

        OnResponseStatusChanged?.Invoke(true);

        // 2. Add "thinking" message and notify view
        var thinkingMessage = new ChatMessage { Role = "Ariko", Content = "..." };
        ActiveSession.Messages.Add(thinkingMessage);
        OnMessageAdded?.Invoke(thinkingMessage);

        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.SendChatRequest(prompt, provider, selectedModel, settings, apiKeys);

        var newContent = result.IsSuccess ? result.Data : result.Error;

        // 3. Replace "thinking" message in session with final message, and notify view
        // The view will see the "thinking" message arrive, then the final one. It should handle replacing the visual.
        ActiveSession.Messages.Remove(thinkingMessage);
        var finalMessage = new ChatMessage { Role = "Ariko", Content = newContent };
        ActiveSession.Messages.Add(finalMessage);
        OnMessageAdded?.Invoke(finalMessage);

        OnResponseStatusChanged?.Invoke(false);
    }

    public async void FetchModelsForCurrentProvider(string selectedProvider)
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.FetchAvailableModels(provider, settings, apiKeys);
        if (result.IsSuccess)
        {
            OnModelsFetched?.Invoke(result.Data);
        }
        else
        {
            OnError?.Invoke(result.Error);
            OnModelsFetched?.Invoke(new List<string> { "Error" });
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
        {
            ChatHistory.RemoveAt(ChatHistory.Count - 1);
        }

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
