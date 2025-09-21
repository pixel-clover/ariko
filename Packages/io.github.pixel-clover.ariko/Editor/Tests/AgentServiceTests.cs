using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class AgentServiceTests
{
    private ArikoSettings settings;
    private ArikoLLMService llmService;
    private AgentService agentService;
    private ChatSession activeSession;
    private ToolRegistry toolRegistry;
    private List<string> logMessages;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<ArikoSettings>();
        settings.debugMode = true;
        llmService = new ArikoLLMService();
        activeSession = new ChatSession();
        toolRegistry = new ToolRegistry(settings, "Agent");
        logMessages = new List<string>();

        agentService = new AgentService(
            llmService,
            settings,
            new Dictionary<string, string>(),
            () => activeSession,
            () => "",
            () => toolRegistry,
            isPending => { },
            (msg, sess) => { },
            toolCall => { }
        );

        Application.logMessageReceived += HandleLog;
    }

    [TearDown]
    public void TearDown()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        logMessages.Add(logString);
    }

    [Test]
    public async Task AgentService_DebugMode_LogsCorrectly()
    {
        // Arrange
        var mockLlmService = new MockArikoLLMService();
        agentService = new AgentService(
            mockLlmService,
            settings,
            new Dictionary<string, string>(),
            () => activeSession,
            () => "",
            () => toolRegistry,
            isPending => { },
            (msg, sess) => { },
            toolCall => { }
        );

        // Act
        await agentService.SendAgentRequest("Google", "gemini-1.5-pro");

        // Assert
        Assert.IsTrue(logMessages.Count > 0, "No log messages were received.");
        Assert.IsTrue(logMessages.Exists(m => m.Contains("Sending system prompt")), "System prompt was not logged.");
        Assert.IsTrue(logMessages.Exists(m => m.Contains("Received response")), "LLM response was not logged.");
    }

    private class MockArikoLLMService : ArikoLLMService
    {
        public override Task<WebRequestResult<string>> SendChatRequest(List<ChatMessage> messages, AIProvider provider, string model, ArikoSettings settings, Dictionary<string, string> apiKeys)
        {
            var response = @"{
                ""thought"": ""The user wants to create a file. I will use the CreateFile tool."",
                ""tool_name"": ""CreateFile"",
                ""parameters"": {
                    ""filePath"": ""Assets/test.txt"",
                    ""content"": ""Hello, world!""
                }
            }";
            return Task.FromResult(new WebRequestResult<string> { IsSuccess = true, Data = response });
        }
    }
}
