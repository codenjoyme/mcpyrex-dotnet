- Links contains `{workspaceFolder}` which should be replaced with absolute path to the project.
 - For example for Windows.
 ```json
 {
  "mcpServers": {
    "langchain-mcp-dotnet": {
      "type": "stdio", 
      "command": "C:\\workspace\\hello-langchain\\.dotnet\\dotnet.exe",
      "args": ["run", "--project", "C:\\workspace\\hello-langchain\\.mcp-dotnet\\mcp.csproj"]
    }
  }
}
```
- For other OS links should be un Unix style: `/home/user/workspace/hello-langchain/.mcp-dotnet/mcp.csproj`.