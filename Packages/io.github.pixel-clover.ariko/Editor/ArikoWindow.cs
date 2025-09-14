using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class ArikoWindow : EditorWindow
{
    private readonly List<Object> manuallyAttachedAssets = new();
    private Toggle autoContextToggle;
    private ScrollView chatHistoryScrollView;
    private Label fetchingModelsLabel;
    private ArikoLLMService llmService;
    private VisualElement manualAttachmentsList;
    private PopupField<string> modelPopup;
    private PopupField<string> providerPopup;
    private Button sendButton;
    private ArikoSettings settings;
    private TextField userInput;
    private PopupField<string> workModePopup;

    private void OnEnable()
    {
        ArikoSearchWindow.OnAssetSelected += HandleAssetSelectedFromSearch;
        Selection.selectionChanged += UpdateAutoContextLabel;
    }

    private void OnDisable()
    {
        ArikoSearchWindow.OnAssetSelected -= HandleAssetSelectedFromSearch;
        Selection.selectionChanged -= UpdateAutoContextLabel;
    }

    public void CreateGUI()
    {
        FindOrCreateSettings();
        llmService = new ArikoLLMService();

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Ariko/Editor/ArikoWindow.uxml");
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Ariko/Editor/ArikoWindow.uss");
        rootVisualElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootVisualElement);

        rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin
            ? "unity-editor-dark-theme"
            : "unity-editor-light-theme");

        InitializeQueries();
        CreateAndSetupPopups();
        RegisterCallbacks();

        settings.LoadApiKeysFromEnvironment();
        FetchModelsForCurrentProvider();

        ApplyChatStyles();

        chatHistoryScrollView.schedule.Execute(() =>
        {
            var firstChild = chatHistoryScrollView.contentContainer.Children().FirstOrDefault();
            if (firstChild != null)
                chatHistoryScrollView.ScrollTo(firstChild);
        });
        userInput.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                if (evt.shiftKey) return;

                SendMessageToAssistant();
                evt.StopImmediatePropagation();
            }
        });
    }

    [MenuItem("Tools/Ariko Assistant")]
    public static void ShowWindow()
    {
        GetWindow<ArikoWindow>("Ariko Assistant");
    }

    private void InitializeQueries()
    {
        chatHistoryScrollView = rootVisualElement.Q<ScrollView>("chat-history");
        userInput = rootVisualElement.Q<TextField>("user-input");
        sendButton = rootVisualElement.Q<Button>("send-button");
        autoContextToggle = rootVisualElement.Q<Toggle>("auto-context-toggle");
        manualAttachmentsList = rootVisualElement.Q<VisualElement>("manual-attachments-list");
        fetchingModelsLabel = rootVisualElement.Q<Label>("fetching-models-label");
    }

    private void CreateAndSetupPopups()
    {
        var providerPlaceholder = rootVisualElement.Q<VisualElement>("provider-popup-placeholder");
        providerPopup = new PopupField<string>(new List<string>(), 0);
        providerPlaceholder.Add(providerPopup);
        providerPopup.choices.AddRange(Enum.GetNames(typeof(ArikoLLMService.AIProvider)).ToList());
        if (providerPopup.choices.Count > 0)
            providerPopup.SetValueWithoutNotify(providerPopup.choices.First());

        var modelPlaceholder = rootVisualElement.Q<VisualElement>("model-popup-placeholder");
        modelPopup = new PopupField<string>(new List<string>(), 0);
        modelPlaceholder.Add(modelPopup);

        var workModePlaceholder = rootVisualElement.Q<VisualElement>("work-mode-popup-placeholder");
        workModePopup = new PopupField<string>(new List<string>(), 0);
        workModePlaceholder.Add(workModePopup);
        workModePopup.choices.AddRange(Enum.GetNames(typeof(WorkMode)).ToList());
        if (workModePopup.choices.Count > 0)
            workModePopup.SetValueWithoutNotify(workModePopup.choices.First());
    }

    private void RegisterCallbacks()
    {
        rootVisualElement.Q<Button>("new-chat-button").clicked += ClearChat;
        rootVisualElement.Q<Button>("settings-button").clicked += ToggleSettingsPanel;
        rootVisualElement.Q<Button>("add-file-button").clicked += ArikoSearchWindow.ShowWindow;
        sendButton.clicked += SendMessageToAssistant;

        providerPopup.RegisterValueChangedCallback(evt => FetchModelsForCurrentProvider());
        modelPopup.RegisterValueChangedCallback(evt => SetSelectedModelForProvider(evt.newValue));

        RegisterSettingsCallbacks();
    }

    private void RegisterSettingsCallbacks()
    {
        var settingsPanel = rootVisualElement.Q<VisualElement>("settings-panel");
        settingsPanel.Q<Button>("save-settings-button").clicked += SaveAndCloseSettings;
        settingsPanel.Q<ColorField>("ariko-bg-color").RegisterValueChangedCallback(evt =>
        {
            settings.assistantChatBackgroundColor = evt.newValue;
            ApplyChatStyles();
        });
        settingsPanel.Q<ColorField>("user-bg-color").RegisterValueChangedCallback(evt =>
        {
            settings.userChatBackgroundColor = evt.newValue;
            ApplyChatStyles();
        });
        settingsPanel.Q<ObjectField>("chat-font").RegisterValueChangedCallback(evt =>
        {
            settings.chatFont = evt.newValue as Font;
            ApplyChatStyles();
        });
        settingsPanel.Q<IntegerField>("chat-font-size").RegisterValueChangedCallback(evt =>
        {
            settings.chatFontSize = evt.newValue;
            ApplyChatStyles();
        });
        settingsPanel.Q<Toggle>("role-bold-toggle").RegisterValueChangedCallback(evt =>
        {
            settings.roleLabelsBold = evt.newValue;
            ApplyChatStyles();
        });
    }

    private void SendMessageToAssistant()
    {
        var text = userInput.value;
        if (string.IsNullOrWhiteSpace(text)) return;

        AddMessageToChat("User", text);
        userInput.value = "";

        var context = BuildContextString();
        var systemPrompt = !chatHistoryScrollView.Children().Any() && !string.IsNullOrEmpty(settings.systemPrompt)
            ? settings.systemPrompt + "\n\n"
            : "";
        var prompt = $"{systemPrompt}{context}\n\nUser Question:\n{text}";

        SetResponsePending(true);
        var thinkingMessage = AddMessageToChat("Ariko", "...");

        var model = GetSelectedModelForProvider();
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerPopup.value);

        llmService.SendChatRequest(prompt, provider, model, settings, result =>
        {
            var contentContainer = thinkingMessage.Q<VisualElement>("content-container");
            contentContainer.Clear();

            var newContent = result.IsSuccess ? MarkdownParser.Parse(result.Data) : MarkdownParser.Parse(result.Error);
            contentContainer.Add(newContent);

            SetResponsePending(false);
            ScrollChatToBottom();
        });
    }

    // csharp
    private VisualElement AddMessageToChat(string role, string content)
    {
        var messageContainer = new VisualElement();
        messageContainer.AddToClassList("chat-message");
        messageContainer.AddToClassList(role == "User" ? "user-message" : "ariko-message");

        // If this is the first message in the chat, override the top margin (USS :first-child isn't supported)
        var isFirstMessage = chatHistoryScrollView != null && chatHistoryScrollView.contentContainer.childCount == 0;
        if (isFirstMessage) messageContainer.style.marginTop = new StyleLength(0f);

        var roleLabel = new Label(role) { name = "role" };
        roleLabel.AddToClassList("role-label");

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(MarkdownParser.Parse(content));

        messageContainer.Add(roleLabel);
        messageContainer.Add(contentContainer);

        StyleChatMessage(messageContainer);

        chatHistoryScrollView.Add(messageContainer);

        ScrollChatToBottom();
        return messageContainer;
    }

    private void StyleChatMessage(VisualElement message)
    {
        if (message == null || settings == null) return;

        if (message.ClassListContains("user-message"))
            message.style.backgroundColor = settings.userChatBackgroundColor;
        else if (message.ClassListContains("ariko-message"))
            message.style.backgroundColor = settings.assistantChatBackgroundColor;

        // Apply font styles to all labels within the message content
        message.Query<Label>().ForEach(label =>
        {
            // Role labels have their own styling, so we exclude them here
            if (label.name != "role")
            {
                if (settings.chatFont != null)
                    label.style.unityFont = settings.chatFont;
                label.style.fontSize = settings.chatFontSize;
            }
        });

        var roleLabel = message.Q<Label>("role");
        if (roleLabel != null)
            roleLabel.style.unityFontStyleAndWeight = settings.roleLabelsBold ? FontStyle.Bold : FontStyle.Normal;
    }

    private void ApplyChatStyles()
    {
        if (chatHistoryScrollView == null) return;
        var messages = chatHistoryScrollView.Query<VisualElement>(className: "chat-message").ToList();
        foreach (var message in messages) StyleChatMessage(message);
    }

    private void ScrollChatToBottom()
    {
        chatHistoryScrollView.schedule.Execute(() =>
            chatHistoryScrollView.verticalScroller.value = chatHistoryScrollView.verticalScroller.highValue);
    }

    private void FetchModelsForCurrentProvider()
    {
        fetchingModelsLabel.style.display = DisplayStyle.Flex;
        modelPopup.SetEnabled(false);
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerPopup.value);

        llmService.FetchAvailableModels(provider, settings, result =>
        {
            modelPopup.choices.Clear();
            modelPopup.choices.AddRange(result.IsSuccess ? result.Data : new List<string> { "Error" });

            var currentModel = GetSelectedModelForProvider();
            if (modelPopup.choices.Contains(currentModel))
            {
                modelPopup.SetValueWithoutNotify(currentModel);
            }
            else
            {
                modelPopup.SetValueWithoutNotify(modelPopup.choices.FirstOrDefault());
                SetSelectedModelForProvider(modelPopup.value);
            }

            fetchingModelsLabel.style.display = DisplayStyle.None;
            modelPopup.SetEnabled(true);
        });
    }

    private void FindOrCreateSettings()
    {
        var settingsPath = "Assets/Ariko/Editor/ArikoSettings.asset";
        settings = AssetDatabase.LoadAssetAtPath<ArikoSettings>(settingsPath);
        if (settings == null)
        {
            settings = CreateInstance<ArikoSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
            AssetDatabase.SaveAssets();
        }
    }

    private void ClearChat()
    {
        chatHistoryScrollView.Clear();
        manuallyAttachedAssets.Clear();
        UpdateManualAttachmentsList();
    }

    private void SetResponsePending(bool isPending)
    {
        sendButton.SetEnabled(!isPending);
        userInput.SetEnabled(!isPending);
    }

    private string BuildContextString()
    {
        var contextBuilder = new StringBuilder();
        if (autoContextToggle.value && Selection.activeObject != null)
        {
            contextBuilder.AppendLine("--- Current Selection Context ---");
            AppendAssetInfo(Selection.activeObject, contextBuilder);
        }

        if (manuallyAttachedAssets.Any())
        {
            contextBuilder.AppendLine("--- Manually Attached Context ---");
            foreach (var asset in manuallyAttachedAssets) AppendAssetInfo(asset, contextBuilder);
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

    private void UpdateAutoContextLabel()
    {
        if (autoContextToggle == null) return;
        var labelText = "Auto-Context from Selection";
        if (Selection.activeObject != null)
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            labelText = string.IsNullOrEmpty(path) ? Selection.activeObject.name : path;
        }

        autoContextToggle.label = labelText;
    }

    private void HandleAssetSelectedFromSearch(Object selectedAsset)
    {
        if (selectedAsset != null && !manuallyAttachedAssets.Contains(selectedAsset))
        {
            manuallyAttachedAssets.Add(selectedAsset);
            UpdateManualAttachmentsList();
        }
    }

    private void UpdateManualAttachmentsList()
    {
        manualAttachmentsList.Clear();
        foreach (var asset in manuallyAttachedAssets)
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var objectField = new ObjectField { value = asset, objectType = typeof(Object) };
            objectField.SetEnabled(false);
            var removeButton = new Button(() =>
            {
                manuallyAttachedAssets.Remove(asset);
                UpdateManualAttachmentsList();
            }) { text = "x" };
            container.Add(objectField);
            container.Add(removeButton);
            manualAttachmentsList.Add(container);
        }
    }

    private string GetSelectedModelForProvider()
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerPopup.value);
        switch (provider)
        {
            case ArikoLLMService.AIProvider.Google: return settings.google_SelectedModel;
            case ArikoLLMService.AIProvider.OpenAI: return settings.openAI_SelectedModel;
            case ArikoLLMService.AIProvider.Ollama: return settings.ollama_SelectedModel;
            default: return null;
        }
    }

    private void SetSelectedModelForProvider(string modelName)
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerPopup.value);
        switch (provider)
        {
            case ArikoLLMService.AIProvider.Google: settings.google_SelectedModel = modelName; break;
            case ArikoLLMService.AIProvider.OpenAI: settings.openAI_SelectedModel = modelName; break;
            case ArikoLLMService.AIProvider.Ollama: settings.ollama_SelectedModel = modelName; break;
        }

        EditorUtility.SetDirty(settings);
    }

    private void ToggleSettingsPanel()
    {
        var settingsPanel = rootVisualElement.Q<VisualElement>("settings-panel");
        var chatPanel = rootVisualElement.Q<VisualElement>("chat-panel");

        // Use resolvedStyle to get the actual runtime visibility (avoids 'Undefined' inline style on first click)
        var settingsVisible = settingsPanel != null && settingsPanel.resolvedStyle.display == DisplayStyle.Flex;

        if (!settingsVisible)
        {
            LoadSettingsToUI();
            settingsPanel.style.display = DisplayStyle.Flex;
            chatPanel.style.display = DisplayStyle.None;
        }
        else
        {
            settingsPanel.style.display = DisplayStyle.None;
            chatPanel.style.display = DisplayStyle.Flex;
        }
    }

    private void LoadSettingsToUI()
    {
        var panel = rootVisualElement.Q<VisualElement>("settings-panel");
        panel.Q<TextField>("google-api-key").value = settings.google_ApiKey;
        panel.Q<TextField>("openai-api-key").value = settings.openAI_ApiKey;
        panel.Q<TextField>("ollama-url").value = settings.ollama_Url;
        panel.Q<ColorField>("ariko-bg-color").value = settings.assistantChatBackgroundColor;
        panel.Q<ColorField>("user-bg-color").value = settings.userChatBackgroundColor;
        panel.Q<TextField>("system-prompt").value = settings.systemPrompt;
        panel.Q<ObjectField>("chat-font").value = settings.chatFont;
        panel.Q<IntegerField>("chat-font-size").value = settings.chatFontSize;
        panel.Q<Toggle>("role-bold-toggle").value = settings.roleLabelsBold;
    }

    private void SaveAndCloseSettings()
    {
        var panel = rootVisualElement.Q<VisualElement>("settings-panel");
        settings.google_ApiKey = panel.Q<TextField>("google-api-key").value;
        settings.openAI_ApiKey = panel.Q<TextField>("openai-api-key").value;
        settings.ollama_Url = panel.Q<TextField>("ollama-url").value;
        settings.systemPrompt = panel.Q<TextField>("system-prompt").value;
        settings.assistantChatBackgroundColor = panel.Q<ColorField>("ariko-bg-color").value;
        settings.userChatBackgroundColor = panel.Q<ColorField>("user-bg-color").value;
        settings.chatFont = panel.Q<ObjectField>("chat-font").value as Font;
        settings.chatFontSize = panel.Q<IntegerField>("chat-font-size").value;
        settings.roleLabelsBold = panel.Q<Toggle>("role-bold-toggle").value;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        ApplyChatStyles();

        ToggleSettingsPanel();
    }

    private enum WorkMode
    {
        Ask,
        Agent
    }
}
