using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

public static class MarkdownParser
{
    public static VisualElement Parse(string markdownText)
    {
        var container = new VisualElement();
        var lines = markdownText.Split('\n');
        var inCodeBlock = false;
        var codeBlockContent = "";
        var codeBlockLanguage = "";

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("```"))
            {
                if (!inCodeBlock)
                {
                    inCodeBlock = true;
                    codeBlockLanguage = line.Substring(3).Trim();
                }
                else
                {
                    inCodeBlock = false;
                    container.Add(CreateCodeBlock(codeBlockContent, codeBlockLanguage));
                    codeBlockContent = "";
                    codeBlockLanguage = "";
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeBlockContent += line + "\n";
                continue;
            }

            if (line.StartsWith("# "))
                container.Add(CreateHeader(line.Substring(2), "h1"));
            else if (line.StartsWith("## "))
                container.Add(CreateHeader(line.Substring(3), "h2"));
            else if (line.StartsWith("- "))
                container.Add(CreateListItem(line.Substring(2)));
            else if (!string.IsNullOrWhiteSpace(line)) container.Add(CreateParagraph(line));
        }

        return container;
    }

    private static Label CreateHeader(string text, string styleClass)
    {
        var label = new Label(ApplyInlineFormatting(text));
        label.AddToClassList("markdown-header");
        label.AddToClassList(styleClass);
        label.enableRichText = true;
        label.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
        {
            evt.menu.AppendAction("Copy", action => GUIUtility.systemCopyBuffer = label.text,
                DropdownMenuAction.AlwaysEnabled);
        });
        return label;
    }

    private static VisualElement CreateListItem(string text)
    {
        var itemContainer = new VisualElement();
        itemContainer.AddToClassList("list-item-container");

        var bullet = new Label("•");
        bullet.AddToClassList("list-item-bullet");

        var label = new Label(ApplyInlineFormatting(text));
        label.AddToClassList("list-item-text");
        label.enableRichText = true;
        label.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
        {
            evt.menu.AppendAction("Copy", action => GUIUtility.systemCopyBuffer = label.text,
                DropdownMenuAction.AlwaysEnabled);
        });

        itemContainer.Add(bullet);
        itemContainer.Add(label);
        return itemContainer;
    }

    private static Label CreateParagraph(string text)
    {
        var label = new Label(ApplyInlineFormatting(text));
        label.AddToClassList("markdown-paragraph");
        label.enableRichText = true;
        label.RegisterCallback<ContextualMenuPopulateEvent>(evt =>
        {
            evt.menu.AppendAction("Copy", action => GUIUtility.systemCopyBuffer = label.text,
                DropdownMenuAction.AlwaysEnabled);
        });
        return label;
    }

    private static VisualElement CreateCodeBlock(string content, string language)
    {
        var container = new VisualElement();
        container.AddToClassList("code-block-container");

        var header = new VisualElement();
        header.AddToClassList("code-block-header");

        var langLabel = new Label(language);
        langLabel.AddToClassList("code-block-language");

        var copyButton = new Button(() => GUIUtility.systemCopyBuffer = content.Trim())
        {
            text = "Copy"
        };
        copyButton.AddToClassList("code-block-copy-button");

        header.Add(langLabel);
        header.Add(copyButton);

        var codeLabel = new Label
        {
            text = HighlightSyntax(content.Trim(), language),
            enableRichText = true
        };
        codeLabel.AddToClassList("code-block-content");

        container.Add(header);
        container.Add(codeLabel);

        return container;
    }

    private static string HighlightSyntax(string code, string language)
    {
        if (string.IsNullOrEmpty(code) || language.ToLower() != "csharp") return code;

        const string keywordColor = "#569CD6";
        const string stringColor = "#D69D85";
        const string commentColor = "#6A9955";
        const string typeColor = "#4EC9B0";

        var keywords = new List<string>
        {
            "public", "private", "protected", "internal", "static", "void", "string", "int", "float", "bool",
            "class", "struct", "enum", "interface", "namespace", "using", "return", "if", "else", "while", "for",
            "foreach", "in", "new", "true", "false", "null", "get", "set", "var"
        };

        var types = new List<string>
        {
            "Vector3", "Vector2", "GameObject", "Transform", "MonoBehaviour", "Debug", "Input", "Quaternion", "Color"
        };

        var keywordPattern = "\\b(" + string.Join("|", keywords) + ")\\b";
        code = Regex.Replace(code, keywordPattern, $"<color={keywordColor}>$1</color>");

        var typePattern = "\\b(" + string.Join("|", types) + ")\\b";
        code = Regex.Replace(code, typePattern, $"<color={typeColor}>$1</color>");

        code = Regex.Replace(code, @"""""(.*?)""""", $"<color={stringColor}>\"$1\"</color>");
        code = Regex.Replace(code, @"//(.*?)\n", $"<color={commentColor}>//$1</color>\n");

        return code;
    }

    private static string ApplyInlineFormatting(string text)
    {
        text = Regex.Replace(text, @"\*\*(.*?)\*\*", "<b>$1</b>");
        text = Regex.Replace(text, @"\*(.*?)\*", "<i>$1</i>");
        text = Regex.Replace(text, @"`(.*?)`", "<style=\"font-family: 'monospace';\">$1</style>");
        return text;
    }
}
