- If you need to test the tool after creation or modification do the following:
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- run lng_winapi_window_tree '{\"pid\":18672}'
```
- If you need to run two or more tools together, you can run them in parallel with bathch command:
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- batch lng_llm_rag_add_data '{\"input_text\":\"Hello pirate!\"}' lng_llm_rag_search '{\"query\":\"Pirate\"}'
```
- If you need to run the same in `daemon` mode:
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- run --daemon lng_winapi_window_tree '{\"pid\":18672}'
```
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- batch --daemon lng_llm_rag_add_data '{\"input_text\":\"Hello pirate!\"}' lng_llm_rag_search '{\"query\":\"Pirate\"}'
```
- Log file in this case will be `.mcp-dotnet/logs/mcp_server.log`.
- Always use single quotes for the json parameter string, and escape double quotes within that string with a slash.
Not:
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- run lng_count_words '{"input_text":"Hello world! This is a test."}'
```
Not:
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- run lng_count_words "{`"input_text`":`"Hello world! This is a test.`"}"
```
But:
```bash
dotnet run --project .mcp-dotnet/Run.csproj -- run lng_count_words '{\"input_text\":\"Hello world! This is a test.\"}'
```
- But if you were asked to test a new tool via MCP explicitly, then always ask after the changes if the server was restarted, as this is done manually.
- If an error occurred during testing through MCP, report it as `FAIL` even if testing through `dotnet run --project .mcp-dotnet/Run.csproj -- run` gave a positive result. 
