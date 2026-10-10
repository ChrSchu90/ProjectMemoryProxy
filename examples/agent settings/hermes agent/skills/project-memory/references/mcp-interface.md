# Project-memory MCP interface

Read this reference when composing a memory tool call or diagnosing a missing binding. It describes the currently planned **gateway contract**, not the storage backend. Actual MCP tool discovery and schemas are authoritative; capabilities can change.

## Project-scoped operations

- Search: `search_notes(context_id, ...)`, when exposed. Try multiple natural-language query variations. Use scoped filters such as `metadata_filters`, `tags`, or `status` only if the live schema supports them.
- Read: `read_note(context_id, identifier, ...)`, using the exact returned permalink or identifier when possible.
- Create/update: `write_note(context_id, ...)` and `edit_note(context_id, ...)` when exposed. Inspect tool parameters; do not infer a destructive overwrite is safe.
- Related knowledge: `build_context`, `recent_activity`, or schema tools only if they are exposed **with trustworthy project scoping**. Never use a global operation that can cross project boundaries just because it is available.

**For all project-scoped tools:** pass `context_id`, not a client-chosen backend project name/UUID. Do not use unscoped search/fetch compatibility tools, an implicit default, or broad cross-project search.

## Binding diagnostics

- `resolve_context(context_id)` is read-only. Only `resolved` permits project-scoped memory operations.
- `not_bound`, `binding_inactive`, `project_routing_inactive`, `invalid_context`, tool errors, and unavailable endpoints mean no normal project-memory reads/writes.
- `list_projects` and `list_context_bindings`, when present, are read-only inventory tools. Neither provides a basis to choose a target without a trusted context and explicit binding.

## Administrative tools

The current planned gateway exposes `create_project`, `delete_project`, `bind_context`, and `unbind_context` to trusted clients. **Tool availability is not authorization.** Obtain explicit user approval for each intended management operation and inspect the live schema.

- `bind_context(context_id, project_id)`: project ID is an exact target identifier obtained from trusted inventory and operator approval; do not infer it.
- `unbind_context(context_id)`: physically removes the specified binding. Confirm before execution.
- `create_project(project_name, project_path)`: the current service requires an absolute path visible to its storage environment, distinct from the display name and ID.
- `delete_project(project_id, delete_notes)`: the current service requires an explicit decision about retaining or deleting the underlying notes. This may remove dependent bindings.

No unspecified deactivate/reactivate tools should be invented. Verify management results through read-only inspection.

## Trust boundary

The caller-provided `context_id` is a routing selector, **not** authentication or cryptographic authorization. The configured MCP endpoint is intended only for trusted agents and protected transports. Do not claim that unrelated or malicious clients are isolated merely because a context binding exists. Administrative MCP tools may be reachable to a trusted connected client; their exposure is not permission to use them.

The one globally exposed diagnostic tool, `basic_memory_diagnostics` (if present), is not project memory and does not accept a project context. Using it for connection diagnostics does not authorize unscoped project-data operations.
