using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class ChatPanelController
{
    private readonly Toggle autoContextToggle;
    private readonly Button cancelButton;
    private readonly ArikoChatController chatController;

    private readonly ScrollView chatHistoryScrollView;
    private readonly Label emptyStateLabel;
    private readonly VisualElement manualAttachmentsList;
    private readonly MarkdigRenderer markdownRenderer;
    private readonly PopupField<string> modelPopup;

    private readonly PopupField<string> providerPopup;
    private readonly VisualElement root;
    private readonly Button sendButton;
    private readonly ArikoSettings settings;
    private readonly Label statusLabel;
    private readonly TextField userInput;
    private VisualElement thinkingMessage;

    // Streaming state
    private VisualElement streamingAssistantElement;
    private VisualElement streamingAssistantContentContainer;
    private System.Text.StringBuilder streamingAssistantText;

    public ChatPanelController(VisualElement root, ArikoChatController controller, ArikoSettings arikosettings,
        MarkdigRenderer renderer, PopupField<string> provider, PopupField<string> model)
    {
        this.root = root;
        chatController = controller;
        settings = arikosettings;
        markdownRenderer = renderer;
        providerPopup = provider;
        modelPopup = model;

        chatHistoryScrollView = root.Q<ScrollView>("chat-history");
        userInput = root.Q<TextField>("user-input");
        sendButton = root.Q<Button>("send-button");
        cancelButton = root.Q<Button>("cancel-button");
        autoContextToggle = root.Q<Toggle>("auto-context-toggle");
        manualAttachmentsList = root.Q<VisualElement>("manual-attachments-list");
        statusLabel = root.Q<Label>("status-label");

        emptyStateLabel = new Label(ArikoUIStrings.EmptyState);
        emptyStateLabel.AddToClassList("empty-state-label");
        emptyStateLabel.pickingMode = PickingMode.Ignore;
        emptyStateLabel.style.display = DisplayStyle.None;
        root.Q<ScrollView>("chat-history").Add(emptyStateLabel);

        RegisterCallbacks();
        UpdateEmptyState();
        UpdateAutoContextLabel();
    }

    public int objectPickerControlID { get; private set; }

    private void RegisterCallbacks()
    {
        chatController.OnMessageAdded += (message, session) => HandleMessageAdded(message, session);
        chatController.OnChatCleared += HandleChatCleared;
        chatController.OnChatReloaded += HandleChatReloaded;
        chatController.OnResponseStatusChanged += SetResponsePending;

        sendButton.clicked += SendMessage;
        cancelButton.clicked += chatController.CancelCurrentRequest;
        userInput.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return && !evt.shiftKey)
            {
                SendMessage();
                evt.StopImmediatePropagation();
            }
        });

        root.Q<Button>("add-file-button").clicked += ShowAttachmentObjectPicker;
        autoContextToggle.RegisterValueChangedCallback(evt => chatController.AutoContext = evt.newValue);
    }

    public async void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(userInput.value)) return;
        var textToSend = userInput.value;
        userInput.value = "";
        await SendMessageInternal(textToSend);
    }

    public async Task SendExternalMessage(string message)
    {
        await SendMessageInternal(message);
    }

    private async Task SendMessageInternal(string text)
    {
        try
        {
            // Create or reuse assistant visual for streaming
            PrepareStreamingAssistantElement();
            streamingAssistantText = new System.Text.StringBuilder();

            await chatController.SendMessageToAssistantStreamed(
                text,
                providerPopup.value,
                modelPopup.value,
                delta =>
                {
                    streamingAssistantText.Append(delta);
                    UpdateStreamingAssistantContent(streamingAssistantText.ToString());
                },
                (success, errorText) =>
                {
                    // Finalization handled in HandleMessageAdded to avoid duplicate elements
                    if (!success && !string.IsNullOrEmpty(errorText))
                    {
                        UpdateStreamingAssistantContent(errorText);
                        streamingAssistantElement.AddToClassList("error-message");
                    }
                    streamingAssistantText = null;
                }
            );
        }
        catch (Exception e)
        {
            Debug.LogError($"Ariko: An unexpected error occurred: {e.Message}");
            HandleError("An unexpected error occurred. See console for details.");
        }
    }

    private void HandleMessageAdded(ChatMessage message, ChatSession session)
    {
        if (session != chatController.ActiveSession) return;

        // If we are currently streaming and the final assistant message arrives, update the existing element
        if (streamingAssistantElement != null && message.Role == "Ariko" && message.Content != "...")
        {
            UpdateStreamingAssistantContent(message.Content);
            // Add copy button now that final content is ready
            var header = streamingAssistantElement.Q<VisualElement>(className: "message-header");
            if (header != null && header.Q<Button>(className: "copy-button") == null)
            {
                var copyButton = new Button(() => EditorGUIUtility.systemCopyBuffer = message.Content)
                {
                    text = ArikoUIStrings.CopyButton
                };
                copyButton.AddToClassList("copy-button");
                header.Add(copyButton);
            }

            streamingAssistantElement = null;
            streamingAssistantContentContainer = null;
            thinkingMessage = null;
            ScrollToBottom();
            UpdateEmptyState();
            return;
        }

        if (thinkingMessage != null && message.Role == "Ariko" && message.Content != "..." &&
            chatHistoryScrollView.Contains(thinkingMessage))
        {
            chatHistoryScrollView.Remove(thinkingMessage);
            thinkingMessage = null;
        }

        var messageElement = AddMessageToChat(message);

        if (message.Role == "Ariko" && message.Content == "...") thinkingMessage = messageElement;

        ScrollToBottom();
        UpdateEmptyState();
    }

    private void HandleChatCleared()
    {
        chatHistoryScrollView.Clear();
        chatController.ManuallyAttachedAssets.Clear();
        UpdateManualAttachmentsList();
        UpdateEmptyState();
    }

    private void HandleChatReloaded()
    {
        chatHistoryScrollView.Clear();
        foreach (var message in chatController.ActiveSession.Messages) AddMessageToChat(message);
        UpdateManualAttachmentsList();
        ScrollToBottom();
        UpdateEmptyState();
    }

    private void HandleError(string error)
    {
        Debug.LogError($"Ariko: {error}");
        SetStatus(ArikoUIStrings.StatusError);
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

        SetStatus(isPending ? ArikoUIStrings.StatusThinking : ArikoUIStrings.StatusReady);
    }

    public void ApplyChatStyles()
    {
        if (settings == null) return;
        chatHistoryScrollView.Query<VisualElement>(className: "chat-message").ForEach(ApplyChatStylesForElement);
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
            roleLabel.style.unityFontStyleAndWeight = settings.roleLabelsBold ? FontStyle.Bold : FontStyle.Normal;
    }

    private void ScrollToBottom()
    {
        chatHistoryScrollView.schedule.Execute(() =>
            chatHistoryScrollView.verticalScroller.value = chatHistoryScrollView.verticalScroller.highValue);
    }

    public void UpdateManualAttachmentsList()
    {
        manualAttachmentsList.Clear();
        foreach (var asset in chatController.ManuallyAttachedAssets)
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var objectField = new ObjectField { value = asset, objectType = typeof(Object) };
            objectField.SetEnabled(false);
            var removeButton = new Button(() =>
            {
                chatController.ManuallyAttachedAssets.Remove(asset);
                UpdateManualAttachmentsList();
            }) { text = "x" };
            container.Add(objectField);
            container.Add(removeButton);
            manualAttachmentsList.Add(container);
        }
    }

    public void UpdateAutoContextLabel()
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

    private void SetStatus(string text)
    {
        if (statusLabel != null) statusLabel.text = text;
    }

    private void UpdateEmptyState()
    {
        var hasMessages = chatController != null &&
                          chatController.ActiveSession != null &&
                          chatController.ActiveSession.Messages.Count > 0;

        emptyStateLabel.style.display = hasMessages ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private static bool IsColorLight(Color color)
    {
        var luminance = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
        return luminance > 0.5;
    }

    private void ShowAttachmentObjectPicker()
    {
        objectPickerControlID = EditorGUIUtility.GetControlID(FocusType.Passive);
        EditorGUIUtility.ShowObjectPicker<Object>(null, true, "t:MonoScript t:TextAsset t:Prefab t:Shader",
            objectPickerControlID);
    }

    // --- Streaming Helpers ---
    private void PrepareStreamingAssistantElement()
    {
        // Reuse thinking bubble if present
        if (thinkingMessage != null && chatHistoryScrollView.Contains(thinkingMessage))
        {
            streamingAssistantElement = thinkingMessage;
            streamingAssistantContentContainer = streamingAssistantElement.Q<VisualElement>("content-container");
            return;
        }

        // Otherwise, create a new assistant message container
        var message = new ChatMessage { Role = "Ariko", Content = "..." };
        var container = new VisualElement();
        container.AddToClassList("chat-message");
        container.AddToClassList("ariko-message");

        var headerContainer = new VisualElement();
        headerContainer.AddToClassList("message-header");
        var roleLabel = new Label(message.Role) { name = "role" };
        roleLabel.AddToClassList("role-label");
        headerContainer.Add(roleLabel);

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(markdownRenderer.Render(message.Content));

        container.Add(headerContainer);
        container.Add(contentContainer);
        chatHistoryScrollView.Add(container);
        ApplyChatStylesForElement(container);

        streamingAssistantElement = container;
        streamingAssistantContentContainer = contentContainer;
        thinkingMessage = container;
        ScrollToBottom();
        UpdateEmptyState();
    }

    private void UpdateStreamingAssistantContent(string fullText)
    {
        if (streamingAssistantElement == null) return;
        if (streamingAssistantContentContainer == null)
            streamingAssistantContentContainer = streamingAssistantElement.Q<VisualElement>("content-container");
        if (streamingAssistantContentContainer == null) return;

        streamingAssistantContentContainer.Clear();
        streamingAssistantContentContainer.Add(markdownRenderer.Render(fullText));
        ApplyChatStylesForElement(streamingAssistantElement);
        ScrollToBottom();
    }
}
