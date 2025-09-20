using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>
///     Manages the UI and logic for the main chat panel, including user input, message display, and context management.
/// </summary>
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
    private readonly VisualElement statusIndicator;
    private readonly Label statusLabel;
    private readonly TextField userInput;
    private VisualElement streamingAssistantContentContainer;

    // Streaming state
    private VisualElement streamingAssistantElement;
    private StringBuilder streamingAssistantText;
    private VisualElement thinkingMessage;
    private IVisualElementScheduledItem thinkingSchedule;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatPanelController" /> class.
    /// </summary>
    /// <param name="root">The root visual element of the chat panel.</param>
    /// <param name="controller">The main chat controller.</param>
    /// <param name="arikosettings">The settings for Ariko.</param>
    /// <param name="renderer">The markdown renderer.</param>
    /// <param name="provider">The provider popup field.</param>
    /// <param name="model">The model popup field.</param>
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
        statusIndicator = root.Q<VisualElement>("status-indicator");

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
        SetResponsePending(false);
    }

    /// <summary>
    ///     Gets the control ID for the object picker.
    /// </summary>
    public int objectPickerControlID { get; private set; }

    /// <summary>
    ///     Registers all the necessary callbacks for the chat panel UI.
    /// </summary>
    private void RegisterCallbacks()
    {
        chatController.OnMessageAdded += (message, session) => HandleMessageAdded(message, session);
        chatController.OnChatCleared += HandleChatCleared;
        chatController.OnChatReloaded += HandleChatReloaded;
        chatController.OnError += HandleError;
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
        // Focus input on mouse hover
        userInput.RegisterCallback<MouseEnterEvent>(_ => userInput.Focus());

        // Placeholder hint label
        var placeholderLabel = new Label("Edit files in your project in agent mode");
        placeholderLabel.AddToClassList("input-placeholder");
        placeholderLabel.pickingMode = PickingMode.Ignore;
        userInput.Add(placeholderLabel);

        void UpdatePlaceholderVisibility()
        {
            var empty = string.IsNullOrEmpty(userInput.value);
            var focused = userInput.focusController?.focusedElement == userInput ||
                          userInput.focusController?.focusedElement ==
                          userInput.Q(className: "unity-text-field__input");
            placeholderLabel.style.display = empty && !focused ? DisplayStyle.Flex : DisplayStyle.None;
        }

        userInput.RegisterValueChangedCallback(_ => UpdatePlaceholderVisibility());
        userInput.RegisterCallback<FocusInEvent>(_ => UpdatePlaceholderVisibility());
        userInput.RegisterCallback<FocusOutEvent>(_ => UpdatePlaceholderVisibility());
        UpdatePlaceholderVisibility();

        root.Q<Button>("add-file-button").clicked += ShowAttachmentObjectPicker;
        autoContextToggle.RegisterValueChangedCallback(evt => chatController.AutoContext = evt.newValue);
    }

    /// <summary>
    ///     Sends the message from the user input field to the chat controller.
    /// </summary>
    public async void SendMessage()
    {
        var raw = userInput.value ?? string.Empty;
        var trimmed = raw.TrimEnd();
        if (string.IsNullOrWhiteSpace(trimmed)) return;
        var textToSend = trimmed;
        userInput.value = "";
        await SendMessageInternal(textToSend);
    }

    /// <summary>
    ///     Sends a message from an external source to the chat controller.
    /// </summary>
    /// <param name="message">The message to send.</param>
    public async Task SendExternalMessage(string message)
    {
        await SendMessageInternal(message);
    }

    /// <summary>
    ///     Internal method to send a message to the assistant and handle the response.
    /// </summary>
    /// <param name="text">The message text to send.</param>
    private async Task SendMessageInternal(string text)
    {
        try
        {
            // Create or reuse assistant visual for streaming
            PrepareStreamingAssistantElement();
            streamingAssistantText = new StringBuilder();

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

    /// <summary>
    ///     Handles the event when a new message is added to the chat.
    /// </summary>
    /// <param name="message">The new chat message.</param>
    /// <param name="session">The session the message was added to.</param>
    private void HandleMessageAdded(ChatMessage message, ChatSession session)
    {
        if (session != chatController.ActiveSession) return;

        // If we are currently streaming and the final assistant message arrives, update the existing element
        if (streamingAssistantElement != null && message.Role == "Ariko" && message.Content != "...")
        {
            UpdateStreamingAssistantContent(message.Content);
            // Add copy button now that final content is ready
            var header = streamingAssistantElement.Q<VisualElement>(className: "message-header");
            var rightActions = streamingAssistantElement.Q<VisualElement>("message-header-right");
            if (header != null && rightActions != null && rightActions.Q<Button>(className: "copy-button") == null)
            {
                var copyButton = new Button(() => EditorGUIUtility.systemCopyBuffer = message.Content)
                {
                    text = ArikoUIStrings.CopyButton
                };
                copyButton.AddToClassList("copy-button");
                rightActions.Add(copyButton);
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

    /// <summary>
    ///     Handles the event when the chat is cleared.
    /// </summary>
    private void HandleChatCleared()
    {
        chatHistoryScrollView.Clear();
        chatController.ManuallyAttachedAssets.Clear();
        UpdateManualAttachmentsList();
        UpdateEmptyState();
    }

    /// <summary>
    ///     Handles the event when a chat session is reloaded.
    /// </summary>
    public void HandleChatReloaded()
    {
        chatHistoryScrollView.Clear();
        foreach (var message in chatController.ActiveSession.Messages) AddMessageToChat(message);
        UpdateManualAttachmentsList();
        ScrollToBottom();
        UpdateEmptyState();
    }

    /// <summary>
    ///     Handles an error by logging it and displaying it in the chat.
    /// </summary>
    /// <param name="error">The error message.</param>
    private void HandleError(string error)
    {
        Debug.LogError($"Ariko: {error}");
        statusLabel.text = ArikoUIStrings.StatusError;
        statusLabel.style.display = DisplayStyle.Flex;
        // Also show detailed error inline in the chat UI
        var msg = new ChatMessage { Role = "System", Content = error, IsError = true };
        AddMessageToChat(msg);
        ScrollToBottom();
    }

    /// <summary>
    ///     Adds a chat message to the chat history UI.
    /// </summary>
    /// <param name="message">The chat message to add.</param>
    /// <returns>The created visual element for the message.</returns>
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

        var roleText = message.Role == "User" ? GetUnityUserFirstName() : message.Role;
        var roleLabel = new Label(roleText) { name = "role" };
        roleLabel.AddToClassList("role-label");
        headerContainer.Add(roleLabel);

        // Right side actions (timestamp and optional copy)
        var rightActions = new VisualElement { name = "message-header-right" };
        rightActions.AddToClassList("message-header-right");
        var timestamp = new Label(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        timestamp.AddToClassList("timestamp-label");
        rightActions.Add(timestamp);

        if (message.Role == "Ariko" && message.Content != "...")
        {
            var copyButton = new Button(() => EditorGUIUtility.systemCopyBuffer = message.Content)
            {
                text = ArikoUIStrings.CopyButton
            };
            copyButton.AddToClassList("copy-button");
            rightActions.Add(copyButton);
        }

        headerContainer.Add(rightActions);

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(markdownRenderer.Render(message.Content));

        messageContainer.Add(headerContainer);
        // No separator between role and text per request
        messageContainer.Add(contentContainer);

        chatHistoryScrollView.Add(messageContainer);
        ApplyChatStylesForElement(messageContainer);
        ScrollToBottom();
        return messageContainer;
    }

    /// <summary>
    ///     Sets the UI state to pending a response from the assistant.
    /// </summary>
    /// <param name="isPending">True if a response is pending, false otherwise.</param>
    private void SetResponsePending(bool isPending)
    {
        userInput.SetEnabled(!isPending);

        sendButton.style.display = isPending ? DisplayStyle.None : DisplayStyle.Flex;
        cancelButton.style.display = isPending ? DisplayStyle.Flex : DisplayStyle.None;

        if (statusIndicator != null)
        {
            statusIndicator.EnableInClassList("thinking", isPending);
            statusIndicator.EnableInClassList("ready", !isPending);
        }

        // Keep the text label for errors or other statuses
        statusLabel.text = isPending ? "Thinking" : "Ready";
        // The label is hidden by default via USS, but we can show it for important statuses like errors.
        statusLabel.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    ///     Applies the current chat styles to all messages in the chat history.
    /// </summary>
    public void ApplyChatStyles()
    {
        if (settings == null) return;
        chatHistoryScrollView.Query<VisualElement>(className: "chat-message").ForEach(ApplyChatStylesForElement);
    }

    /// <summary>
    ///     Applies the chat styles to a single message element.
    /// </summary>
    /// <param name="message">The message visual element.</param>
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

    /// <summary>
    ///     Scrolls the chat history to the bottom.
    /// </summary>
    private void ScrollToBottom()
    {
        chatHistoryScrollView.schedule.Execute(() =>
            chatHistoryScrollView.verticalScroller.value = chatHistoryScrollView.verticalScroller.highValue);
    }

    /// <summary>
    ///     Updates the list of manually attached assets in the UI.
    /// </summary>
    public void UpdateManualAttachmentsList()
    {
        manualAttachmentsList.Clear();
        foreach (var asset in chatController.ManuallyAttachedAssets)
        {
            var chip = new VisualElement();
            chip.AddToClassList("attachment-chip");

            var icon = new Image();
            var content = EditorGUIUtility.ObjectContent(asset, asset.GetType());
            if (content != null && content.image is Texture tex)
            {
                icon.image = tex;
                icon.scaleMode = ScaleMode.ScaleToFit;
                icon.AddToClassList("attachment-chip__icon");
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
            removeButton.AddToClassList("attachment-chip__remove");

            chip.Add(icon);
            chip.Add(nameLabel);
            chip.Add(removeButton);
            manualAttachmentsList.Add(chip);
        }
    }

    /// <summary>
    ///     Updates the label for the auto-context toggle to reflect the current selection.
    /// </summary>
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

    /// <summary>
    ///     Sets the status text in the UI.
    /// </summary>
    /// <summary>
    ///     Updates the empty state label and suggestion buttons based on whether there are messages.
    /// </summary>
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

    /// <summary>
    ///     Determines if a color is light or dark to decide on text color.
    /// </summary>
    /// <param name="color">The color to check.</param>
    /// <returns>True if the color is light, false otherwise.</returns>
    private static bool IsColorLight(Color color)
    {
        var luminance = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
        return luminance > 0.5;
    }

    /// <summary>
    ///     Shows the object picker to attach an asset to the chat context.
    /// </summary>
    private void ShowAttachmentObjectPicker()
    {
        objectPickerControlID = EditorGUIUtility.GetControlID(FocusType.Passive);
        EditorGUIUtility.ShowObjectPicker<Object>(null, true, "t:MonoScript t:TextAsset t:Prefab t:Shader",
            objectPickerControlID);
    }

    private static string GetUnityUserFirstName()
    {
        try
        {
            // Try to access UnityEditor internal user info via reflection
            var asm = typeof(EditorApplication).Assembly;
            var connectType = asm.GetType("UnityEditor.Connect.UnityConnect");
            string name = null;
            if (connectType != null)
            {
                var instanceProp = connectType.GetProperty("instance",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                var instance = instanceProp?.GetValue(null);
                var userInfoProp = connectType.GetProperty("userInfo",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var userInfo = userInfoProp?.GetValue(instance);
                if (userInfo != null)
                {
                    var uiType = userInfo.GetType();
                    var displayNameProp = uiType.GetProperty("displayName",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var userNameProp = uiType.GetProperty("userName",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    name = displayNameProp?.GetValue(userInfo) as string;
                    if (string.IsNullOrWhiteSpace(name)) name = userNameProp?.GetValue(userInfo) as string;
                }
            }

            if (string.IsNullOrWhiteSpace(name)) name = Environment.UserName;
            var parts = name?.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts != null && parts.Length > 0 ? parts[0] : name;
        }
        catch
        {
            return Environment.UserName;
        }
    }

    // --- Streaming Helpers ---
    /// <summary>
    ///     Prepares the visual element for the assistant's streaming response.
    /// </summary>
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

        var rightActions = new VisualElement { name = "message-header-right" };
        rightActions.AddToClassList("message-header-right");
        var timestamp = new Label(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        timestamp.AddToClassList("timestamp-label");
        rightActions.Add(timestamp);
        headerContainer.Add(rightActions);

        var contentContainer = new VisualElement { name = "content-container" };
        contentContainer.Add(markdownRenderer.Render(message.Content));

        container.Add(headerContainer);
        // No separator between role and text per request
        container.Add(contentContainer);
        chatHistoryScrollView.Add(container);
        ApplyChatStylesForElement(container);

        streamingAssistantElement = container;
        streamingAssistantContentContainer = contentContainer;
        thinkingMessage = container;
        ScrollToBottom();
        UpdateEmptyState();
    }

    /// <summary>
    ///     Updates the content of the streaming assistant's visual element.
    /// </summary>
    /// <param name="fullText">The full text to display.</param>
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
