using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class ArikoWindow : EditorWindow
{
    private Button approveButton;
    private Toggle autoContextToggle;
    private Button cancelButton;
    private ScrollView chatHistoryScrollView;
    private VisualElement confirmationDialog;
    private Label confirmationLabel;
    public ArikoChatController controller;
    private Button denyButton;
    private Label emptyStateLabel;
    private Label fetchingModelsLabel;
    private Button historyButton;
    private ScrollView historyListScrollView;
    private VisualElement manualAttachmentsList;
    private MarkdigRenderer markdownRenderer;
    private PopupField<string> modelPopup;
    private PopupField<string> providerPopup;
    private Button sendButton;
    private ArikoSettings settings;
    private Label statusLabel;
    private VisualElement thinkingMessage;
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

        if (controller != null)
        {
            ChatHistoryStorage.SaveHistory(controller.ChatHistory);
            UnregisterControllerCallbacks();
        }
    }

    public async void CreateGUI()
    {
        settings = ArikoSettingsManager.LoadSettings();
        controller = new ArikoChatController(settings);
        markdownRenderer = new MarkdigRenderer(settings);

        var visualTree =
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/io.github.pixel-clover.ariko/Editor/ArikoWindow.uxml");
        var styleSheet =
            AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/io.github.pixel-clover.ariko/Editor/ArikoWindow.uss");
        rootVisualElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootVisualElement);

        rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin
            ? "unity-editor-dark-theme"
            : "unity-editor-light-theme");

        InitializeQueries();

        if (historyButton != null)
        {
            historyButton.text = ArikoUIStrings.HistoryButton;
            historyButton.tooltip = ArikoUIStrings.TipHistory;
        }

        var newChatBtn = rootVisualElement.Q<Button>("new-chat-button");
        if (newChatBtn != null)
        {
            newChatBtn.text = ArikoUIStrings.NewChatButton;
            newChatBtn.tooltip = ArikoUIStrings.TipNewChat;
        }

        var settingsBtn = rootVisualElement.Q<Button>("settings-button");
        if (settingsBtn != null)
        {
            settingsBtn.text = ArikoUIStrings.SettingsButton;
            settingsBtn.tooltip = ArikoUIStrings.TipSettings;
        }

        var clearAllBtn = rootVisualElement.Q<Button>("clear-history-button");
        if (clearAllBtn != null)
        {
            clearAllBtn.text = ArikoUIStrings.ClearAllButton;
            clearAllBtn.tooltip = ArikoUIStrings.TipClearAll;
        }

        var addFileBtn = rootVisualElement.Q<Button>("add-file-button");
        if (addFileBtn != null)
        {
            addFileBtn.text = ArikoUIStrings.AddFileButton;
            addFileBtn.tooltip = ArikoUIStrings.TipAddFile;
        }

        if (sendButton != null)
        {
            sendButton.text = ArikoUIStrings.SendButton;
            sendButton.tooltip = ArikoUIStrings.TipSend;
        }

        if (cancelButton != null)
        {
            cancelButton.text = ArikoUIStrings.CancelButton;
            cancelButton.tooltip = ArikoUIStrings.TipCancel;
        }

        if (userInput != null)
        {
            userInput.tooltip = ArikoUIStrings.TipInput;
        }

        statusLabel = rootVisualElement.Q<Label>("status-label");
        SetStatus(ArikoUIStrings.StatusReady);

        emptyStateLabel = new Label(ArikoUIStrings.EmptyState);
        emptyStateLabel.AddToClassList("empty-state-label");
        emptyStateLabel.pickingMode = PickingMode.Ignore;
        emptyStateLabel.style.display = DisplayStyle.None;
        rootVisualElement.Q<ScrollView>("chat-history").Add(emptyStateLabel);
        CreateAndSetupPopups();
        RegisterCallbacks();

        UpdateHistoryPanel();

        await FetchModelsForCurrentProviderAsync(providerPopup.value);

        ApplyChatStyles();
        ScrollToBottom();
        UpdateEmptyState();
    }

    [MenuItem("Tools/Ariko Assistant %&a")]
    public static void ShowWindow()
    {
        GetWindow<ArikoWindow>(ArikoUIStrings.WindowTitle);
    }

    public async void SendExternalMessage(string message)
    {
        if (controller == null)
        {
            Debug.LogError("Ariko: Chat controller is not initialized.");
            return;
        }

        var provider = providerPopup.value;
        var model = modelPopup.value;

        try
        {
            await controller.SendMessageToAssistant(message, provider, model);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ariko: An unexpected error occurred while sending external message: {e.Message}");
            HandleError("An unexpected error occurred. See console for details.");
        }
    }

    private void InitializeQueries()
    {
        chatHistoryScrollView = rootVisualElement.Q<ScrollView>("chat-history");
        historyListScrollView = rootVisualElement.Q<ScrollView>("history-list");
        userInput = rootVisualElement.Q<TextField>("user-input");
        sendButton = rootVisualElement.Q<Button>("send-button");
        cancelButton = rootVisualElement.Q<Button>("cancel-button");
        historyButton = rootVisualElement.Q<Button>("history-button");
        autoContextToggle = rootVisualElement.Q<Toggle>("auto-context-toggle");
        manualAttachmentsList = rootVisualElement.Q<VisualElement>("manual-attachments-list");
        fetchingModelsLabel = rootVisualElement.Q<Label>("fetching-models-label");

        confirmationDialog = rootVisualElement.Q<VisualElement>("confirmation-dialog");
        confirmationLabel = rootVisualElement.Q<Label>("confirmation-label");
        approveButton = rootVisualElement.Q<Button>("approve-button");
        denyButton = rootVisualElement.Q<Button>("deny-button");
    }

    private void CreateAndSetupPopups()
    {
        var providerPlaceholder = rootVisualElement.Q<VisualElement>("provider-popup-placeholder");
        providerPopup = new PopupField<string>(new List<string>(), 0);
        providerPlaceholder.Add(providerPopup);
        providerPopup.choices.AddRange(Enum.GetNames(typeof(ArikoLLMService.AIProvider)).ToList());
        providerPopup.SetValueWithoutNotify(settings.selectedProvider ?? providerPopup.choices.First());

        var modelPlaceholder = rootVisualElement.Q<VisualElement>("model-popup-placeholder");
        modelPopup = new PopupField<string>(new List<string>(), 0);
        modelPlaceholder.Add(modelPopup);

        var workModePlaceholder = rootVisualElement.Q<VisualElement>("work-mode-popup-placeholder");
        workModePopup = new PopupField<string>(new List<string>(), 0);
        workModePlaceholder.Add(workModePopup);
        workModePopup.choices.AddRange(Enum.GetNames(typeof(WorkMode)).ToList());
        workModePopup.SetValueWithoutNotify(settings.selectedWorkMode ?? workModePopup.choices.First());
    }

    private void RegisterCallbacks()
    {
        controller.OnMessageAdded += HandleMessageAdded;
        controller.OnChatCleared += HandleChatCleared;
        controller.OnChatReloaded += HandleChatReloaded;
        controller.OnHistoryChanged += UpdateHistoryPanel;
        controller.OnResponseStatusChanged += SetResponsePending;
        controller.OnModelsFetched += HandleModelsFetched;
        controller.OnError += HandleError;
        controller.OnToolCallConfirmationRequested += HandleToolCallConfirmationRequested;

        sendButton.clicked += SendMessage;
        cancelButton.clicked += controller.CancelCurrentRequest;
        userInput.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                SendMessage();
                evt.StopImmediatePropagation();
            }
        });

        approveButton.clicked += () =>
        {
            controller.RespondToToolConfirmation(true, providerPopup.value, modelPopup.value);
            confirmationDialog.style.display = DisplayStyle.None;
            userInput.SetEnabled(true);
        };
        denyButton.clicked += () =>
        {
            controller.RespondToToolConfirmation(false, providerPopup.value, modelPopup.value);
            confirmationDialog.style.display = DisplayStyle.None;
            userInput.SetEnabled(true);
        };

        rootVisualElement.Q<Button>("new-chat-button").clicked += controller.ClearChat;
        rootVisualElement.Q<Button>("clear-history-button").clicked += controller.ClearAllHistory;
        rootVisualElement.Q<Button>("add-file-button").clicked += ArikoSearchWindow.ShowWindow;
        autoContextToggle.RegisterValueChangedCallback(evt => controller.AutoContext = evt.newValue);

        providerPopup.RegisterValueChangedCallback(async evt =>
        {
            settings.selectedProvider = evt.newValue;
            await FetchModelsForCurrentProviderAsync(evt.newValue);
        });
        modelPopup.RegisterValueChangedCallback(evt => SetSelectedModelForProvider(evt.newValue));
        workModePopup.RegisterValueChangedCallback(evt => settings.selectedWorkMode = evt.newValue);

        rootVisualElement.Q<Button>("settings-button").clicked += ToggleSettingsPanel;
        historyButton.clicked += ToggleHistoryPanel;
        RegisterSettingsCallbacks();
    }

    private void UnregisterControllerCallbacks()
    {
        controller.OnMessageAdded -= HandleMessageAdded;
        controller.OnChatCleared -= HandleChatCleared;
        controller.OnChatReloaded -= HandleChatReloaded;
        controller.OnHistoryChanged -= UpdateHistoryPanel;
        controller.OnResponseStatusChanged -= SetResponsePending;
        controller.OnModelsFetched -= HandleModelsFetched;
        controller.OnError -= HandleError;
        controller.OnToolCallConfirmationRequested -= HandleToolCallConfirmationRequested;
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

    private void HandleToolCallConfirmationRequested(ToolCall toolCall)
    {
        confirmationLabel.text = $"Thought: {toolCall.thought}\nAction: {toolCall.tool_name}";
        confirmationDialog.style.display = DisplayStyle.Flex;
        userInput.SetEnabled(false);
    }

    private async void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(userInput.value)) return;
        var textToSend = userInput.value;
        userInput.value = "";
        try
        {
            await controller.SendMessageToAssistant(textToSend, providerPopup.value, modelPopup.value);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ariko: An unexpected error occurred: {e.Message}");
            HandleError("An unexpected error occurred. See console for details.");
        }
    }

    private async Task FetchModelsForCurrentProviderAsync(string provider)
    {
        fetchingModelsLabel.style.display = DisplayStyle.Flex;
        modelPopup.SetEnabled(false);
        try
        {
            await controller.FetchModelsForCurrentProvider(provider);
        }
        catch (Exception e)
        {
            Debug.LogError($"Ariko: An unexpected error occurred while fetching models: {e.Message}");
            HandleError("Failed to fetch models. See console for details.");
        }
        finally
        {
            fetchingModelsLabel.style.display = DisplayStyle.None;
            modelPopup.SetEnabled(true);
        }
    }


    private void HandleMessageAdded(ChatMessage message)
    {
        if (thinkingMessage != null && message.Role == "Ariko" && message.Content != "..." && chatHistoryScrollView.Contains(thinkingMessage))
        {
            chatHistoryScrollView.Remove(thinkingMessage);
            thinkingMessage = null;
        }

        var messageElement = AddMessageToChat(message);

        if (message.Role == "Ariko" && message.Content == "...") thinkingMessage = messageElement;

        UpdateHistoryPanel();
        ScrollToBottom();
        UpdateEmptyState();
    }

    private void HandleChatCleared()
    {
        chatHistoryScrollView.Clear();
        controller.ManuallyAttachedAssets.Clear();
        UpdateManualAttachmentsList();
        UpdateEmptyState();
    }

    private void HandleChatReloaded()
    {
        chatHistoryScrollView.Clear();
        foreach (var message in controller.ActiveSession.Messages) AddMessageToChat(message);
        UpdateManualAttachmentsList();
        ScrollToBottom();
        UpdateEmptyState();
    }

    private void HandleModelsFetched(List<string> models)
    {
        fetchingModelsLabel.style.display = DisplayStyle.None;
        modelPopup.SetEnabled(true);

        modelPopup.choices.Clear();
        modelPopup.choices.AddRange(models);

        var currentModel = GetSelectedModelForProvider();
        if (modelPopup.choices.Contains(currentModel))
        {
            modelPopup.SetValueWithoutNotify(currentModel);
        }
        else
        {
            var newModel = modelPopup.choices.FirstOrDefault();
            modelPopup.SetValueWithoutNotify(newModel);
            SetSelectedModelForProvider(newModel);
        }
    }

    private void HandleError(string error)
    {
        Debug.LogError($"Ariko: {error}");
        AddMessageToChat(new ChatMessage { Role = "System", Content = error, IsError = true });
        SetStatus(ArikoUIStrings.StatusError);
    }

    private void UpdateHistoryPanel()
    {
        historyListScrollView.Clear();
        foreach (var session in controller.ChatHistory)
        {
            var sessionContainer = new VisualElement();
            sessionContainer.AddToClassList("history-item-container");
            if (session == controller.ActiveSession) sessionContainer.AddToClassList("history-item--selected");

            var sessionLabel = new Label(session.SessionName);
            sessionLabel.AddToClassList("history-item-label");
            sessionLabel.RegisterCallback<MouseDownEvent>(evt => controller.SwitchToSession(session));

            var deleteButton = new Button(() => controller.DeleteSession(session)) { text = "x" };
            deleteButton.AddToClassList("history-item-delete-button");

            sessionContainer.Add(sessionLabel);
            sessionContainer.Add(deleteButton);
            historyListScrollView.Add(sessionContainer);
        }
    }

    private VisualElement AddMessageToChat(ChatMessage message)
    {
        var messageContainer = new VisualElement();
        messageContainer.AddToClassList("chat-message");
        messageContainer.AddToClassList(message.Role.ToLower() + "-message");
        if (message.IsError) messageContainer.AddToClassList("error-message");


        var isFirstMessage = chatHistoryScrollView.contentContainer.childCount == 0;
        if (isFirstMessage) messageContainer.style.marginTop = new StyleLength(0f);

        var headerContainer = new VisualElement();
        headerContainer.AddToClassList("message-header");

        var roleLabel = new Label(message.Role) { name = "role" };
        roleLabel.AddToClassList("role-label");
        headerContainer.Add(roleLabel);

        if (message.Role == "Ariko" && message.Content != "...")
        {
            var copyButton = new Button(() => EditorGUIUtility.systemCopyBuffer = message.Content)
            {
                text = ArikoUIStrings.CopyButton
            };
            copyButton.AddToClassList("copy-button");
            headerContainer.Add(copyButton);
        }

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(markdownRenderer.Render(message.Content));

        messageContainer.Add(headerContainer);
        messageContainer.Add(contentContainer);

        chatHistoryScrollView.Add(messageContainer);
        ApplyChatStylesForElement(messageContainer);
        return messageContainer;
    }

    private void SetResponsePending(bool isPending)
    {
        userInput.SetEnabled(!isPending);

        sendButton.style.display = isPending ? DisplayStyle.None : DisplayStyle.Flex;
        cancelButton.style.display = isPending ? DisplayStyle.Flex : DisplayStyle.None;

        if (!isPending) fetchingModelsLabel.style.display = DisplayStyle.None;
        SetStatus(isPending ? ArikoUIStrings.StatusThinking : ArikoUIStrings.StatusReady);
    }

    private void ApplyChatStyles()
    {
        if (settings == null) return;

        rootVisualElement.Query<VisualElement>(className: "chat-message").ForEach(ApplyChatStylesForElement);
    }

    private void ApplyChatStylesForElement(VisualElement message)
    {
        var backgroundColor = Color.clear;
        if (message.ClassListContains("user-message"))
        {
            backgroundColor = settings.userChatBackgroundColor;
            message.style.backgroundColor = backgroundColor;
        }
        else if (message.ClassListContains("ariko-message"))
        {
            backgroundColor = settings.assistantChatBackgroundColor;
            message.style.backgroundColor = backgroundColor;
        }

        var textColor = IsColorLight(backgroundColor) ? Color.black : Color.white;
        message.Query<Label>().ForEach(label =>
        {
            label.style.color = textColor;
            if (label.name != "role")
            {
                if (settings.chatFont != null)
                    label.style.unityFont = settings.chatFont;
                label.style.fontSize = settings.chatFontSize;
            }
        });

        var roleLabel = message.Q<Label>("role");
        if (roleLabel != null)
        {
            roleLabel.style.unityFontStyleAndWeight = settings.roleLabelsBold ? FontStyle.Bold : FontStyle.Normal;
        }
    }

    private void ScrollToBottom()
    {
        chatHistoryScrollView.schedule.Execute(() =>
            chatHistoryScrollView.verticalScroller.value = chatHistoryScrollView.verticalScroller.highValue);
    }

    private void HandleAssetSelectedFromSearch(Object selectedAsset)
    {
        if (selectedAsset != null && !controller.ManuallyAttachedAssets.Contains(selectedAsset))
        {
            controller.ManuallyAttachedAssets.Add(selectedAsset);
            UpdateManualAttachmentsList();
        }
    }

    private void UpdateManualAttachmentsList()
    {
        manualAttachmentsList.Clear();
        foreach (var asset in controller.ManuallyAttachedAssets)
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var objectField = new ObjectField { value = asset, objectType = typeof(Object) };
            objectField.SetEnabled(false);
            var removeButton = new Button(() =>
            {
                controller.ManuallyAttachedAssets.Remove(asset);
                UpdateManualAttachmentsList();
            }) { text = "x" };
            container.Add(objectField);
            container.Add(removeButton);
            manualAttachmentsList.Add(container);
        }
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

    private string GetSelectedModelForProvider()
    {
        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), providerPopup.value);
        return provider switch
        {
            ArikoLLMService.AIProvider.Google => settings.google_SelectedModel,
            ArikoLLMService.AIProvider.OpenAI => settings.openAI_SelectedModel,
            ArikoLLMService.AIProvider.Ollama => settings.ollama_SelectedModel,
            _ => null
        };
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
    }

    private void ToggleHistoryPanel()
    {
        var historyPanel = rootVisualElement.Q<VisualElement>("history-panel");
        var isVisible = historyPanel.resolvedStyle.display == DisplayStyle.Flex;
        historyPanel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void ToggleSettingsPanel()
    {
        var settingsPanel = rootVisualElement.Q<VisualElement>("settings-panel");
        var chatPanel = rootVisualElement.Q<VisualElement>("chat-panel");
        var historyPanel = rootVisualElement.Q<VisualElement>("history-panel");
        var settingsVisible = settingsPanel.resolvedStyle.display == DisplayStyle.Flex;

        if (!settingsVisible)
        {
            LoadSettingsToUI();
            settingsPanel.style.display = DisplayStyle.Flex;
            chatPanel.style.display = DisplayStyle.None;
            historyPanel.style.display = DisplayStyle.None;
        }
        else
        {
            settingsPanel.style.display = DisplayStyle.None;
            chatPanel.style.display = DisplayStyle.Flex;
            historyPanel.style.display = DisplayStyle.Flex;
        }
    }

    private void LoadSettingsToUI()
    {
        var panel = rootVisualElement.Q<VisualElement>("settings-panel");
        panel.Q<TextField>("google-api-key").value = controller.GetApiKey("Google");
        panel.Q<TextField>("openai-api-key").value = controller.GetApiKey("OpenAI");
        panel.Q<TextField>("ollama-url").value = settings.ollama_Url;
        panel.Q<ColorField>("ariko-bg-color").value = settings.assistantChatBackgroundColor;
        panel.Q<ColorField>("user-bg-color").value = settings.userChatBackgroundColor;
        panel.Q<TextField>("system-prompt").value = settings.systemPrompt;
        panel.Q<ObjectField>("chat-font").value = settings.chatFont;
        panel.Q<IntegerField>("chat-font-size").value = settings.chatFontSize;
        panel.Q<IntegerField>("chat-history-size").value = settings.chatHistorySize;
        panel.Q<Toggle>("role-bold-toggle").value = settings.roleLabelsBold;
    }

    private void SaveAndCloseSettings()
    {
        var panel = rootVisualElement.Q<VisualElement>("settings-panel");
        controller.SetApiKey("Google", panel.Q<TextField>("google-api-key").value);
        controller.SetApiKey("OpenAI", panel.Q<TextField>("openai-api-key").value);
        settings.ollama_Url = panel.Q<TextField>("ollama-url").value;
        settings.systemPrompt = panel.Q<TextField>("system-prompt").value;
        settings.assistantChatBackgroundColor = panel.Q<ColorField>("ariko-bg-color").value;
        settings.userChatBackgroundColor = panel.Q<ColorField>("user-bg-color").value;
        settings.chatFont = panel.Q<ObjectField>("chat-font").value as Font;
        settings.chatFontSize = panel.Q<IntegerField>("chat-font-size").value;
        settings.chatHistorySize = panel.Q<IntegerField>("chat-history-size").value;
        settings.roleLabelsBold = panel.Q<Toggle>("role-bold-toggle").value;

        ArikoSettingsManager.SaveSettings(settings);
        ApplyChatStyles();
        ToggleSettingsPanel();
    }

    private void SetStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }

    private void UpdateEmptyState()
    {
        var hasMessages = controller != null &&
                          controller.ActiveSession != null &&
                          controller.ActiveSession.Messages.Count > 0;

        emptyStateLabel.style.display = hasMessages ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private static bool IsColorLight(Color color)
    {
        var luminance = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
        return luminance > 0.5;
    }

    private enum WorkMode
    {
        Ask,
        Agent
    }
}
