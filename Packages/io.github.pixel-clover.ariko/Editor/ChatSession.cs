// Packages/io.github.pixel-clover.ariko/Editor/ChatSession.cs

using System;
using System.Collections.Generic;

public class ChatSession
{
    public ChatSession()
    {
        Messages = new List<ChatMessage>();
        // Simple way to name the session for now. This can be improved later.
        SessionName = $"Chat started at {DateTime.Now:HH:mm:ss}";
    }

    public string SessionName { get; set; }
    public List<ChatMessage> Messages { get; set; }
}
