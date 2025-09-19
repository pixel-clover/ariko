using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
///     Manages the UI and logic for the settings panel, allowing users to configure API keys, appearance, and other
///     preferences.
/// </summary>
public class SettingsPanelController
{
    private readonly Action applyChatStyles;
    private readonly ArikoChatController chatController;
    private readonly VisualElement chatPanel;
    private readonly VisualElement historyPanel;
    private readonly ArikoSettings settings;
    private readonly VisualElement settingsPanel;

    private bool settingsLayoutBuilt = false;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SettingsPanelController" /> class.
    /// </summary>
    /// <param name="root">The root visual element of the settings panel.</param>
    /// <param name="controller">The main chat controller.</param>
    /// <param name="settings">The settings for Ariko.</param>
    /// <param name="applyChatStylesCallback">A callback to apply chat styles.</param>
    public SettingsPanelController(VisualElement root, ArikoChatController controller, ArikoSettings settings,
        Action applyChatStylesCallback)
    {
        chatController = controller;
        this.settings = settings;
        applyChatStyles = applyChatStylesCallback;

        settingsPanel = root.Q<VisualElement>("settings-panel");
        chatPanel = root.Q<VisualElement>("chat-panel");
        historyPanel = root.Q<VisualElement>("history-panel");

        root.Q<Button>("settings-button").clicked += ToggleSettingsPanel;
        settingsPanel.Q<Button>("save-settings-button").clicked += SaveAndCloseSettings;

        RegisterSettingsCallbacks();
    }

    /// <summary>
    ///     Registers callbacks for the settings UI elements.
    /// </summary>
    private void RegisterSettingsCallbacks()
    {
        settingsPanel.Q<ColorField>("ariko-bg-color").RegisterValueChangedCallback(evt =>
        {
            settings.assistantChatBackgroundColor = evt.newValue;
            applyChatStyles?.Invoke();
        });
        settingsPanel.Q<ColorField>("user-bg-color").RegisterValueChangedCallback(evt =>
        {
            settings.userChatBackgroundColor = evt.newValue;
            applyChatStyles?.Invoke();
        });
        settingsPanel.Q<ObjectField>("chat-font").RegisterValueChangedCallback(evt =>
        {
            settings.chatFont = evt.newValue as Font;
            applyChatStyles?.Invoke();
        });
        settingsPanel.Q<IntegerField>("chat-font-size").RegisterValueChangedCallback(evt =>
        {
            settings.chatFontSize = evt.newValue;
            applyChatStyles?.Invoke();
        });
        settingsPanel.Q<Toggle>("role-bold-toggle").RegisterValueChangedCallback(evt =>
        {
            settings.roleLabelsBold = evt.newValue;
            applyChatStyles?.Invoke();
        });
        settingsPanel.Q<Toggle>("enable-delete-tools-toggle").RegisterValueChangedCallback(evt =>
        {
            settings.enableDeleteTools = evt.newValue;
        });
    }

    /// <summary>
    ///     Toggles the visibility of the settings panel.
    /// </summary>
    private void ToggleSettingsPanel()
    {
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

    /// <summary>
    ///     Loads the current settings into the UI fields.
    /// </summary>
    private void LoadSettingsToUI()
    {
        settingsPanel.Q<TextField>("google-api-key").value = chatController.GetApiKey("Google");
        settingsPanel.Q<TextField>("openai-api-key").value = chatController.GetApiKey("OpenAI");
        settingsPanel.Q<TextField>("ollama-url").value = settings.ollama_Url;
        settingsPanel.Q<ColorField>("ariko-bg-color").value = settings.assistantChatBackgroundColor;
        settingsPanel.Q<ColorField>("user-bg-color").value = settings.userChatBackgroundColor;
        settingsPanel.Q<TextField>("system-prompt").value = settings.systemPrompt;
        settingsPanel.Q<TextField>("agent-system-prompt").value = settings.agentSystemPrompt;
        settingsPanel.Q<ObjectField>("chat-font").value = settings.chatFont;
        settingsPanel.Q<IntegerField>("chat-font-size").value = settings.chatFontSize;
        settingsPanel.Q<IntegerField>("chat-history-size").value = settings.chatHistorySize;
        settingsPanel.Q<Toggle>("role-bold-toggle").value = settings.roleLabelsBold;
        settingsPanel.Q<Toggle>("enable-delete-tools-toggle").value = settings.enableDeleteTools;
    }

    /// <summary>
    ///     Saves the settings from the UI and closes the settings panel.
    /// </summary>
    private void SaveAndCloseSettings()
    {
        chatController.SetApiKey("Google", settingsPanel.Q<TextField>("google-api-key").value);
        chatController.SetApiKey("OpenAI", settingsPanel.Q<TextField>("openai-api-key").value);
        settings.ollama_Url = settingsPanel.Q<TextField>("ollama-url").value;
        settings.systemPrompt = settingsPanel.Q<TextField>("system-prompt").value;
        settings.agentSystemPrompt = settingsPanel.Q<TextField>("agent-system-prompt").value;
        settings.assistantChatBackgroundColor = settingsPanel.Q<ColorField>("ariko-bg-color").value;
        settings.userChatBackgroundColor = settingsPanel.Q<ColorField>("user-bg-color").value;
        settings.chatFont = settingsPanel.Q<ObjectField>("chat-font").value as Font;
        settings.chatFontSize = settingsPanel.Q<IntegerField>("chat-font-size").value;
        settings.chatHistorySize = settingsPanel.Q<IntegerField>("chat-history-size").value;
        settings.roleLabelsBold = settingsPanel.Q<Toggle>("role-bold-toggle").value;
        settings.enableDeleteTools = settingsPanel.Q<Toggle>("enable-delete-tools-toggle").value;

        ArikoSettingsManager.SaveSettings(settings);
        chatController.ReloadToolRegistry(settings.selectedWorkMode);
        applyChatStyles?.Invoke();
        ToggleSettingsPanel();
    }
}
