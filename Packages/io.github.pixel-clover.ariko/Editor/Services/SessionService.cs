using System;
using System.Collections.Generic;
using System.Linq;

public class SessionService
{
    private readonly ArikoSettings settings;

    public Action OnChatCleared;
    public Action OnChatReloaded;
    public Action OnHistoryChanged;

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
    }

    public List<ChatSession> ChatHistory { get; }
    public ChatSession ActiveSession { get; private set; }

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

    public void SwitchToSession(ChatSession session)
    {
        if (session == null || session == ActiveSession) return;

        ActiveSession = session;
        OnChatReloaded?.Invoke();
        OnHistoryChanged?.Invoke();
    }

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

    public void ClearAllHistory()
    {
        ChatHistory.Clear();
        ClearChat();
    }
}
