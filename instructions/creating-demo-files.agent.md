- Demo files are placed in `/case/` subfolder within tool directory with `.demo.agent.md` extension
- File name pattern: `{feature-name}.demo.agent.md` (lowercase, kebab-case)
- Demo files must follow specific structure with exact section headers
- Keep demonstrations concise and focused on practical usage
- Use actual MCP tool calls, not terminal commands unless debugging
- Structure demonstration as step-by-step sequence with clear inputs/outputs
- Include all necessary parameters and configurations for reproducible results

## Required Sections

### ## Info
- One brief sentence describing what the demo demonstrates
- Focus on the practical capability being shown

### ## How to ask for demo in chat
- Exact template for users to request this demo
- Use format: "Show me demo from file:" followed by descriptive request
- Make request sound natural and user-friendly

### ## 🤖 AI Instructions  
- Step-by-step numbered sequence of actions
- Each step should include specific MCP tool calls with exact JSON parameters
- Use concise bullet points under each step
- Include key expressions and parameter combinations
- Highlight critical configuration details with **bold** text

### ## Important
- Critical notes about configuration, limitations, or prerequisites
- Environment variables, ports, file paths, authentication requirements
- Special encoding rules or parameter formats
- Error handling considerations

### ## Scenario
- Brief description of what the demo accomplishes
- Input/output flow explanation
- Key features being demonstrated

## Optional Sections

### API Usage Examples / PowerShell Examples / Testing
- Practical examples for external validation
- Code snippets for different interfaces (GET/POST, CLI commands)
- Expected response formats

### Response Format / Supported Operations
- Technical specifications when relevant
- Available options and parameters

## Style Guidelines

- Use JSON code blocks with proper syntax highlighting
- Employ `{! expression !}` syntax for dynamic values
- Include actual URLs, ports, and realistic data
- Keep explanations minimal - focus on actionable steps  
- Use emojis sparingly (🤖 for AI Instructions section only)
- Provide complete, working examples that can be copy-pasted
- Test all MCP tool calls before including them
- Use bullet points and sub-bullets, avoid long paragraphs
- Include specific error conditions and their handling
