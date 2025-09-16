using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class ArikoWindow : EditorWindow
{
    private Toggle autoContextToggle;

    // --- UI Elements ---
    private ScrollView chatHistoryScrollView;

    // --- State ---
    private ArikoChatController controller;
    private Label fetchingModelsLabel;
    private ScrollView historyListScrollView;
    private VisualElement manualAttachmentsList;
    private MarkdigRenderer markdownRenderer;
    private PopupField<string> modelPopup;
    private PopupField<string> providerPopup;
    private Button sendButton;
    private ArikoSettings settings;
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

        if (controller != null) UnregisterControllerCallbacks();
    }

    public void CreateGUI()
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
        CreateAndSetupPopups();
        RegisterCallbacks();

        UpdateHistoryPanel(); // Initial population of history panel

        controller.FetchModelsForCurrentProvider(providerPopup.value);

        ApplyChatStyles();
        ScrollToBottom();
    }

    // --- Lifecycle Methods ---
    [MenuItem("Tools/Ariko Assistant")]
    public static void ShowWindow()
    {
        GetWindow<ArikoWindow>("Ariko Assistant");
    }

    // --- Initialization and Callbacks ---

    private void InitializeQueries()
    {
        chatHistoryScrollView = rootVisualElement.Q<ScrollView>("chat-history");
        historyListScrollView = rootVisualElement.Q<ScrollView>("history-list");
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
        // Controller -> View
        controller.OnMessageAdded += HandleMessageAdded;
        controller.OnChatCleared += HandleChatCleared;
        controller.OnChatReloaded += HandleChatReloaded;
        controller.OnHistoryChanged += UpdateHistoryPanel;
        controller.OnResponseStatusChanged += SetResponsePending;
        controller.OnModelsFetched += HandleModelsFetched;
        controller.OnError += HandleError;

        // View -> Controller
        sendButton.clicked += SendMessage;
        userInput.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                SendMessage();
                evt.StopImmediatePropagation();
            }
        });

        rootVisualElement.Q<Button>("new-chat-button").clicked += controller.ClearChat;
        rootVisualElement.Q<Button>("add-file-button").clicked += ArikoSearchWindow.ShowWindow;
        autoContextToggle.RegisterValueChangedCallback(evt => controller.AutoContext = evt.newValue);

        providerPopup.RegisterValueChangedCallback(evt =>
        {
            settings.selectedProvider = evt.newValue;
            controller.FetchModelsForCurrentProvider(evt.newValue);
        });
        modelPopup.RegisterValueChangedCallback(evt => SetSelectedModelForProvider(evt.newValue));
        workModePopup.RegisterValueChangedCallback(evt => settings.selectedWorkMode = evt.newValue);

        // Settings Panel
        rootVisualElement.Q<Button>("settings-button").clicked += ToggleSettingsPanel;
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

    // --- Chat and History Handling ---

    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(userInput.value)) return;
        controller.SendMessageToAssistant(userInput.value, providerPopup.value, modelPopup.value);
        userInput.value = "";
    }

    private void HandleMessageAdded(ChatMessage message)
    {
        // If this is the start of a response, remove the "thinking..." message
        if (thinkingMessage != null && message.Role == "Ariko" && message.Content != "...")
        {
            chatHistoryScrollView.Remove(thinkingMessage);
            thinkingMessage = null;
        }

        var messageElement = AddMessageToChat(message.Role, message.Content);

        // If this is a "thinking..." message, store it for later removal
        if (message.Role == "Ariko" && message.Content == "...") thinkingMessage = messageElement;

        // When a message is added to the active session, its name might change (e.g., if it's based on first message)
        // So we update the history panel to reflect the potential new name.
        UpdateHistoryPanel();

        ScrollToBottom();
    }

    private void HandleChatCleared()
    {
        chatHistoryScrollView.Clear();
        controller.ManuallyAttachedAssets.Clear();
        UpdateManualAttachmentsList();
    }

    private void HandleChatReloaded()
    {
        chatHistoryScrollView.Clear();
        foreach (var message in controller.ActiveSession.Messages) AddMessageToChat(message.Role, message.Content);
        UpdateManualAttachmentsList(); // Assuming attachments might be session-specific
        ScrollToBottom();
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
        AddMessageToChat("System", $"Error: {error}");
    }

    private void UpdateHistoryPanel()
    {
        historyListScrollView.Clear();
        foreach (var session in controller.ChatHistory)
        {
            var sessionLabel = new Label(session.SessionName);
            sessionLabel.AddToClassList("history-item");
            if (session == controller.ActiveSession) sessionLabel.AddToClassList("history-item--selected");
            sessionLabel.RegisterCallback<MouseDownEvent>(evt => controller.SwitchToSession(session));
            historyListScrollView.Add(sessionLabel);
        }
    }

    private VisualElement AddMessageToChat(string role, string content)
    {
        var messageContainer = new VisualElement();
        messageContainer.AddToClassList("chat-message");
        messageContainer.AddToClassList(role.ToLower() + "-message");

        var isFirstMessage = chatHistoryScrollView.contentContainer.childCount == 0;
        if (isFirstMessage) messageContainer.style.marginTop = new StyleLength(0f);

        var headerContainer = new VisualElement();
        headerContainer.AddToClassList("message-header");

        var roleLabel = new Label(role) { name = "role" };
        roleLabel.AddToClassList("role-label");
        headerContainer.Add(roleLabel);

        if (role == "Ariko" && content != "...")
        {
            var copyButton = new Button(() => EditorGUIUtility.systemCopyBuffer = content) { text = "Copy" };
            copyButton.AddToClassList("copy-button");
            headerContainer.Add(copyButton);
        }

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(markdownRenderer.Render(content));

        messageContainer.Add(headerContainer);
        messageContainer.Add(contentContainer);

        chatHistoryScrollView.Add(messageContainer);
        ApplyChatStylesForElement(messageContainer);
        return messageContainer;
    }

    private void SetResponsePending(bool isPending)
    {
        sendButton.SetEnabled(!isPending);
        userInput.SetEnabled(!isPending);
        if (!isPending) fetchingModelsLabel.style.display = DisplayStyle.None;
    }

    private void ApplyChatStyles()
    {
        if (settings == null) return;

        rootVisualElement.Query<VisualElement>(className: "chat-message").ForEach(ApplyChatStylesForElement);
    }

    private void ApplyChatStylesForElement(VisualElement message)
    {
        if (message.ClassListContains("user-message"))
            message.style.backgroundColor = settings.userChatBackgroundColor;
        else if (message.ClassListContains("ariko-message"))
            message.style.backgroundColor = settings.assistantChatBackgroundColor;

        message.Query<Label>().ForEach(label =>
        {
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

    private enum WorkMode
    {
        Ask,
        Agent
    }
}
