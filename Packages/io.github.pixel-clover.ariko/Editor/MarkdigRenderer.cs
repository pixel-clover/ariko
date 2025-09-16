using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
// Required for GUIUtility

// Required for AssetDatabase

public class MarkdigRenderer
{
    private readonly MarkdownPipeline pipeline;
    private readonly ArikoSettings settings;

    public MarkdigRenderer(ArikoSettings settings)
    {
        pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        this.settings = settings;
    }

    public VisualElement Render(string markdownText)
    {
        var document = Markdown.Parse(markdownText, pipeline);
        var container = new VisualElement();
        container.AddToClassList("markdown-container");

        foreach (var block in document) container.Add(CreateElementForBlock(block));
        return container;
    }

    private VisualElement CreateElementForBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var header = new Label(GetInlineText(heading.Inline));
                header.AddToClassList($"h{heading.Level}");
                return header;
            case ParagraphBlock paragraph:
                // Paragraphs can contain multiple inline elements (text, links, bold, etc.)
                // For simplicity, this example just gets the text.
                // A full implementation would create multiple Labels for different styles.
                var p = new Label(GetInlineText(paragraph.Inline));
                p.enableRichText = true; // For bold, italic, etc.
                p.AddToClassList("markdown-paragraph");
                return p;
            case FencedCodeBlock code:
                return CreateCodeBlock(code.Lines.ToString(), code.Info);
            case ListBlock list:
                var listContainer = new VisualElement();
                listContainer.AddToClassList("list");
                foreach (var item in list)
                    if (item is ListItemBlock listItem)
                        // In Markdig, a ListItemBlock contains other blocks (like ParagraphBlock).
                        // We need to process these nested blocks.
                        foreach (var subBlock in listItem)
                        {
                            var itemElement = CreateElementForBlock(subBlock);
                            // We might want to wrap list item content in another container for styling
                            var listItemContainer = new VisualElement();
                            listItemContainer.AddToClassList("list-item");
                            var bullet = new Label("•");
                            bullet.AddToClassList("list-item-bullet");
                            listItemContainer.Add(bullet);
                            listItemContainer.Add(itemElement);
                            listContainer.Add(listItemContainer);
                        }

                return listContainer;
            default:
                // It's better to have a fallback for unhandled block types.
                // Using a simple label to show the type of the unhandled block.
                var fallback = new Label($"Unhandled block type: {block.GetType().Name}");
                fallback.AddToClassList("markdown-fallback");
                return fallback;
        }
    }

    private VisualElement CreateCodeBlock(string content, string language)
    {
        var container = new VisualElement();
        container.AddToClassList("code-block-container");

        var header = new VisualElement();
        header.AddToClassList("code-block-header");

        var langLabel = new Label(language);
        langLabel.AddToClassList("code-block-language");

        var copyButton = new Button(() => GUIUtility.systemCopyBuffer = content.Trim()) { text = "Copy" };
        copyButton.AddToClassList("code-block-copy-button");

        header.Add(langLabel);
        header.Add(copyButton);

        var codeLabel = new Label();
        codeLabel.AddToClassList("code-block-content");

        // --- Syntax Highlighting ---
        var highlightedCode = content.Trim();
        if (!string.IsNullOrEmpty(language))
        {
            // Find the SyntaxTheme asset. For now, we'll just find the first one.
            // A more robust solution would allow theme selection in settings.
            var themeGuids = AssetDatabase.FindAssets("t:SyntaxTheme");
            SyntaxTheme theme = null;
            if (themeGuids.Length > 0)
                theme = AssetDatabase.LoadAssetAtPath<SyntaxTheme>(AssetDatabase.GUIDToAssetPath(themeGuids[0]));

            // Find the LanguageDefinition asset that matches the language name
            var langGuids = AssetDatabase.FindAssets($"t:LanguageDefinition {language}");
            LanguageDefinition langDef = null;
            if (langGuids.Length > 0)
                // To be more specific, we could check the asset name, but for now, the first match is fine.
                langDef = AssetDatabase.LoadAssetAtPath<LanguageDefinition>(
                    AssetDatabase.GUIDToAssetPath(langGuids[0]));

            if (theme != null && langDef != null)
            {
                highlightedCode = SyntaxHighlighter.Highlight(content.Trim(), langDef, theme);
                codeLabel.enableRichText = true;
            }
        }

        codeLabel.text = highlightedCode;
        // -------------------------

        container.Add(header);
        container.Add(codeLabel);
        return container;
    }

    private string GetInlineText(ContainerInline inlines)
    {
        var builder = new StringBuilder();
        if (inlines == null) return "";

        foreach (var inline in inlines)
            if (inline is LiteralInline literal)
            {
                builder.Append(literal.Content.ToString());
            }
            else if (inline is EmphasisInline emphasis) // For *italic* and **bold**
            {
                var tag = emphasis.DelimiterCount == 2 ? "b" : "i";
                builder.Append($"<{tag}>");
                // Recursively get text from children of the emphasis inline
                foreach (var child in emphasis)
                    if (child is LiteralInline childLiteral)
                        builder.Append(childLiteral.Content.ToString());

                builder.Append($"</{tag}>");
            }
            else if (inline is CodeInline code)
            {
                builder.Append($"<code>{code.Content}</code>");
            }
            else if (inline is LinkInline link)
            {
                // For links, we can make them clickable in a real app. Here, we just show the text.
                // A full implementation would create a clickable label or button.
                builder.Append(GetInlineText(link));
            }
            else
            {
                // Fallback for other inline types
                builder.Append(inline);
            }

        return builder.ToString();
    }
}
