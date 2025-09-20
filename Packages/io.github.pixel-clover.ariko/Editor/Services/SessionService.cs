using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Manages the lifecycle of chat sessions, including creating, loading, switching, and deleting them.
/// </summary>
public class SessionService
{
    private readonly ArikoSettings settings;

    /// <summary>
    ///     Event triggered when the current chat session is cleared.
    /// </summary>
    public Action OnChatCleared;

    /// <summary>
    ///     Event triggered when a different chat session is loaded.
    /// </summary>
    public Action OnChatReloaded;

    /// <summary>
    ///     Event triggered when the chat history changes (e.g., a session is added or deleted).
    /// </summary>
    public Action OnHistoryChanged;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SessionService" /> class.
    /// </summary>
    /// <param name="settings">The Ariko settings asset.</param>
    public SessionService(ArikoSettings settings)
    {
        this.settings = settings;
        ChatHistory = ChatHistoryStorage.LoadHistory() ?? new List<ChatSession>();
        if (ChatHistory.Count == 0)
        {
            ActiveSession = new ChatSession();
            ChatHistory.Add(ActiveSession);
        }
        else
        {
            ActiveSession = ChatHistory[0];
        }
        OnChatReloaded?.Invoke();
    }

    /// <summary>
    ///     Gets the list of all chat sessions.
    /// </summary>
    public List<ChatSession> ChatHistory { get; }

    /// <summary>
    ///     Gets the currently active chat session.
    /// </summary>
    public ChatSession ActiveSession { get; private set; }

    /// <summary>
    ///     Clears the current chat session and starts a new one.
    /// </summary>
    public void ClearChat()
    {
        if (ActiveSession != null && ActiveSession.Messages.Count == 0)
        {
            OnChatCleared?.Invoke();
            return;
        }

        ActiveSession = new ChatSession();
        ChatHistory.Insert(0, ActiveSession);

        if (settings.chatHistorySize > 0 && ChatHistory.Count > settings.chatHistorySize)
            ChatHistory.RemoveAt(ChatHistory.Count - 1);

        OnChatCleared?.Invoke();
        OnHistoryChanged?.Invoke();
    }

    /// <summary>
    ///     Switches the active chat to the specified session.
    /// </summary>
    /// <param name="session">The chat session to make active.</param>
    public void SwitchToSession(ChatSession session)
    {
        if (session == null || session == ActiveSession) return;

        ActiveSession = session;
        OnChatReloaded?.Invoke();
        OnHistoryChanged?.Invoke();
    }

    /// <summary>
    ///     Deletes a specific chat session from the history.
    /// </summary>
    /// <param name="session">The chat session to delete.</param>
    public void DeleteSession(ChatSession session)
    {
        if (session == null || !ChatHistory.Contains(session)) return;

        var wasActive = session == ActiveSession;
        ChatHistory.Remove(session);

        if (wasActive)
        {
            if (ChatHistory.Any())
                SwitchToSession(ChatHistory[0]);
            else
                ClearChat();
        }
        else
        {
            OnHistoryChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Deletes all chat sessions from the history.
    /// </summary>
    public void ClearAllHistory()
    {
        ChatHistory.Clear();
        ClearChat();
    }
}
