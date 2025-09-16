using System.Text.RegularExpressions;
using UnityEngine;

public static class SyntaxHighlighter
{
    public static string Highlight(string code, LanguageDefinition lang, SyntaxTheme theme)
    {
        if (lang == null || theme == null) return code;

        // Apply colors using rich text tags
        var keywordColor = ColorUtility.ToHtmlStringRGB(theme.KeywordColor);
        var typeColor = ColorUtility.ToHtmlStringRGB(theme.TypeColor);
        var stringColor = ColorUtility.ToHtmlStringRGB(theme.StringColor);
        var commentColor = ColorUtility.ToHtmlStringRGB(theme.CommentColor);
        var numberColor = ColorUtility.ToHtmlStringRGB(theme.NumberColor);

        // Order of replacement is important
        code = Regex.Replace(code, lang.StringPattern, $"<color=#{stringColor}>$0</color>");
        code = Regex.Replace(code, lang.CommentPattern, m => $"<color=#{commentColor}>{m.Value}</color>",
            RegexOptions.Multiline);
        code = Regex.Replace(code, lang.NumberPattern, $"<color=#{numberColor}>$0</color>");

        if (lang.Keywords.Count > 0)
        {
            var keywordPattern = "\\b(" + string.Join("|", lang.Keywords) + ")\\b";
            code = Regex.Replace(code, keywordPattern, $"<color=#{keywordColor}>$1</color>");
        }

        if (lang.Types.Count > 0)
        {
            var typePattern = "\\b(" + string.Join("|", lang.Types) + ")\\b";
            code = Regex.Replace(code, typePattern, $"<color=#{typeColor}>$1</color>");
        }

        return code;
    }
}
