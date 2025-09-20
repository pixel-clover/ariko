using NUnit.Framework;
using UnityEngine;

public class ToolRegistryTests
{
    private ArikoSettings settings;

    [SetUp]
    public void SetUp()
    {
        settings = ScriptableObject.CreateInstance<ArikoSettings>();
    }

    [Test]
    public void ToolRegistry_AgentMode_NoDestructiveTools_RegistersCorrectTools()
    {
        // Arrange
        settings.enableDeleteTools = false;

        // Act
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Assert
        Assert.IsNotNull(toolRegistry.GetTool("CreateGameObject"));
        Assert.IsNotNull(toolRegistry.GetTool("CreateFile"));
        Assert.IsNotNull(toolRegistry.GetTool("ModifyFile"));
        Assert.IsNotNull(toolRegistry.GetTool("read_file"));
        Assert.IsNull(toolRegistry.GetTool("DeleteFile"));
    }

    [Test]
    public void ToolRegistry_AgentMode_WithDestructiveTools_RegistersAllTools()
    {
        // Arrange
        settings.enableDeleteTools = true;

        // Act
        var toolRegistry = new ToolRegistry(settings, "Agent");

        // Assert
        Assert.IsNotNull(toolRegistry.GetTool("CreateGameObject"));
        Assert.IsNotNull(toolRegistry.GetTool("CreateFile"));
        Assert.IsNotNull(toolRegistry.GetTool("ModifyFile"));
        Assert.IsNotNull(toolRegistry.GetTool("read_file"));
        Assert.IsNotNull(toolRegistry.GetTool("DeleteFile"));
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
        var expectedPrompt = "You have access to the following tools. Use them to fulfill the user's request.\n" +
                             "Tool: CreateGameObject\n" +
                             "Description: Creates a new GameObject with the given name.\n" +
                             "Parameters: name (The name of the GameObject to create.)\n\n" +
                             "Tool: CreateFile\n" +
                             "Description: Creates a new file with the given content. Useful for creating new C# scripts.\n" +
                             "Parameters: filePath (The path of the file to create. Should be relative to the Assets folder.), content (The content of the file to create.)\n\n" +
                             "Tool: ModifyFile\n" +
                             "Description: Modifies an existing file with a prompt.\n" +
                             "Parameters: filePath (The path of the file to modify, relative to the Assets folder.), prompt (A prompt describing the modifications to make.)\n\n" +
                             "Tool: read_file\n" +
                             "Description: Reads the entire content of a file at the specified path.\n" +
                             "Parameters: path (The relative path to the file to be read.)\n\n" +
                             "Tool: DeleteFile\n" +
                             "Description: Deletes a file at the given path.\n" +
                             "Parameters: filePath (The path of the file to delete. Should be relative to the Assets folder.)\n\n";

        // Act
        var prompt = toolRegistry.GetToolDefinitionsForPrompt();

        // Assert
        Assert.AreEqual(expectedPrompt, prompt);
    }
}
