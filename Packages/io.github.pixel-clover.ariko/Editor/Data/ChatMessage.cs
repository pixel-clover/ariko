/// <summary>
///     Represents a single message in a chat session.
/// </summary>
public struct ChatMessage
{
    /// <summary>
    ///     Gets or sets the role of the message sender ("User", "Ariko", or "System").
    /// </summary>
    public string Role { get; set; }

    /// <summary>
    ///     Gets or sets the text content of the message.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether this message represents an error.
    /// </summary>
    public bool IsError { get; set; }
}
