using UnityEngine;

/// <summary>
///     A ScriptableObject that defines the colors for syntax highlighting.
/// </summary>
[CreateAssetMenu(fileName = "SyntaxTheme", menuName = "Ariko/Syntax Theme")]
public class SyntaxTheme : ScriptableObject
{
    /// <summary>
    ///     The color for plain text.
    /// </summary>
    public Color PlainTextColor = Color.white;

    /// <summary>
    ///     The color for keywords.
    /// </summary>
    public Color KeywordColor = Color.cyan;

    /// <summary>
    ///     The color for type names.
    /// </summary>
    public Color TypeColor = Color.green;

    /// <summary>
    ///     The color for string literals.
    /// </summary>
    public Color StringColor = Color.yellow;

    /// <summary>
    ///     The color for comments.
    /// </summary>
    public Color CommentColor = Color.gray;

    /// <summary>
    ///     The color for numbers.
    /// </summary>
    public Color NumberColor = Color.magenta;
}
