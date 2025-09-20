using NUnit.Framework;
using Ariko.Editor.Settings;

public class ToolRegistryTests
{
    private ArikoSettings settings;

    [SetUp]
    public void SetUp()
    {
        settings = new ArikoSettings();
    }

    [Test]
    public void ToolRegistry_AgentMode_NoDestructiveTools_RegistersCorrectTools()
    {
        // Arrange
        settings.enableDeleteTools = false;

        // Act
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Assert
        Assert.IsNotNull(toolRegistry.GetTool("create_game_object"));
        Assert.IsNotNull(toolRegistry.GetTool("create_file"));
        Assert.IsNotNull(toolRegistry.GetTool("modify_file"));
        Assert.IsNotNull(toolRegistry.GetTool("read_file"));
        Assert.IsNull(toolRegistry.GetTool("delete_file"));
    }

    [Test]
    public void ToolRegistry_AgentMode_WithDestructiveTools_RegistersAllTools()
    {
        // Arrange
        settings.enableDeleteTools = true;

        // Act
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Assert
        Assert.IsNotNull(toolRegistry.GetTool("create_game_object"));
        Assert.IsNotNull(toolRegistry.GetTool("create_file"));
        Assert.IsNotNull(toolRegistry.GetTool("modify_file"));
        Assert.IsNotNull(toolRegistry.GetTool("read_file"));
        Assert.IsNotNull(toolRegistry.GetTool("delete_file"));
    }

    [Test]
    public void ToolRegistry_AskMode_RegistersNoTools()
    {
        // Arrange
        settings.enableDeleteTools = true; // Ensure this doesn't affect the outcome

        // Act
        var toolRegistry = new ToolRegistry(settings, "Ask");

        // Assert
        Assert.IsNull(toolRegistry.GetTool("create_game_object"));
        Assert.IsNull(toolRegistry.GetTool("create_file"));
        Assert.IsNull(toolRegistry.GetTool("modify_file"));
        Assert.IsNull(toolRegistry.GetTool("read_file"));
        Assert.IsNull(toolRegistry.GetTool("delete_file"));
    }

    [Test]
    public void GetTool_ReturnsCorrectTool()
    {
        // Arrange
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Act
        var tool = toolRegistry.GetTool("create_file");

        // Assert
        Assert.IsNotNull(tool);
        Assert.AreEqual("create_file", tool.Name);
    }

    [Test]
    public void GetTool_ReturnsNullForNonExistentTool()
    {
        // Arrange
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Act
        var tool = toolRegistry.GetTool("non_existent_tool");

        // Assert
        Assert.IsNull(tool);
    }

    [Test]
    public void GetToolDefinitionsForPrompt_ReturnsCorrectlyFormattedString()
    {
        // Arrange
        settings.enableDeleteTools = true;
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Act
        var prompt = toolRegistry.GetToolDefinitionsForPrompt();

        // Assert
        StringAssert.Contains("You have access to the following tools.", prompt);
        StringAssert.Contains("Tool: create_game_object", prompt);
        StringAssert.Contains("Tool: create_file", prompt);
        StringAssert.Contains("Tool: modify_file", prompt);
        StringAssert.Contains("Tool: read_file", prompt);
        StringAssert.Contains("Tool: delete_file", prompt);
        StringAssert.Contains("Description:", prompt);
        StringAssert.Contains("Parameters:", prompt);
    }
}
