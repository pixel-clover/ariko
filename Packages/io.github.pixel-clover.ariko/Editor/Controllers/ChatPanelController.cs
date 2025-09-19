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
    private Label thinkingIndicator;
    private IVisualElementScheduledItem thinkingSchedule;
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
        thinkingIndicator = root.Q<Label>("thinking-indicator");

        emptyStateLabel = new Label(ArikoUIStrings.EmptyState);
        emptyStateLabel.AddToClassList("empty-state-label");
        emptyStateLabel.pickingMode = PickingMode.Ignore;
        emptyStateLabel.style.display = DisplayStyle.None;
        var chatHistoryScroll = root.Q<ScrollView>("chat-history");
        chatHistoryScroll.Add(emptyStateLabel);

        // Suggestion buttons for empty state
        var suggestionsContainer = new VisualElement { name = "empty-suggestions" };
        suggestionsContainer.style.flexDirection = FlexDirection.Row;
        suggestionsContainer.style.justifyContent = Justify.Center;
        suggestionsContainer.style.display = DisplayStyle.None;
        void AddSuggestion(string text)
        {
            var b = new Button(() =>
            {
                userInput.value = text;
                userInput.Focus();
            }) { text = text };
            b.style.marginLeft = 3;
            b.style.marginRight = 3;
            suggestionsContainer.Add(b);
        }
        AddSuggestion("Explain the selected component");
        AddSuggestion("Refactor this script");
        AddSuggestion("Generate unit tests for this class");
        chatHistoryScroll.Add(suggestionsContainer);

        // Drag-and-drop context area setup
        var contextArea = root.Q<VisualElement>("context-area");
        if (contextArea != null)
        {
            contextArea.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                DragAndDrop.visualMode = DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0
                    ? DragAndDropVisualMode.Copy
                    : DragAndDropVisualMode.Rejected;
                evt.StopPropagation();
            });
            contextArea.RegisterCallback<DragPerformEvent>(evt =>
            {
                if (DragAndDrop.objectReferences != null)
                {
                    foreach (var obj in DragAndDrop.objectReferences)
                    {
                        if (obj == null) continue;
                        if (!chatController.ManuallyAttachedAssets.Contains(obj))
                            chatController.ManuallyAttachedAssets.Add(obj);
                    }
                    UpdateManualAttachmentsList();
                }
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
            });
        }

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
        // Also show detailed error inline in the chat UI
        var msg = new ChatMessage { Role = "System", Content = error, IsError = true };
        AddMessageToChat(msg);
        ScrollToBottom();
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

        if (thinkingIndicator != null)
        {
            thinkingIndicator.style.display = isPending ? DisplayStyle.Flex : DisplayStyle.None;
            if (isPending)
            {
                var dots = 0;
                thinkingIndicator.text = "Thinking";
                thinkingSchedule?.Pause();
                thinkingSchedule = thinkingIndicator.schedule.Execute(() =>
                {
                    dots = (dots + 1) % 4;
                    thinkingIndicator.text = "Thinking" + new string('.', dots);
                }).Every(300);
            }
            else
            {
                thinkingSchedule?.Pause();
                thinkingIndicator.text = "Thinking";
            }
        }
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
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.marginTop = 2;
            chip.style.marginBottom = 2;
            chip.style.marginRight = 4;
            chip.style.paddingLeft = 6;
            chip.style.paddingRight = 4;
            chip.style.paddingTop = 2;
            chip.style.paddingBottom = 2;
            chip.style.backgroundColor = new Color(0.2f,0.2f,0.2f,0.5f);
            chip.style.borderTopLeftRadius = 6;
            chip.style.borderTopRightRadius = 6;
            chip.style.borderBottomLeftRadius = 6;
            chip.style.borderBottomRightRadius = 6;

            var icon = new Image();
            var content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
            if (content != null && content.image is Texture tex)
            {
                icon.image = tex;
                icon.scaleMode = ScaleMode.ScaleToFit;
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = 4;
            }
            var nameLabel = new Label(asset.name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
            nameLabel.style.fontSize = 12;
            nameLabel.style.marginRight = 6;

            var removeButton = new Button(() =>
            {
                chatController.ManuallyAttachedAssets.Remove(asset);
                UpdateManualAttachmentsList();
            }) { text = "x" };
            removeButton.style.width = 18;
            removeButton.style.height = 18;

            chip.Add(icon);
            chip.Add(nameLabel);
            chip.Add(removeButton);
            manualAttachmentsList.Add(chip);
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
        var suggestions = root.Q<VisualElement>("empty-suggestions");
        if (suggestions != null)
            suggestions.style.display = hasMessages ? DisplayStyle.None : DisplayStyle.Flex;
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
