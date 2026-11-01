# Project-memory MCP interface

Read this reference when composing a memory tool call or diagnosing a missing binding. It describes the current **gateway contract**, not the storage backend. Actual MCP tool discovery and schemas are authoritative; capabilities can change.

## Project-scoped operations

- Search: `bm_search(context_id, ...)`, when exposed. Try query variations. For Basic Memory 1.0, use `mode` (e.g. `text`), `filters`, `note_types`, `items`, and `since` as the live schema permits; `projects` and `all_projects` are not public PMP arguments.
- Read: `bm_read(context_id, identifier, ...)`, preferably using an exact returned `file_path`, permalink or identifier; inspect `checksum` when safely editing or overwriting.
- Create/update: `bm_write(context_id, ...)` and `bm_edit(context_id, identifier, command, ...)` when exposed. Use `expected_checksum` for guarded overwrites/edits; do not infer destructive overwrite consent.
- Related knowledge: `bm_context`, `bm_list`, `bm_find`, or `bm_schema` when exposed **with trustworthy project scoping**. Basic Memory's `bm_project(command="recent")` is internal to PMP's lifecycle integration, not a public note tool. Never use a global operation that can cross project boundaries just because it is available.

**For all project-scoped tools:** pass `context_id`, not a client-chosen backend project name/UUID. Do not use unscoped search/fetch compatibility tools, an implicit default, or broad cross-project search.

## Binding diagnostics

- `resolve_context(context_id)` is read-only. Only `resolved` permits project-scoped memory operations.
- `not_bound`, `binding_inactive`, `project_routing_inactive`, `invalid_context`, tool errors, and unavailable endpoints mean no normal project-memory reads/writes.
- `list_projects` and `list_context_bindings`, when present, are read-only inventory tools. Neither provides a basis to choose a target without a trusted context and explicit binding.

## Administrative tools

The current gateway exposes `create_project`, `delete_project`, `bind_context`, and `unbind_context` to trusted clients. **Tool availability is not authorization.** Obtain explicit user approval for each intended management operation and inspect the live schema.

- `bind_context(context_id, project_id)`: project ID is an exact target identifier obtained from trusted inventory and operator approval; do not infer it.
- `unbind_context(context_id)`: physically removes the specified binding. Confirm before execution.
- `create_project(project_name, project_path)`: the current service requires an absolute path visible to its storage environment, distinct from the display name and ID.
- `delete_project(project_id, delete_notes)`: requires an explicit decision about retaining or deleting the underlying notes, and Basic Memory must have separately enabled its destructive `bm_project_delete` tool. This may remove dependent bindings; when upstream deletion is disabled, no deletion should be assumed.

No unspecified deactivate/reactivate tools should be invented. Verify management results through read-only inspection.

## Trust boundary

The caller-provided `context_id` is a routing selector, **not** authentication or cryptographic authorization. The configured MCP endpoint is intended only for trusted agents and protected transports. Do not claim that unrelated or malicious clients are isolated merely because a context binding exists. Administrative MCP tools may be reachable to a trusted connected client; their exposure is not permission to use them.

`bm_man` is globally exposed for read-only built-in documentation without a project context; it is not a project-data search or an authorization mechanism. The removed `basic_memory_diagnostics` tool must not be called.
