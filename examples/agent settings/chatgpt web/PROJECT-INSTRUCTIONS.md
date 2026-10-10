## Shared Memory Context

Memory context ID: `chatgpt-project:my-project`

Git Repositories:

- `git:github.com/Me/my-project`
- `git:github.com/Me/my-other-project`

For project-specific persistent memory, use the `project-memory` skill and the configured project-memory MCP connection. Pass the exact ChatGPT Project Memory context ID to project-scoped memory tools.

The listed Git identities are associated repository contexts for coding agents. They do not override this ChatGPT context ID and do not guarantee a corresponding memory binding. Each desired binding requires separate validation and operator approval before creation.
