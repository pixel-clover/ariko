using System;
using UnityEditor;
using UnityEngine.UIElements;

public class HistoryPanelController
{
    private const string HistoryPanelVisibleKey = "Ariko.HistoryPanel.Visible";

    private readonly ArikoChatController chatController;
    private readonly VisualElement historyPanel;
    private readonly ScrollView historyListScrollView;

    public HistoryPanelController(VisualElement root, ArikoChatController controller)
    {
        chatController = controller;

        historyPanel = root.Q<VisualElement>("history-panel");
        historyListScrollView = root.Q<ScrollView>("history-list");

        var historyButton = root.Q<Button>("history-button");
        historyButton.clicked += ToggleHistoryPanel;

        historyPanel.style.display = EditorPrefs.GetBool(HistoryPanelVisibleKey, false) ? DisplayStyle.Flex : DisplayStyle.None;

        root.Q<Button>("new-chat-button").clicked += chatController.ClearChat;
        root.Q<Button>("clear-history-button").clicked += chatController.ClearAllHistory;

        chatController.OnHistoryChanged += UpdateHistoryPanel;
        chatController.OnChatReloaded += UpdateHistoryPanel;

        UpdateHistoryPanel();
    }

    private void ToggleHistoryPanel()
    {
        var isVisible = historyPanel.resolvedStyle.display == DisplayStyle.Flex;
        historyPanel.style.display = isVisible ? DisplayStyle.None : DisplayStyle.Flex;
        EditorPrefs.SetBool(HistoryPanelVisibleKey, !isVisible);
    }

    private void UpdateHistoryPanel()
    {
        historyListScrollView.Clear();
        foreach (var session in chatController.ChatHistory)
        {
            var sessionContainer = new VisualElement();
            sessionContainer.AddToClassList("history-item-container");
            if (session == chatController.ActiveSession)
            {
                sessionContainer.AddToClassList("history-item--selected");
            }

            var sessionLabel = new Label(session.SessionName);
            sessionLabel.AddToClassList("history-item-label");
            sessionLabel.RegisterCallback<MouseDownEvent>(evt => chatController.SwitchToSession(session));

            var deleteButton = new Button(() => chatController.DeleteSession(session)) { text = "x" };
            deleteButton.AddToClassList("history-item-delete-button");

            sessionContainer.Add(sessionLabel);
            sessionContainer.Add(deleteButton);
            historyListScrollView.Add(sessionContainer);
        }
    }
}
