using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;

/// <summary>
/// Manages the agentic workflow, including interpreting LLM responses as tool calls,
/// handling user confirmation for tool execution, and orchestrating the agent-thought-tool loop.
/// </summary>
public class AgentService
{
    private readonly Dictionary<string, string> apiKeys;
    private readonly Func<string> buildContext;
    private readonly Func<ChatSession> getActiveSession;
    private readonly Func<ToolRegistry> getToolRegistry;
    private readonly ArikoLLMService llmService;
    private readonly Action<ChatMessage, ChatSession> onMessageAdded;

    private readonly Action<bool> onResponseStatusChanged;
    private readonly Action<ToolCall> onToolCallConfirmationRequested;
    private readonly ArikoSettings settings;

    private ToolCall pendingToolCall;

    public AgentService(
        ArikoLLMService llmService,
        ArikoSettings settings,
        Dictionary<string, string> apiKeys,
        Func<ChatSession> getActiveSession,
        Func<string> buildContext,
        Func<ToolRegistry> getToolRegistry,
        Action<bool> onResponseStatusChanged,
        Action<ChatMessage, ChatSession> onMessageAdded,
        Action<ToolCall> onToolCallConfirmationRequested)
    {
        this.llmService = llmService;
        this.settings = settings;
        this.apiKeys = apiKeys;
        this.getActiveSession = getActiveSession;
        this.buildContext = buildContext;
        this.getToolRegistry = getToolRegistry;
        this.onResponseStatusChanged = onResponseStatusChanged;
        this.onMessageAdded = onMessageAdded;
        this.onToolCallConfirmationRequested = onToolCallConfirmationRequested;
    }

    public async Task SendAgentRequest(string selectedProvider, string selectedModel)
    {
        onResponseStatusChanged?.Invoke(true);

        var sessionForThisMessage = getActiveSession();

        var messagesToSend = new List<ChatMessage>();
        var toolDefinitions = getToolRegistry().GetToolDefinitionsForPrompt();

        var systemPromptBuilder = new StringBuilder();
        systemPromptBuilder.AppendLine(settings.agentSystemPrompt);
        systemPromptBuilder.AppendLine(toolDefinitions);

        var context = buildContext();
        if (!string.IsNullOrEmpty(context))
        {
            systemPromptBuilder.AppendLine("\n--- Context ---");
            systemPromptBuilder.AppendLine(context);
        }

        messagesToSend.Add(new ChatMessage { Role = "System", Content = systemPromptBuilder.ToString() });
        messagesToSend.AddRange(sessionForThisMessage.Messages);

        var provider = (ArikoLLMService.AIProvider)Enum.Parse(typeof(ArikoLLMService.AIProvider), selectedProvider);
        var result = await llmService.SendChatRequest(messagesToSend, provider, selectedModel, settings, apiKeys);
        onResponseStatusChanged?.Invoke(false);

        if (result.IsSuccess)
        {
            if (TryParseToolCall(result.Data, out var toolCall))
            {
                pendingToolCall = toolCall;
                onToolCallConfirmationRequested?.Invoke(toolCall);
            }
            else
            {
                var finalMessage = new ChatMessage { Role = "Ariko", Content = result.Data };
                sessionForThisMessage.Messages.Add(finalMessage);
                onMessageAdded?.Invoke(finalMessage, sessionForThisMessage);
            }
        }
        else
        {
            var errorMessage = GetFormattedErrorMessage(result.Error, result.ErrorType);
            var finalMessage = new ChatMessage { Role = "Ariko", Content = errorMessage, IsError = true };
            sessionForThisMessage.Messages.Add(finalMessage);
            onMessageAdded?.Invoke(finalMessage, sessionForThisMessage);
        }
    }

    public async void RespondToToolConfirmation(bool userApproved, string selectedProvider, string selectedModel)
    {
        if (pendingToolCall == null) return;

        var toolCall = pendingToolCall;
        pendingToolCall = null;

        var sessionForThisMessage = getActiveSession();
        var toolRegistry = getToolRegistry();

        if (userApproved)
        {
            var tool = toolRegistry.GetTool(toolCall.tool_name);
            string executionResult;
            if (tool != null)
            {
                var context = new ToolExecutionContext
                {
                    Arguments = toolCall.parameters,
                    Provider = selectedProvider,
                    Model = selectedModel,
                    Settings = settings,
                    ApiKeys = apiKeys
                };
                executionResult = await tool.Execute(context);
            }
            else
            {
                executionResult = $"Error: Tool '{toolCall.tool_name}' not found.";
            }

            var resultMessage = new ChatMessage { Role = "User", Content = $"Observation: {executionResult}" };
            sessionForThisMessage.Messages.Add(resultMessage);
            onMessageAdded?.Invoke(resultMessage, sessionForThisMessage);

            await SendAgentRequest(selectedProvider, selectedModel);
        }
        else
        {
            var denialMessage = new ChatMessage { Role = "User", Content = "Observation: User denied the action." };
            sessionForThisMessage.Messages.Add(denialMessage);
            onMessageAdded?.Invoke(denialMessage, sessionForThisMessage);
            await SendAgentRequest(selectedProvider, selectedModel);
        }
    }

    private bool TryParseToolCall(string response, out ToolCall toolCall)
    {
        toolCall = null;
        try
        {
            var match = Regex.Match(response, @"```json\s*([\s\S]*?)\s*```", RegexOptions.Singleline);
            var jsonToParse = match.Success ? match.Groups[1].Value : response;

            var agentResponse = JsonConvert.DeserializeObject<AgentResponse>(jsonToParse);

            if (string.IsNullOrWhiteSpace(agentResponse?.ToolName) ||
                getToolRegistry().GetTool(agentResponse.ToolName) == null) return false;

            toolCall = new ToolCall
            {
                thought = agentResponse.Thought,
                tool_name = agentResponse.ToolName,
                parameters = agentResponse.Parameters ?? new Dictionary<string, object>()
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string GetFormattedErrorMessage(string error, ErrorType errorType)
    {
        switch (errorType)
        {
            case ErrorType.Auth:
                return $"Authentication Error: {error}\nPlease check your API key or settings.";
            case ErrorType.Network:
                return $"Network Error: {error}\nPlease check your internet connection.";
            case ErrorType.Http:
                return $"API Error: {error}";
            case ErrorType.Parsing:
                return $"Response Parsing Error: {error}\nThe data from the server was in an unexpected format.";
            case ErrorType.Cancellation:
                return "Request was cancelled.";
            default:
                return $"An unknown error occurred: {error}";
        }
    }
}
