# AGENTS.md

## Shared Project Memory

Use the configured project-memory MCP service for durable, project-scoped knowledge. For substantive work requiring earlier decisions or context, load the `project-memory` skill. Determine the canonical Git context from the actual repository's unambiguous `origin` fetch remote, not the working-directory name; pass that exact context to scoped memory operations. Let the MCP service validate project binding and selection.

Do not use unscoped memory operations, manually choose another project, bypass the configured MCP boundary, or fall back to a default. When no trustworthy bound context is available, continue work without persistent project memory and report the limitation when material.

All newly created persistent project-memory notes must be written in English, including titles, headings, content, observations, and human-readable metadata; preserve exact technical literals and relevant original quotations.

These memory instructions do not relax the collaboration or task-scoped Git approval rules above.
