// Packages/io.github.pixel-clover.ariko/Editor/ChatSession.cs
using System.Collections.Generic;

public class ChatSession
{
    public string SessionName { get; set; }
    public List<ChatMessage> Messages { get; set; }

    public ChatSession()
    {
        Messages = new List<ChatMessage>();
        // Simple way to name the session for now. This can be improved later.
        SessionName = $"Chat started at {System.DateTime.Now:HH:mm:ss}";
    }
}
