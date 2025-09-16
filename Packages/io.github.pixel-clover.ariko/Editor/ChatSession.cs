// Packages/io.github.pixel-clover.ariko/Editor/ChatSession.cs

using System;
using System.Collections.Generic;

/// <summary>
///     Represents a single, continuous conversation, containing a list of messages.
/// </summary>
public class ChatSession
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ChatSession" /> class.
    /// </summary>
    public ChatSession()
    {
        Messages = new List<ChatMessage>();
        // Simple way to name the session for now. This can be improved later.
        SessionName = $"Chat started at {DateTime.Now:HH:mm:ss}";
    }

    /// <summary>
    ///     Gets or sets the name of the session, displayed in the history panel.
    /// </summary>
    public string SessionName { get; set; }

    /// <summary>
    ///     Gets or sets the list of messages in this chat session.
    /// </summary>
    public List<ChatMessage> Messages { get; set; }
}
