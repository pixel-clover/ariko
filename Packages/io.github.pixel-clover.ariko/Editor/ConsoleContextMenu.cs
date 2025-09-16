// csharp

using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
///     Adds context menu items to the Unity Console window to ask Ariko to explain log entries.
///     Uses reflection to support multiple Unity versions.
/// </summary>
public static class ConsoleContextMenu
{
    [MenuItem("CONTEXT/LogEntry/Ask Ariko to Explain")]
    private static void ExplainErrorWithArikoMenuAction(MenuCommand command)
    {
        ProcessLogEntry(command.context);
    }

    // Fallback for Unity versions that expose the Console window as the context.
    [MenuItem("CONTEXT/ConsoleWindow/Ask Ariko to Explain")]
    private static void ExplainFromConsoleWindow(MenuCommand command)
    {
        if (!TryExplainFromActiveConsole())
            Debug.LogError("Ariko: Could not read the selected Console entry.");
    }

    // Top-level fallback in case no context menu is shown.
    [MenuItem("Tools/Ariko/Explain Selected Console Error", priority = 2000)]
    private static void ExplainSelectedConsoleError()
    {
        if (!TryExplainFromActiveConsole())
            Debug.LogError("Ariko: No Console entry selected or Unity internals changed.");
    }

    /// <summary>
    ///     Processes a log entry object to extract the error message and stack trace, then sends it to Ariko.
    ///     This method is public for testability.
    /// </summary>
    /// <param name="logEntry">The log entry object from the Unity Editor's internal API.</param>
    public static void ProcessLogEntry(object logEntry)
    {
        if (logEntry == null)
        {
            Debug.LogError("Ariko: Could not find LogEntry object from context.");
            return;
        }

        var type = logEntry.GetType();
        var messageField = type.GetField("message", BindingFlags.Instance | BindingFlags.Public);
        var conditionField = type.GetField("condition", BindingFlags.Instance | BindingFlags.Public);

        if (messageField == null || conditionField == null)
        {
            Debug.LogError("Ariko: Could not find 'message' or 'condition' in LogEntry. Unity API may have changed.");
            return;
        }

        var errorText = messageField.GetValue(logEntry) as string;
        var stackTrace = conditionField.GetValue(logEntry) as string;

        if (string.IsNullOrEmpty(errorText))
        {
            Debug.LogWarning("Ariko: The selected log entry appears to be empty.");
            return;
        }

        SendToAriko(errorText, stackTrace);
    }

    // Reads the ConsoleWindow's active text (works across many Unity versions).
    private static bool TryExplainFromActiveConsole()
    {
        var editorAsm = typeof(EditorWindow).Assembly;
        var consoleWindowType = editorAsm.GetType("UnityEditor.ConsoleWindow");
        if (consoleWindowType == null)
            return false;

        var focused = EditorWindow.focusedWindow;
        var consoleWindow = focused != null && consoleWindowType.IsInstanceOfType(focused)
            ? focused
            : Resources.FindObjectsOfTypeAll(consoleWindowType).FirstOrDefault() as EditorWindow;

        if (consoleWindow == null)
            return false;

        var activeTextField =
            consoleWindowType.GetField("m_ActiveText", BindingFlags.Instance | BindingFlags.NonPublic);
        var activeText = activeTextField?.GetValue(consoleWindow) as string;

        if (string.IsNullOrEmpty(activeText))
            return false;

        // First line as error, remainder as stack trace.
        var parts = activeText.Split(new[] { '\n' }, 2);
        var errorText = parts.Length > 0 ? parts[0] : activeText;
        var stackTrace = parts.Length > 1 ? parts[1] : string.Empty;

        SendToAriko(errorText, stackTrace);
        return true;
    }

    private static void SendToAriko(string errorText, string stackTrace)
    {
        var arikoWindow = EditorWindow.GetWindow<ArikoWindow>("Ariko Assistant");
        var prompt =
            "Explain this Unity error and stack trace. Identify the likely cause. Provide a corrected C# code snippet.\n\n" +
            $"\\*\\*Error:\\*\\*\n```\\n{errorText}\\n```\n\n" +
            $"\\*\\*Stack Trace:\\*\\*\n```\\n{stackTrace}\\n```";

        EditorApplication.delayCall += () =>
        {
            if (arikoWindow != null && arikoWindow.controller != null)
                arikoWindow.SendExternalMessage(prompt);
            else
                Debug.LogError("Ariko: The Ariko window is not available to process the request.");
        };
    }
}
