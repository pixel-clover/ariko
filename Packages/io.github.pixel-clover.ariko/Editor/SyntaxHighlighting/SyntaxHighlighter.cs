using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
///     A simple, regex-based syntax highlighter that uses Unity's rich text tags.
/// </summary>
public static class SyntaxHighlighter
{
    /// <summary>
    ///     Highlights the given code string using the specified language definition and theme.
    ///     It first HTML-escapes the input to prevent unintended rich-text tag parsing,
    ///     then applies regex-based highlighting by inserting <color> tags.
    /// </summary>
    /// <param name="code">The source code to highlight.</param>
    /// <param name="lang">The language definition containing regex patterns and keywords.</param>
    /// <param name="theme">The syntax theme containing the colors.</param>
    /// <returns>The highlighted code with Unity rich text tags.</returns>
    public static string Highlight(string code, LanguageDefinition lang, SyntaxTheme theme)
    {
        if (string.IsNullOrEmpty(code) || lang == null || theme == null) return code ?? string.Empty;

        // HTML-escape to avoid breaking rich text with user code containing '<', '>' or '&'.
        code = HtmlEscape(code);

        // Apply colors using rich text tags
        var keywordColor = ColorUtility.ToHtmlStringRGB(theme.KeywordColor);
        var typeColor = ColorUtility.ToHtmlStringRGB(theme.TypeColor);
        var stringColor = ColorUtility.ToHtmlStringRGB(theme.StringColor);
        var commentColor = ColorUtility.ToHtmlStringRGB(theme.CommentColor);
        var numberColor = ColorUtility.ToHtmlStringRGB(theme.NumberColor);

        var regexOptions = RegexOptions.Multiline | RegexOptions.CultureInvariant;

        // Order of replacement is important
        if (!string.IsNullOrEmpty(lang.StringPattern))
            code = Regex.Replace(code, lang.StringPattern, $"<color=#{stringColor}>$0</color>", regexOptions);

        if (!string.IsNullOrEmpty(lang.CommentPattern))
            code = Regex.Replace(code, lang.CommentPattern, m => $"<color=#{commentColor}>{m.Value}</color>",
                regexOptions);

        if (!string.IsNullOrEmpty(lang.NumberPattern))
            code = Regex.Replace(code, lang.NumberPattern, $"<color=#{numberColor}>$0</color>", regexOptions);

        if (lang.Keywords != null && lang.Keywords.Count > 0)
        {
            var escaped = lang.Keywords.Where(k => !string.IsNullOrEmpty(k)).Select(Regex.Escape);
            var keywordPattern = @"\b(" + string.Join("|", escaped) + @")\b";
            code = Regex.Replace(code, keywordPattern, $"<color=#{keywordColor}>$1</color>", regexOptions);
        }

        if (lang.Types != null && lang.Types.Count > 0)
        {
            var escaped = lang.Types.Where(t => !string.IsNullOrEmpty(t)).Select(Regex.Escape);
            var typePattern = @"\b(" + string.Join("|", escaped) + @")\b";
            code = Regex.Replace(code, typePattern, $"<color=#{typeColor}>$1</color>", regexOptions);
        }

        return code;
    }

    private static string HtmlEscape(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        // Important: replace '&' first to avoid double-escaping
        return text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
