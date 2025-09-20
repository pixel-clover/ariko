using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     The main editor window for the Ariko Assistant. This class is responsible for creating the UI,
///     initializing controllers, and handling top-level UI events and state management.
/// </summary>
public class ArikoWindow : EditorWindow
{
    private Button approveButton;
    private VisualElement chatHistory;
    private VisualElement chatPanel;

    private ChatPanelController chatPanelController;

    private VisualElement confirmationDialog;
    private Label confirmationLabel;
    private Button denyButton;

    private Label fetchingModelsLabel;
    private VisualElement footer;
    private Label footerMetadataLabel;
    private VisualElement historyPanel;
    private MarkdigRenderer markdownRenderer;
    private PopupField<string> modelPopup;

    private PopupField<string> providerPopup;
    private ArikoSettings settings;

    private VisualElement splitter;
    private VisualElement verticalSplitter;
    private PopupField<string> workModePopup;
    public ArikoChatController controller { get; private set; }

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
            if (chatPanelController == null || EditorGUIUtility.GetObjectPickerControlID() !=
                chatPanelController.objectPickerControlID) return;

            var selectedObject = EditorGUIUtility.GetObjectPickerObject();
            if (selectedObject != null && !controller.ManuallyAttachedAssets.Contains(selectedObject))
            {
                controller.ManuallyAttachedAssets.Add(selectedObject);
                chatPanelController.UpdateManualAttachmentsList();
            }

            if (Event.current.type != EventType.Layout) Event.current.Use();
        }
    }

    public async void CreateGUI()
    {
        settings = ArikoSettingsManager.LoadSettings();
        controller = new ArikoChatController(settings);
        markdownRenderer = new MarkdigRenderer(settings);

        var visualTree =
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/io.github.pixel-clover.ariko/Editor/EditorWindow/ArikoWindow.uxml");
        var styleSheet =
            AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/io.github.pixel-clover.ariko/Editor/EditorWindow/ArikoWindow.uss");
        rootVisualElement.styleSheets.Add(styleSheet);
        visualTree.CloneTree(rootVisualElement);

        rootVisualElement.AddToClassList(EditorGUIUtility.isProSkin
            ? "unity-editor-dark-theme"
            : "unity-editor-light-theme");

        InitializeQueries();
        SetupUIStrings();
        CreateAndSetupPopups();

        var contentArea = rootVisualElement.Q<VisualElement>("content-area");
        splitter.AddManipulator(new SplitterDragManipulator(contentArea, historyPanel, chatPanel,
            SplitterDragManipulator.Orientation.Horizontal, "Ariko.Splitter.Horizontal"));
        verticalSplitter.AddManipulator(new SplitterDragManipulator(chatPanel, chatHistory, footer,
            SplitterDragManipulator.Orientation.Vertical, "Ariko.Splitter.Vertical"));

        chatPanelController = new ChatPanelController(rootVisualElement, controller, settings, markdownRenderer,
            providerPopup, modelPopup);
        new HistoryPanelController(rootVisualElement, controller);
        chatPanelController.HandleChatReloaded();
        new SettingsPanelController(rootVisualElement, controller, settings,
            chatPanelController.ApplyChatStyles);

        RegisterCallbacks();

        await FetchModelsForCurrentProviderAsync(providerPopup.value);
    }

    [MenuItem("Tools/Ariko Assistant %&a")]
    public static void ShowWindow()
    {
        GetWindow<ArikoWindow>(ArikoUIStrings.WindowTitle);
    }

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
        footerMetadataLabel = rootVisualElement.Q<Label>("footer-metadata");
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
            UpdateFooterMetadata();
        });
        modelPopup.RegisterValueChangedCallback(evt =>
        {
            controller.SetSelectedModelForProvider(providerPopup.value, evt.newValue);
            UpdateFooterMetadata();
        });
        workModePopup.RegisterValueChangedCallback(evt =>
        {
            settings.selectedWorkMode = evt.newValue;
            controller.ReloadToolRegistry(evt.newValue);
            UpdateFooterMetadata();
        });

        UpdateFooterMetadata();
    }

    private void UpdateFooterMetadata()
    {
        if (footerMetadataLabel == null) return;
        var workMode = workModePopup.value;
        var provider = providerPopup.value;
        var model = modelPopup.value ?? "Not selected";
        var unityVersion = Application.unityVersion;
        var arikoVersion = Ariko.ArikoInfo.Version;
        footerMetadataLabel.text =
            $"Work Mode: {workMode} | Model Provider: {provider} | Model: {model} | Unity Version: {unityVersion} | Ariko Version: {arikoVersion}";
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

        var currentModel = controller.GetSelectedModelForProvider(providerPopup.value);
        if (modelPopup.choices.Contains(currentModel))
        {
            modelPopup.SetValueWithoutNotify(currentModel);
        }
        else
        {
            var newModel = modelPopup.choices.FirstOrDefault();
            modelPopup.SetValueWithoutNotify(newModel);
            controller.SetSelectedModelForProvider(providerPopup.value, newModel);
        }
        UpdateFooterMetadata();
    }

    private void HandleError(string error)
    {
        Debug.LogError($"Ariko: {error}");
        var statusLabel = rootVisualElement.Q<Label>("status-label");
        if (statusLabel != null) statusLabel.text = ArikoUIStrings.StatusError;
    }

    private void HandleToolCallConfirmationRequested(ToolCall toolCall)
    {
        if (confirmationLabel != null)
        {
            confirmationLabel.enableRichText = true;
            var paramsText = string.Empty;
            if (toolCall.parameters != null && toolCall.parameters.Count > 0)
                foreach (var kv in toolCall.parameters)
                    paramsText += $"\n - <b>{kv.Key}</b>: {kv.Value}";

            var thought = string.IsNullOrEmpty(toolCall.thought) ? "(no reasoning provided)" : toolCall.thought;
            confirmationLabel.text =
                $"<b>Requested Tool</b>: <b>{toolCall.tool_name}</b>\n\n<b>Parameters</b>:{(string.IsNullOrEmpty(paramsText) ? "\n (none)" : paramsText)}\n\n<b>Reasoning</b>: {thought}\n\nApprove to continue?";
        }

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


    public async void SendExternalMessage(string message)
    {
        if (chatPanelController == null)
        {
            Debug.LogError("Ariko: Chat panel controller is not initialized.");
            return;
        }

        await chatPanelController.SendExternalMessage(message);
    }

    private enum WorkMode
    {
        Ask,
        Agent
    }
}
