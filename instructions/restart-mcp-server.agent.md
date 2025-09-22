- To restart the MCP server, you can use the following command:
  + on Windows: `cd ".\.mcp-dotnet"; taskkill /f /im ".mcp-dotnet.exe" 2>$null; taskkill /f /im "dotnet.exe" 2>$null; Start-Sleep 2; & "..\.dotnet\dotnet.exe" build --verbosity quiet`
- Then just call any MCP tool - server will start automatically.
- Don't run the MCP server manually with `dothet TBD mcp_server.run`.
- If something went wrong, ask me to enable it manually in the `.vscode/mcp.json` file. There are buttons for that. Then wait for my confirmation.