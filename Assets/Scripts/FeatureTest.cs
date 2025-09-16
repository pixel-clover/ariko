using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
///     A test class for demonstrating and testing the "Explain Error" feature of Ariko.
/// </summary>
public class FeatureTest
{
    /// <summary>
    ///     Runs the feature test. It generates a test error, retrieves it from the console,
    ///     and sends it to Ariko for an explanation.
    /// </summary>
    [MenuItem("Tools/Ariko Tests/Run Feature Test")]
    public static void RunTest()
    {
        // 1. Generate an error
        TestError.ThrowError();

        // 2. Use reflection to get the LogEntries class and its methods
        var logEntriesType = Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        if (logEntriesType == null)
        {
            Debug.LogError("Ariko Test: Could not find LogEntries type.");
            return;
        }

        var logEntryType = logEntriesType.GetNestedType("LogEntry", BindingFlags.Public | BindingFlags.NonPublic);
        if (logEntryType == null)
        {
            Debug.LogError("Ariko Test: Could not find nested LogEntry type.");
            return;
        }

        var getEntryMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);
        var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);

        if (getEntryMethod == null || getCountMethod == null)
        {
            Debug.LogError("Ariko Test: Could not find GetEntryInternal or GetCount methods.");
            return;
        }

        // 3. Get the last log entry
        var count = (int)getCountMethod.Invoke(null, null);
        if (count == 0)
        {
            Debug.LogError("Ariko Test: No log entries found.");
            return;
        }

        var logEntry = Activator.CreateInstance(logEntryType);
        object[] args = { count - 1, logEntry };
        getEntryMethod.Invoke(null, args);
        logEntry = args[1];

        if (logEntry == null)
        {
            Debug.LogError("Ariko Test: Failed to get log entry.");
            return;
        }

        // 4. Call the public processing method directly for testing
        ConsoleContextMenu.ProcessLogEntry(logEntry);

        Debug.Log("Ariko Test: Feature test executed. Check the Ariko window for the explanation.");
    }
}
