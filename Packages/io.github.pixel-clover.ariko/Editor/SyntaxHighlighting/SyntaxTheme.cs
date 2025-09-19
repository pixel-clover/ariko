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
    public Color PlainTextColor = new(0.82f, 0.82f, 0.84f, 1f); // #D1D1D6

    /// <summary>
    ///     The color for keywords.
    /// </summary>
    public Color KeywordColor = new(0.78f, 0.57f, 0.92f, 1f); // #C792EA

    /// <summary>
    ///     The color for type names.
    /// </summary>
    public Color TypeColor = new(0.51f, 0.67f, 1.00f, 1f); // #82AAFF

    /// <summary>
    ///     The color for string literals.
    /// </summary>
    public Color StringColor = new(0.76f, 0.91f, 0.55f, 1f); // #C3E88D

    /// <summary>
    ///     The color for comments.
    /// </summary>
    public Color CommentColor = new(0.40f, 0.43f, 0.58f, 1f); // #676E95

    /// <summary>
    ///     The color for numbers.
    /// </summary>
    public Color NumberColor = new(0.97f, 0.55f, 0.42f, 1f); // #F78C6C
}
