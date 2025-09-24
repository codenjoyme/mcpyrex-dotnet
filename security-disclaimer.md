# Security Disclaimer

## Data Security

Where might data leak when working with this tool?

### 1. Data Leak #1: LLM Provider
The solution operates under GitHub Copilot (or Cursor) management with the LLM selected in it. In both cases, you can replace the LLM with one used in your project perimeter.

### 2. Data Leak #2: LLM Tools
**Not implemented** If you use LLM-based tools (`lng_llm_*`), another LLM is used under the hood (with an API key specified in the `.env` file), so you can also configure it to use an LLM available in your project. Currently, there are two options: `azure` and `personal openai`.

### 3. Data Leak #3: .Net Libraries
The third data leak may occur when connecting Python libraries if you create a tool for processing something.

Each tool (or group of tools) has a `settings.yaml` file that describes which libraries are used in it. If not acceptable, set `enabled: false` in the file and the tool will not be available. Alternatively, ask Copilot to change the library and tool implementation to a more secure one.

**Note**: There is a library selection guide (`./instructions/choose-library.agent.md`) that should be used during development to ensure secure library choices.

### 4. Data Leak #4: Generated Code
`GitHub Copilot` / `Cursor` in agent mode generates solutions in `.Net` and (less frequently) `JavaScript` code. Leaks are possible somewhere here. I haven't observed the LLM (I use `Claude Sonnet 4`) intentionally leaving `backdoors`, but everything depends on your request - you need to understand and accept the risk since you're generating code that you cannot (or will not) read and understand. In this case, reducing leak risks can be achieved by requesting the LLM to audit the solution after its generation.

## Risk Mitigation

To minimize security risks:

1. **Use Local/Private LLMs**: Configure both GitHub Copilot and LangChain tools to use LLMs within your project perimeter
2. **Review Dependencies**: Check `settings.yaml` files in each tool and disable tools with unacceptable libraries
3. **Code Review**: Always review generated code before execution
4. **Audit Requests**: Ask the LLM to audit solutions after generation
5. **Environment Isolation**: Use virtual environments and proper access controls
6. **Regular Updates**: Keep dependencies updated and monitor for security advisories

## Recommended Configuration

For maximum security:
- Use on-premises or private cloud LLM instances
- Review and approve all tool dependencies
- Implement code review processes for generated solutions
- Use network isolation where possible
- Monitor and log tool usage
