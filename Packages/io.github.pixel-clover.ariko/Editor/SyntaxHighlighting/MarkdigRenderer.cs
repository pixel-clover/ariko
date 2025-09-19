using System;
using System.IO;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>
///     Renders a Markdown string into a Unity UI Elements VisualElement hierarchy.
/// </summary>
public class MarkdigRenderer
{
    private readonly MarkdownPipeline pipeline;
    private readonly ArikoSettings settings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MarkdigRenderer" /> class.
    /// </summary>
    /// <param name="settings">The Ariko settings to use for styling.</param>
    public MarkdigRenderer(ArikoSettings settings)
    {
        pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        this.settings = settings;
    }

    /// <summary>
    ///     Renders the specified markdown text into a VisualElement.
    /// </summary>
    /// <param name="markdownText">The markdown text to render.</param>
    /// <returns>A VisualElement containing the rendered markdown.</returns>
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
                header.AddToClassList("unity-text-element__selectable");
                return header;
            case ParagraphBlock paragraph:
                // Paragraphs can contain multiple inline elements (text, links, bold, etc.)
                // For simplicity, this example just gets the text.
                // A full implementation would create multiple Labels for different styles.
                var p = new Label(GetInlineText(paragraph.Inline));
                p.enableRichText = true; // For bold, italic, etc.
                p.AddToClassList("markdown-paragraph");
                p.AddToClassList("unity-text-element__selectable");
                return p;
            case FencedCodeBlock code:
            {
                var codeContent = new StringBuilder();
                foreach (var line in code.Lines.Lines) codeContent.AppendLine(line.ToString());
                return CreateCodeBlock(codeContent.ToString(), code.Info);
            }
            case ListBlock list:
                var listContainer = new VisualElement();
                listContainer.AddToClassList("list");
                var itemNumber = 1;
                foreach (var item in list)
                    if (item is ListItemBlock listItem)
                    {
                        var listItemContainer = new VisualElement();
                        listItemContainer.AddToClassList("list-item");

                        var bullet = new Label(list.IsOrdered ? $"{itemNumber++}." : "•");
                        bullet.AddToClassList("list-item-bullet");
                        listItemContainer.Add(bullet);

                        var contentContainer = new VisualElement();
                        contentContainer.AddToClassList("list-item-content");

                        foreach (var subBlock in listItem)
                        {
                            var itemElement = CreateElementForBlock(subBlock);
                            if (itemElement.ClassListContains("markdown-paragraph")) itemElement.style.marginBottom = 0;
                            contentContainer.Add(itemElement);
                        }

                        listItemContainer.Add(contentContainer);
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
        var trimmed = content.Trim();
        var container = new VisualElement();
        container.AddToClassList("code-block-container");

        var header = new VisualElement();
        header.AddToClassList("code-block-header");

        var langLabel = new Label(string.IsNullOrEmpty(language) ? "code" : language);
        langLabel.AddToClassList("code-block-language");
        langLabel.tooltip = string.IsNullOrEmpty(language) ? "Code block" : $"Language: {language}";

        var copyButton = new Button(() => GUIUtility.systemCopyBuffer = trimmed) { text = " Copy " };
        copyButton.tooltip = "Copy code to clipboard";
        copyButton.AddToClassList("code-block-copy-button");
        copyButton.AddToClassList("code-block-action-button");

        // Save button (save to a file in the project)
        var saveButton = new Button(() => SaveCodeToProject(trimmed, language)) { text = "Save to Project" };
        saveButton.tooltip = "Save this snippet into your project";
        saveButton.AddToClassList("code-block-save-button");
        saveButton.AddToClassList("code-block-action-button");

        header.Add(langLabel);
        // separator between language and actions
        var headerSep = new VisualElement { name = "code-header-separator" };
        headerSep.pickingMode = PickingMode.Ignore;
        headerSep.AddToClassList("toolbar-vertical-separator");
        header.Add(headerSep);
        header.Add(copyButton);
        header.Add(saveButton);

        var codeLabel = new Label();
        codeLabel.AddToClassList("code-block-content");
        codeLabel.AddToClassList("unity-text-element__selectable");
        // Preserve formatting of whitespace and new lines for code
        codeLabel.style.whiteSpace = WhiteSpace.Pre;

        // --- Syntax Highlighting ---
        var highlightedCode = trimmed;
        var appliedHighlight = false;
        if (!string.IsNullOrEmpty(language))
        {
            var theme = settings.syntaxTheme;
            var normalized = (language ?? string.Empty).Trim();
            // basic alias normalization
            var lower = normalized.ToLowerInvariant();
            if (lower is "cs" or "c#" or "csharp") normalized = "csharp";
            else if (lower is "js" or "javascript") normalized = "javascript";
            else if (lower is "ts" or "typescript") normalized = "typescript";
            else if (lower is "py" or "python") normalized = "python";

            var langDef = settings.languageDefinitions?.Find(l =>
                string.Equals(l.name, normalized, StringComparison.OrdinalIgnoreCase));

            if (theme != null && langDef != null)
            {
                highlightedCode = SyntaxHighlighter.Highlight(trimmed, langDef, theme);
                appliedHighlight = true;
            }
        }

        codeLabel.enableRichText = appliedHighlight;
        codeLabel.text = appliedHighlight ? highlightedCode : trimmed;
        // -------------------------

        container.Add(header);
        // visual separator between header and content for clarity
        var sep = new VisualElement();
        sep.AddToClassList("separator");
        container.Add(sep);
        container.Add(codeLabel);
        return container;
    }

    private void InsertIntoSelectedScript(string code)
    {
        var obj = Selection.activeObject;
        if (obj == null)
        {
            EditorUtility.DisplayDialog("Insert Code",
                "No asset is selected. Please select a script or text asset in the Project window.", "OK");
            return;
        }

        var path = AssetDatabase.GetAssetPath(obj);
        if (string.IsNullOrEmpty(path))
        {
            EditorUtility.DisplayDialog("Insert Code", "Selected object is not a valid asset.", "OK");
            return;
        }

        try
        {
            var text = File.Exists(path) ? File.ReadAllText(path) : "";
            var toWrite = text + "\n\n// --- Inserted by Ariko Assistant ---\n" + code + "\n";
            File.WriteAllText(path, toWrite);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Insert Code", $"Failed to insert code: {e.Message}", "OK");
        }
    }

    private void CreateNewScriptFromContent(string code, string language)
    {
        var defaultName = string.IsNullOrEmpty(language)
            ? "NewScript.cs"
            : $"New{char.ToUpperInvariant(language[0]) + language.Substring(1)}.cs";
        var path = EditorUtility.SaveFilePanelInProject("Create Script", defaultName, "cs",
            "Choose a location for the new script");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, code);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
            var created = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = created;
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Create Script", $"Failed to create script: {e.Message}", "OK");
        }
    }

    private void SaveCodeToProject(string code, string language)
    {
        // Let user choose any text-based extension, default to .txt if unknown language
        var defaultExt = string.IsNullOrEmpty(language) ? "txt" :
            language.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
            language.Equals("cs", StringComparison.OrdinalIgnoreCase) ? "cs" : language.ToLowerInvariant();
        var defaultName = string.IsNullOrEmpty(language) ? "snippet.txt" : $"snippet.{defaultExt}";
        var path = EditorUtility.SaveFilePanelInProject("Save Code", defaultName, defaultExt,
            "Choose a location to save the code snippet in your project");
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            File.WriteAllText(path, code);
            AssetDatabase.ImportAsset(path);
            AssetDatabase.Refresh();
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = asset;
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog("Save Code", $"Failed to save code: {e.Message}", "OK");
        }
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
