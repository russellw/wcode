using System.Text.Json;

namespace wcode.Lib;

public class ProjectToolProvider
{
    public static List<Tool> CreateProjectTools(ProjectQueryService? queryService = null)
    {
        var tools = new List<Tool>();
        
        // Add project tools only if query service is available
        if (queryService != null)
        {
            tools.AddRange(new List<Tool>
            {
                new Tool
                {
                    Type = "function",
                    Function = new ToolFunction
                    {
                        Name = "read_file",
                        Description = "Read the contents of a file in the project",
                        Parameters = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new
                            {
                                filename = new
                                {
                                    type = "string",
                                    description = "The name or path of the file to read"
                                }
                            },
                            required = new[] { "filename" }
                        })
                    }
                },
                new Tool
                {
                    Type = "function",
                    Function = new ToolFunction
                    {
                        Name = "write_file",
                        Description = "Write content to a file in the project",
                        Parameters = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new
                            {
                                filename = new
                                {
                                    type = "string",
                                    description = "The name or path of the file to write"
                                },
                                content = new
                                {
                                    type = "string",
                                    description = "The content to write to the file"
                                }
                            },
                            required = new[] { "filename", "content" }
                        })
                    }
                },
                new Tool
                {
                    Type = "function",
                    Function = new ToolFunction
                    {
                        Name = "list_files",
                        Description = "List all files in the project directory",
                        Parameters = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new object(),
                            required = new string[0]
                        })
                    }
                },
                new Tool
                {
                    Type = "function",
                    Function = new ToolFunction
                    {
                        Name = "search_files",
                        Description = "Search for content within project files",
                        Parameters = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new
                            {
                                query = new
                                {
                                    type = "string",
                                    description = "The text to search for"
                                }
                            },
                            required = new[] { "query" }
                        })
                    }
                },
                new Tool
                {
                    Type = "function",
                    Function = new ToolFunction
                    {
                        Name = "get_project_structure",
                        Description = "Get the overall structure of the project",
                        Parameters = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new object(),
                            required = new string[0]
                        })
                    }
                },
                new Tool
                {
                    Type = "function",
                    Function = new ToolFunction
                    {
                        Name = "run_program",
                        Description = "Run a program or command in a secure Docker container",
                        Parameters = JsonSerializer.SerializeToElement(new
                        {
                            type = "object",
                            properties = new
                            {
                                command = new
                                {
                                    type = "string",
                                    description = "The command to execute"
                                },
                                language = new
                                {
                                    type = "string",
                                    description = "The programming language (python, node, etc.)"
                                },
                                timeout = new
                                {
                                    type = "number",
                                    description = "Timeout in seconds (default: 30)"
                                }
                            },
                            required = new[] { "command", "language" }
                        })
                    }
                }
            });
        }
        
        // Add a simple diagnostic tool that always works
        tools.Add(new Tool
        {
            Type = "function",
            Function = new ToolFunction
            {
                Name = "get_system_info",
                Description = "Get information about the current system and available capabilities",
                Parameters = JsonSerializer.SerializeToElement(new
                {
                    type = "object",
                    properties = new object(),
                    required = new string[0]
                })
            }
        });
        
        return tools;
    }
}