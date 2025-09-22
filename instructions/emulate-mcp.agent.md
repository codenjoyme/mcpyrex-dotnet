If there is no MCP enabled, follow instructions:
- Open a terminal and run in background: 
  `dotnet TBD .mcp-dotnet/proxy.py`
- Get list of available tools:
  `dotnet TBD .mcp-dotnet/execute.py list`
- Deside which tool to use by user prompt and run (for example):
  `dotnet TBD .mcp-dotnet/execute.py exec lng_count_words --params '{\"input_text\": \"Hello world this is a test\"}'`
Then interpret the result and continue with next tool if needed.