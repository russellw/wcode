using System.Text.Json;
using wcode.Lib;

namespace wcode.Tests;

public class ProjectToolTests
{
    private readonly string _testProjectPath;
    private readonly ProjectQueryService _queryService;
    private readonly ProjectToolExecutor _toolExecutor;

    public ProjectToolTests()
    {
        // Create a temporary test directory
        _testProjectPath = Path.Combine(Path.GetTempPath(), "wcode_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testProjectPath);
        
        // Create test files
        SetupTestProject();
        
        _queryService = new ProjectQueryService(_testProjectPath);
        _toolExecutor = new ProjectToolExecutor(_queryService);
    }

    private void SetupTestProject()
    {
        // Create test files for testing
        var testCsFile = Path.Combine(_testProjectPath, "TestClass.cs");
        File.WriteAllText(testCsFile, @"using System;

namespace TestProject
{
    public class TestClass
    {
        public void TestMethod()
        {
            Console.WriteLine(""Hello, World!"");
        }
    }
}");

        var testTxtFile = Path.Combine(_testProjectPath, "readme.txt");
        File.WriteAllText(testTxtFile, "This is a test project for wcode tool testing.");

        var testJsonFile = Path.Combine(_testProjectPath, "config.json");
        File.WriteAllText(testJsonFile, @"{
    ""name"": ""test-project"",
    ""version"": ""1.0.0"",
    ""tools"": [""read_file"", ""list_files"", ""search_files""]
}");

        // Create a subdirectory with files
        var subDir = Path.Combine(_testProjectPath, "src");
        Directory.CreateDirectory(subDir);
        
        var subFile = Path.Combine(subDir, "Program.cs");
        File.WriteAllText(subFile, @"using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine(""Test application"");
    }
}");
    }

    [Fact]
    public async Task ReadFile_ExistingFile_ReturnsContent()
    {
        // Arrange
        var toolCall = CreateToolCall("read_file", new { filename = "TestClass.cs" });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("public class TestClass", result);
        Assert.Contains("Console.WriteLine", result);
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task ReadFile_NonExistentFile_ReturnsError()
    {
        // Arrange
        var toolCall = CreateToolCall("read_file", new { filename = "NonExistent.cs" });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task ListFiles_ValidProject_ReturnsFileList()
    {
        // Arrange
        var toolCall = CreateToolCall("list_files", new { });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("TestClass.cs", result);
        Assert.Contains("readme.txt", result);
        Assert.Contains("config.json", result);
        Assert.Contains("src", result);
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task SearchFiles_ExistingContent_ReturnsMatches()
    {
        // Arrange
        var toolCall = CreateToolCall("search_files", new { query = "Console.WriteLine" });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("TestClass.cs", result);
        Assert.Contains("Program.cs", result);
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task SearchFiles_NonExistentContent_ReturnsNoMatches()
    {
        // Arrange
        var toolCall = CreateToolCall("search_files", new { query = "NonExistentContent" });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("No results found.", result);
    }

    [Fact]
    public async Task GetProjectStructure_ValidProject_ReturnsStructure()
    {
        // Arrange
        var toolCall = CreateToolCall("get_project_structure", new { });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("TestClass.cs", result);
        Assert.Contains("src", result);
        Assert.Contains("Program.cs", result);
        Assert.DoesNotContain("Error", result);
    }

    [Fact]
    public async Task GetSystemInfo_Always_ReturnsSystemInfo()
    {
        // Arrange
        var toolCall = CreateToolCall("get_system_info", new { });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("System Information", result);
        Assert.Contains("Tool calling: Available", result);
        Assert.Contains("Project query service: Available", result);
        Assert.Contains("Current time:", result);
        Assert.Contains("Available tools:", result);
    }

    [Fact]
    public async Task UnknownTool_ReturnsError()
    {
        // Arrange
        var toolCall = CreateToolCall("unknown_tool", new { });

        // Act
        var result = await _toolExecutor.ExecuteToolCallAsync(toolCall);

        // Assert
        Assert.Contains("Unknown tool function", result);
    }

    [Fact]
    public void ProjectToolProvider_CreateTools_ReturnsExpectedTools()
    {
        // Act
        var tools = ProjectToolProvider.CreateProjectTools(_queryService);

        // Assert
        Assert.Equal(5, tools.Count);
        Assert.Contains(tools, t => t.Function.Name == "read_file");
        Assert.Contains(tools, t => t.Function.Name == "list_files");
        Assert.Contains(tools, t => t.Function.Name == "search_files");
        Assert.Contains(tools, t => t.Function.Name == "get_project_structure");
        Assert.Contains(tools, t => t.Function.Name == "get_system_info");
    }

    [Fact]
    public void ProjectToolProvider_NoQueryService_ReturnsOnlySystemInfo()
    {
        // Act
        var tools = ProjectToolProvider.CreateProjectTools(null);

        // Assert
        Assert.Single(tools);
        Assert.Equal("get_system_info", tools[0].Function.Name);
    }

    private static ToolCall CreateToolCall(string functionName, object arguments)
    {
        return new ToolCall
        {
            Id = Guid.NewGuid().ToString(),
            Type = "function",
            Function = new ToolCallFunction
            {
                Name = functionName,
                Arguments = JsonSerializer.SerializeToElement(arguments)
            }
        };
    }

    private void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testProjectPath))
        {
            Directory.Delete(_testProjectPath, true);
        }
    }
}