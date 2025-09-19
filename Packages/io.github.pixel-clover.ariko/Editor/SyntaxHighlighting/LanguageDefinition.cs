using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A ScriptableObject that defines the syntax rules for a programming language,
///     including keywords, types, and regex patterns for syntax highlighting.
/// </summary>
[CreateAssetMenu(fileName = "LanguageDefinition", menuName = "Ariko/Language Definition")]
public class LanguageDefinition : ScriptableObject
{
    /// <summary>
    ///     A list of keywords for the language (e.g., "if", "for", "while").
    /// </summary>
    public List<string> Keywords = new();

    /// <summary>
    ///     A list of built-in type names for the language (e.g., "int", "string", "bool").
    /// </summary>
    public List<string> Types = new();

    [Header("Regex Patterns")]
    /// <summary>
    /// The regex pattern to identify string literals. Handles escaped quotes within strings.
    /// </summary>
    public string StringPattern = "\"(?:\\\\.|[^\"\\\\])*\"";

    /// <summary>
    ///     The regex pattern to identify single-line comments.
    /// </summary>
    public string CommentPattern = @"//(.*?)$";

    /// <summary>
    ///     The regex pattern to identify numbers.
    /// </summary>
    public string NumberPattern = @"\b\d+(\.\d+)?f?\b";
}
