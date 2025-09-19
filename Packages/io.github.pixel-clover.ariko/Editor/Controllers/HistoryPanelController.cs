using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     Manages the UI and logic for the chat history panel, allowing users to view, switch, rename, and delete past chat
///     sessions.
/// </summary>
public class HistoryPanelController
{
    private const string HistoryPanelVisibleKey = "Ariko.HistoryPanel.Visible";

    private readonly ArikoChatController chatController;
    private readonly ScrollView historyListScrollView;
    private readonly VisualElement historyPanel;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HistoryPanelController" /> class.
    /// </summary>
    /// <param name="root">The root visual element of the history panel.</param>
    /// <param name="controller">The main chat controller.</param>
    public HistoryPanelController(VisualElement root, ArikoChatController controller)
    {
        chatController = controller;

        historyPanel = root.Q<VisualElement>("history-panel");
        historyListScrollView = root.Q<ScrollView>("history-list");

        var historyButton = root.Q<Button>("history-button");
        historyButton.clicked += ToggleHistoryPanel;

        historyPanel.style.display =
            EditorPrefs.GetBool(HistoryPanelVisibleKey, false) ? DisplayStyle.Flex : DisplayStyle.None;

        root.Q<Button>("new-chat-button").clicked += chatController.ClearChat;
        root.Q<Button>("clear-history-button").clicked += chatController.ClearAllHistory;

        chatController.OnHistoryChanged += UpdateHistoryPanel;
        chatController.OnChatReloaded += UpdateHistoryPanel;

        UpdateHistoryPanel();
    }

    /// <summary>
    ///     Toggles the visibility of the history panel.
    /// </summary>
    private void ToggleHistoryPanel()
    {
        var isVisible = historyPanel.resolvedStyle.display == DisplayStyle.Flex;
        historyPanel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        EditorPrefs.SetBool(HistoryPanelVisibleKey, !isVisible);
    }

    /// <summary>
    ///     Updates the history panel with the latest chat sessions.
    /// </summary>
    private void UpdateHistoryPanel()
    {
        historyListScrollView.Clear();
        foreach (var session in chatController.ChatHistory)
        {
            var sessionContainer = new VisualElement();
            sessionContainer.AddToClassList("history-item-container");
            if (session == chatController.ActiveSession) sessionContainer.AddToClassList("history-item--selected");

            var sessionLabel = new Label(session.SessionName);
            sessionLabel.AddToClassList("history-item-label");
            sessionLabel.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    // Start rename
                    var textField = new TextField { value = session.SessionName };
                    textField.AddToClassList("history-item-label");
                    var index = sessionContainer.IndexOf(sessionLabel);
                    sessionContainer.Insert(index, textField);
                    sessionContainer.Remove(sessionLabel);
                    textField.Focus();
                    textField.SelectAll();

                    void Commit()
                    {
                        chatController.RenameSession(session, textField.value);
                        // Replace back with label
                        var newLabel = new Label(session.SessionName);
                        newLabel.AddToClassList("history-item-label");
                        newLabel.RegisterCallback<MouseDownEvent>(_evt => chatController.SwitchToSession(session));
                        var i = sessionContainer.IndexOf(textField);
                        sessionContainer.Insert(i, newLabel);
                        sessionContainer.Remove(textField);
                    }

                    textField.RegisterCallback<KeyDownEvent>(kEvt =>
                    {
                        if (kEvt.keyCode == KeyCode.Return)
                        {
                            Commit();
                            kEvt.StopImmediatePropagation();
                        }
                    });
                    textField.RegisterCallback<FocusOutEvent>(_ => Commit());
                }
                else
                {
                    chatController.SwitchToSession(session);
                }
            });

            var deleteButton = new Button(() => chatController.DeleteSession(session)) { text = "x" };
            deleteButton.AddToClassList("history-item-delete-button");

            sessionContainer.Add(sessionLabel);
            sessionContainer.Add(deleteButton);
            historyListScrollView.Add(sessionContainer);
        }
    }
}
