using UnityEngine;

[CreateAssetMenu(fileName = "ArikoSettings", menuName = "Ariko/Assistant Settings")]
public class ArikoSettings : ScriptableObject
{
    public string ollama_Url = "http://localhost:11434";

    [Header("Saved Model Selections")] public string google_SelectedModel;

    public string openAI_SelectedModel;
    public string ollama_SelectedModel;

    [Header("Saved UI Selections")] public string selectedProvider = "OpenAI";

    public string selectedWorkMode = "Ask";

    [Header("Customization")] [Tooltip("The background color for Ariko's chat bubbles.")]
    public Color assistantChatBackgroundColor = new(0.886f, 0.91f, 0.941f, 1.0f);

    [Tooltip("The background color for the user's chat bubbles.")]
    public Color userChatBackgroundColor = new(0.676f, 0.853f, 0.602f, 1.0f);

    [Tooltip("Font used for chat text (leave null to use default editor font).")]
    public Font chatFont;

    [Tooltip("Font size for chat text.")] public int chatFontSize = 12;

    [Tooltip("Make the role labels (User or Ariko) bold.")]
    public bool roleLabelsBold = true;

    [Tooltip("How many past chat sessions to keep in history. Set to 0 for infinite.")]
    public int chatHistorySize = 5;

    [Header("System Prompt")]
    [TextArea(5, 15)]
    [Tooltip("The initial instruction given to the AI at the start of each new conversation.")]
    public string systemPrompt =
        "You are Ariko, a helpful and friendly AI assistant integrated into the Unity Editor.\n" +
        "Your goal is to assist developers with their Unity and C# questions.\n" +
        "Be concise, accurate, and provide code examples when relevant.\n" +
        "You are an expert in the Unity API." +
        "\n\n" +
        "### Follow these rules when interacting with the user:\n" +
        "1.  **Analyze and Plan:** First, analyze the user's request. Formulate a concise, step-by-step plan and state it clearly.\n" +
        "2.  **Execute Autonomously:** After stating your plan, immediately execute the first step by calling the appropriate tool. The system will pause for user approval before the tool runs.\n" +
        "3.  **Handle Simple Conversation:** If the user's input is a greeting, a simple question, or does not require a tool, respond conversationally. DO NOT use a tool if a direct text answer is sufficient.\n" +
        "4.  **One Tool at aTime:** Decompose complex tasks into a sequence of single tool calls.\n" +
        "5.  **Be Concise:** Do not add comments to code unless requested. Avoid conversational filler when executing a task.";

    [Header("Agent System Prompt")] [TextArea(5, 15)]
    public string agentSystemPrompt =
        "You are an expert Unity developer agent. Your goal is to help the user by performing actions in the Unity Editor.\n" +
        "Analyze the user's request and break it down into steps.\n" +
        "For each step, decide if you need to use a tool. If you do, you must respond ONLY with a JSON object in the following format:\n" +
        "{\n" +
        "  \"thought\": \"A brief explanation of why you are choosing this tool.\",\n" +
        "  \"tool_name\": \"TheNameOfTheToolToUse\",\n" +
        "  \"parameters\": { \"param1\": \"value1\", \"param2\": 123 }\n" +
        "}\n" +
        "If you do not need to use a tool, or if the task is complete, respond with a conversational message.";
}
