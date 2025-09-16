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
    private ArikoChatController controller;
    private ArikoSettings settings;

    // UI Elements
    private ScrollView chatHistoryScrollView;
    private TextField userInput;
    private Button sendButton;
    private Toggle autoContextToggle;
    private VisualElement manualAttachmentsList;
    private Label fetchingModelsLabel;
    private PopupField<string> providerPopup;
    private PopupField<string> modelPopup;
    private PopupField<string> workModePopup;

    private VisualElement thinkingMessage;

    [MenuItem("Tools/Ariko Assistant")]
    public static void ShowWindow()
    {
        GetWindow<ArikoWindow>("Ariko Assistant");
    }

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
            controller.OnMessageAdded -= HandleMessageAdded;
            controller.OnChatCleared -= HandleChatCleared;
            controller.OnResponseStatusChanged -= SetResponsePending;
            controller.OnModelsFetched -= HandleModelsFetched;
            controller.OnError -= HandleError;
        }
    }

    public void CreateGUI()
    {
        settings = ArikoSettingsManager.LoadSettings();
        controller = new ArikoChatController(settings);

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/io.github.pixel-clover.ariko/Editor/ArikoWindow.uxml");
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/io.github.pixel-clover.ariko/Editor/ArikoWindow.uss");
        rootVisualElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootVisualElement);

        rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin ? "unity-editor-dark-theme" : "unity-editor-light-theme");

        InitializeQueries();
        CreateAndSetupPopups();
        RegisterCallbacks();

        controller.FetchModelsForCurrentProvider(providerPopup.value);

        ApplyChatStyles();
        ScrollToBottom();
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
        controller.OnResponseStatusChanged += SetResponsePending;
        controller.OnModelsFetched += HandleModelsFetched;
        controller.OnError += HandleError;

        // View -> Controller
        sendButton.clicked += () => {
            controller.SendMessageToAssistant(userInput.value, providerPopup.value, modelPopup.value, chatHistoryScrollView.childCount);
            userInput.value = "";
        };

        userInput.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                controller.SendMessageToAssistant(userInput.value, providerPopup.value, modelPopup.value, chatHistoryScrollView.childCount);
                userInput.value = "";
                evt.StopImmediatePropagation();
            }
        });

        rootVisualElement.Q<Button>("new-chat-button").clicked += controller.ClearChat;
        rootVisualElement.Q<Button>("add-file-button").clicked += ArikoSearchWindow.ShowWindow;
        autoContextToggle.RegisterValueChangedCallback(evt => controller.AutoContext = evt.newValue);

        providerPopup.RegisterValueChangedCallback(evt => {
            settings.selectedProvider = evt.newValue;
            controller.FetchModelsForCurrentProvider(evt.newValue);
        });
        modelPopup.RegisterValueChangedCallback(evt => SetSelectedModelForProvider(evt.newValue));
        workModePopup.RegisterValueChangedCallback(evt => settings.selectedWorkMode = evt.newValue);

        // Settings Panel
        rootVisualElement.Q<Button>("settings-button").clicked += ToggleSettingsPanel;
        RegisterSettingsCallbacks();
    }

    private void RegisterSettingsCallbacks()
    {
        var settingsPanel = rootVisualElement.Q<VisualElement>("settings-panel");
        settingsPanel.Q<Button>("save-settings-button").clicked += SaveAndCloseSettings;

        settingsPanel.Q<ColorField>("ariko-bg-color").RegisterValueChangedCallback(evt => {
            settings.assistantChatBackgroundColor = evt.newValue;
            ApplyChatStyles();
        });
        settingsPanel.Q<ColorField>("user-bg-color").RegisterValueChangedCallback(evt => {
            settings.userChatBackgroundColor = evt.newValue;
            ApplyChatStyles();
        });
        settingsPanel.Q<ObjectField>("chat-font").RegisterValueChangedCallback(evt => {
            settings.chatFont = evt.newValue as Font;
            ApplyChatStyles();
        });
        settingsPanel.Q<IntegerField>("chat-font-size").RegisterValueChangedCallback(evt => {
            settings.chatFontSize = evt.newValue;
            ApplyChatStyles();
        });
        settingsPanel.Q<Toggle>("role-bold-toggle").RegisterValueChangedCallback(evt => {
            settings.roleLabelsBold = evt.newValue;
            ApplyChatStyles();
        });
    }

    private void HandleMessageAdded(string role, string content)
    {
        // If this is the start of a response, remove the "thinking..." message
        if (thinkingMessage != null && role == "Ariko" && content != "...")
        {
            chatHistoryScrollView.Remove(thinkingMessage);
            thinkingMessage = null;
        }

        var message = AddMessageToChat(role, content);

        // If this is a "thinking..." message, store it for later removal
        if (role == "Ariko" && content == "...")
        {
            thinkingMessage = message;
        }

        ScrollToBottom();
    }

    private void HandleChatCleared()
    {
        chatHistoryScrollView.Clear();
        controller.ManuallyAttachedAssets.Clear();
        UpdateManualAttachmentsList();
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
        // Optionally, show a dialog or add an error message to the chat
        AddMessageToChat("System", $"Error: {error}");
    }

    private VisualElement AddMessageToChat(string role, string content)
    {
        var messageContainer = new VisualElement();
        messageContainer.AddToClassList("chat-message");
        messageContainer.AddToClassList(role.ToLower() + "-message");

        var isFirstMessage = chatHistoryScrollView.contentContainer.childCount == 0;
        if (isFirstMessage) messageContainer.style.marginTop = new StyleLength(0f);

        var roleLabel = new Label(role) { name = "role" };
        roleLabel.AddToClassList("role-label");

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(MarkdownParser.Parse(content));

        messageContainer.Add(roleLabel);
        messageContainer.Add(contentContainer);

        chatHistoryScrollView.Add(messageContainer);
        return messageContainer;
    }

    private void SetResponsePending(bool isPending)
    {
        sendButton.SetEnabled(!isPending);
        userInput.SetEnabled(!isPending);
        if (!isPending)
        {
            fetchingModelsLabel.style.display = DisplayStyle.None;
        }
    }

    private void ApplyChatStyles()
    {
        if (settings == null) return;

        // Because SetCustomProperty is not available in all Unity versions, we apply styles directly.
        // This is a compromise to ensure compatibility. The logic is centralized here.
        rootVisualElement.Query<VisualElement>(className: "chat-message").ForEach(message =>
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
        });
    }

    private void ScrollToBottom()
    {
        chatHistoryScrollView.schedule.Execute(() => chatHistoryScrollView.verticalScroller.value = chatHistoryScrollView.verticalScroller.highValue);
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
            var removeButton = new Button(() => {
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
        var settingsVisible = settingsPanel.resolvedStyle.display == DisplayStyle.Flex;

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
        panel.Q<TextField>("google-api-key").value = controller.GetApiKey("Google");
        panel.Q<TextField>("openai-api-key").value = controller.GetApiKey("OpenAI");
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
        controller.SetApiKey("Google", panel.Q<TextField>("google-api-key").value);
        controller.SetApiKey("OpenAI", panel.Q<TextField>("openai-api-key").value);
        settings.ollama_Url = panel.Q<TextField>("ollama-url").value;
        settings.systemPrompt = panel.Q<TextField>("system-prompt").value;
        settings.assistantChatBackgroundColor = panel.Q<ColorField>("ariko-bg-color").value;
        settings.userChatBackgroundColor = panel.Q<ColorField>("user-bg-color").value;
        settings.chatFont = panel.Q<ObjectField>("chat-font").value as Font;
        settings.chatFontSize = panel.Q<IntegerField>("chat-font-size").value;
        settings.roleLabelsBold = panel.Q<Toggle>("role-bold-toggle").value;

        ArikoSettingsManager.SaveSettings(settings);
        ApplyChatStyles();
        ToggleSettingsPanel();
    }

    private enum WorkMode { Ask, Agent }
}
