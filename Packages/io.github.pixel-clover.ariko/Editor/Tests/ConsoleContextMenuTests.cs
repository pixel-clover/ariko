using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Reflection;

public class ConsoleContextMenuTests
{
    // A mock class to simulate Unity's internal LogEntry class.
    private class MockLogEntry
    {
        public string message;
        public string condition;
    }

    [Test]
    public void ProcessLogEntry_WithDuplicateErrorMessage_ShouldProduceCleanStackTrace()
    {
        // Arrange
        var mockEntry = new MockLogEntry
        {
            message = "NullReferenceException: Object reference not set to an instance of an object.",
            condition = "NullReferenceException: Object reference not set to an instance of an object.\n" +
                        "TestError.ThrowError () (at Assets/Scripts/TestError.cs:15)\n" +
                        "FeatureTest.RunTest () (at Assets/Scripts/FeatureTest.cs:17)"
        };

        // Act
        var prompt = ConsoleContextMenu.ProcessLogEntry(mockEntry);

        // Assert
        Assert.IsNotNull(prompt);

        var expectedStackTrace = "TestError.ThrowError () (at Assets/Scripts/TestError.cs:15)\n" +
                                 "FeatureTest.RunTest () (at Assets/Scripts/FeatureTest.cs:17)";

        var stackTraceInPrompt = prompt.Substring(prompt.IndexOf("Stack Trace"));

        StringAssert.DoesNotContain(mockEntry.message, stackTraceInPrompt);
        StringAssert.Contains(expectedStackTrace.Trim(), stackTraceInPrompt);
    }
}
