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
    public ArikoChatController controller { get; private set; }
    private ArikoSettings settings;
    private MarkdigRenderer markdownRenderer;

    private ChatPanelController chatPanelController;
    private HistoryPanelController historyPanelController;
    private SettingsPanelController settingsPanelController;

    private PopupField<string> providerPopup;
    private PopupField<string> modelPopup;
    private PopupField<string> workModePopup;

    private Label fetchingModelsLabel;

    private VisualElement confirmationDialog;
    private Label confirmationLabel;
    private Button approveButton;
    private Button denyButton;

    [MenuItem("Tools/Ariko Assistant %&a")]
    public static void ShowWindow()
    {
        GetWindow<ArikoWindow>(ArikoUIStrings.WindowTitle);
    }

    private void OnEnable()
    {
        Selection.selectionChanged += () => chatPanelController?.UpdateAutoContextLabel();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= () => chatPanelController?.UpdateAutoContextLabel();

        if (controller != null)
        {
            ChatHistoryStorage.SaveHistory(controller.ChatHistory);
            UnregisterControllerCallbacks();
        }
    }

    private void OnGUI()
    {
        if (Event.current.commandName == "ObjectSelectorClosed")
        {
            if (chatPanelController == null || EditorGUIUtility.GetObjectPickerControlID() != chatPanelController.objectPickerControlID)
            {
                return;
            }

            var selectedObject = EditorGUIUtility.GetObjectPickerObject();
            if (selectedObject != null)
            {
                controller.ManuallyAttachedAssets.Add(selectedObject);
                chatPanelController.UpdateManualAttachmentsList();
            }
            Event.current.Use();
        }
    }

    public async void CreateGUI()
    {
        settings = ArikoSettingsManager.LoadSettings();
        controller = new ArikoChatController(settings);
        markdownRenderer = new MarkdigRenderer(settings);

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/io.github.pixel-clover.ariko/Editor/ArikoWindow.uxml");
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/io.github.pixel-clover.ariko/Editor/ArikoWindow.uss");
        rootVisualElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootVisualElement);

        rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin ? "unity-editor-dark-theme" : "unity-editor-light-theme");

        InitializeQueries();
        SetupUIStrings();
        CreateAndSetupPopups();

        var contentArea = rootVisualElement.Q<VisualElement>("content-area");
        splitter.AddManipulator(new SplitterDragManipulator(contentArea, historyPanel, chatPanel));
        verticalSplitter.AddManipulator(new VerticalSplitterDragManipulator(chatPanel, chatHistory, footer));

        chatPanelController = new ChatPanelController(rootVisualElement, controller, settings, markdownRenderer, providerPopup, modelPopup);
        historyPanelController = new HistoryPanelController(rootVisualElement, controller);
        settingsPanelController = new SettingsPanelController(rootVisualElement, controller, settings, chatPanelController.ApplyChatStyles);

        RegisterCallbacks();

        await FetchModelsForCurrentProviderAsync(providerPopup.value);
    }

    private VisualElement splitter;
    private VisualElement historyPanel;
    private VisualElement chatPanel;
    private VisualElement verticalSplitter;
    private VisualElement chatHistory;
    private VisualElement footer;

    private void InitializeQueries()
    {
        fetchingModelsLabel = rootVisualElement.Q<Label>("fetching-models-label");

        splitter = rootVisualElement.Q<VisualElement>("splitter");
        historyPanel = rootVisualElement.Q<VisualElement>("history-panel");
        chatPanel = rootVisualElement.Q<VisualElement>("chat-panel");

        verticalSplitter = rootVisualElement.Q<VisualElement>("vertical-splitter");
        chatHistory = rootVisualElement.Q<VisualElement>("chat-history");
        footer = rootVisualElement.Q<VisualElement>("footer");

        confirmationDialog = rootVisualElement.Q<VisualElement>("confirmation-dialog");
        confirmationLabel = rootVisualElement.Q<Label>("confirmation-label");
        approveButton = rootVisualElement.Q<Button>("approve-button");
        denyButton = rootVisualElement.Q<Button>("deny-button");
    }

    private void SetupUIStrings()
    {
        rootVisualElement.Q<Button>("history-button").tooltip = ArikoUIStrings.TipHistory;
        rootVisualElement.Q<Button>("new-chat-button").tooltip = ArikoUIStrings.TipNewChat;
        rootVisualElement.Q<Button>("settings-button").tooltip = ArikoUIStrings.TipSettings;
        rootVisualElement.Q<Button>("clear-history-button").tooltip = ArikoUIStrings.TipClearAll;
        rootVisualElement.Q<Button>("add-file-button").tooltip = ArikoUIStrings.TipAddFile;
        rootVisualElement.Q<Button>("send-button").tooltip = ArikoUIStrings.TipSend;
        rootVisualElement.Q<Button>("cancel-button").tooltip = ArikoUIStrings.TipCancel;
        rootVisualElement.Q<TextField>("user-input").tooltip = ArikoUIStrings.TipInput;
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
        controller.OnModelsFetched += HandleModelsFetched;
        controller.OnError += HandleError;
        controller.OnToolCallConfirmationRequested += HandleToolCallConfirmationRequested;

        approveButton.clicked += () =>
        {
            controller.RespondToToolConfirmation(true, providerPopup.value, modelPopup.value);
            confirmationDialog.style.display = DisplayStyle.None;
            rootVisualElement.Q<TextField>("user-input").SetEnabled(true);
        };
        denyButton.clicked += () =>
        {
            controller.RespondToToolConfirmation(false, providerPopup.value, modelPopup.value);
            confirmationDialog.style.display = DisplayStyle.None;
            rootVisualElement.Q<TextField>("user-input").SetEnabled(true);
        };

        providerPopup.RegisterValueChangedCallback(async evt =>
        {
            settings.selectedProvider = evt.newValue;
            await FetchModelsForCurrentProviderAsync(evt.newValue);
        });
        modelPopup.RegisterValueChangedCallback(evt => SetSelectedModelForProvider(evt.newValue));
        workModePopup.RegisterValueChangedCallback(evt =>
        {
            settings.selectedWorkMode = evt.newValue;
            controller.ReloadToolRegistry(evt.newValue);
        });
    }

    private void UnregisterControllerCallbacks()
    {
        controller.OnModelsFetched -= HandleModelsFetched;
        controller.OnError -= HandleError;
        controller.OnToolCallConfirmationRequested -= HandleToolCallConfirmationRequested;
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
        var statusLabel = rootVisualElement.Q<Label>("status-label");
        if (statusLabel != null)
        {
            statusLabel.text = ArikoUIStrings.StatusError;
        }
    }

    private void HandleToolCallConfirmationRequested(ToolCall toolCall)
    {
        confirmationLabel.text = $"Thought: {toolCall.thought}\nAction: {toolCall.tool_name}";
        confirmationDialog.style.display = DisplayStyle.Flex;
        rootVisualElement.Q<TextField>("user-input").SetEnabled(false);
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

    private enum WorkMode
    {
        Ask,
        Agent
    }

    public async void SendExternalMessage(string message)
    {
        if (chatPanelController == null)
        {
            Debug.LogError("Ariko: Chat panel controller is not initialized.");
            return;
        }
        await chatPanelController.SendExternalMessage(message);
    }
}
