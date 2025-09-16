using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ArikoChatController
{
    private readonly ArikoLLMService llmService;
    private readonly ArikoSettings settings;
    private readonly Dictionary<string, string> apiKeys = new();

    public List<Object> ManuallyAttachedAssets { get; } = new();
    public bool AutoContext { get; set; } = true;

    // Events for the View to subscribe to
    public Action<string, string> OnMessageAdded; // role, content
    public Action OnChatCleared;
    public Action<bool> OnResponseStatusChanged; // isPending
    public Action<List<string>> OnModelsFetched;
    public Action<string> OnError;

    public ArikoChatController(ArikoSettings settings)
    {
        this.settings = settings;
        llmService = new ArikoLLMService();
        LoadApiKeysFromEnvironment();
    }

    public void LoadApiKeysFromEnvironment()
    {
        apiKeys["Google"] = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
        apiKeys["OpenAI"] = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";

        var ollamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL");
        if (!string.IsNullOrEmpty(ollamaUrl)) settings.ollama_Url = ollamaUrl;
    }

    public string GetApiKey(string provider) => apiKeys.TryGetValue(provider, out var key) ? key : null;
    public void SetApiKey(string provider, string key) => apiKeys[provider] = key;

    public async void SendMessageToAssistant(string text, string selectedProvider, string selectedModel, int chatHistoryCount)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        OnMessageAdded?.Invoke("User", text);

        var context = BuildContextString();
        var systemPrompt = chatHistoryCount == 0 && !string.IsNullOrEmpty(settings.systemPrompt)
            ? settings.systemPrompt + "\n\n"
            : "";
        var prompt = $"{systemPrompt}{context}\n\nUser Question:\n{text}";

        OnResponseStatusChanged?.Invoke(true);
        OnMessageAdded?.Invoke("Ariko", "...");

        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.SendChatRequest(prompt, provider, selectedModel, settings, apiKeys);

        var newContent = result.IsSuccess ? result.Data : result.Error;
        OnMessageAdded?.Invoke("Ariko", newContent);


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

    public void ClearChat()
    {
        ManuallyAttachedAssets.Clear();
        OnChatCleared?.Invoke();
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
