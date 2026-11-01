# Project-memory MCP interface

Read this reference when composing a PMP memory tool call or diagnosing a missing binding. It describes **ProjectMemoryProxy's public contract**, not the unrestricted Basic Memory server. MCP discovery and the tool's live input schema are authoritative; capabilities can change.

## Project-scoped operations

- **Find and navigate:** `bm_search(context_id, ...)`, `bm_list(context_id, ...)`, `bm_find(context_id, ...)`, and `bm_context(context_id, ...)` when exposed. See [Search and navigation](search.md) for search modes, metadata filters, result paging, and graph depth.
- **Read and change notes:** `bm_read(context_id, identifier, ...)`, `bm_write(context_id, ...)`, `bm_edit(context_id, identifier, command, ...)`, `bm_move(context_id, ...)`, and `bm_delete(context_id, ...)` when exposed. See [Note operations](notes.md) for exact identifiers, optimistic checksums, section edits, overwrite policy, and destructive actions.
- **Validate content structure:** `bm_schema(context_id, command, ...)` when exposed. See [Note schemas](schemas.md) for validation, inference and drift analysis.

**Every project-scoped tool:** use the exact validated `context_id`, never a caller-selected backend project name, UUID, workspace, tenant or all-project selector. PMP supplies the bound project UUID. Native global documentation, compatibility and internal project-management operations are not part of the public PMP note-tool surface; consult these task-specific references instead. See [Errors and recovery](errors.md) when a call fails.

## Binding diagnostics

- `resolve_context(context_id)` is read-only. Only `resolved` permits project-scoped memory operations.
- `not_bound`, `binding_inactive`, `project_routing_inactive`, `invalid_context`, tool errors, and unavailable endpoints mean no normal project-memory reads/writes.
- `list_projects` and `list_context_bindings`, when present, are read-only inventory tools. Neither provides a basis to choose a target without a trusted context and explicit binding.

## Administrative tools

The current PMP gateway exposes `create_project`, `delete_project`, `bind_context`, and `unbind_context` to trusted clients. **Tool availability is not authorization.** Obtain explicit user approval for each intended management operation and inspect the live schema.

- `bind_context(context_id, project_id)`: project ID is an exact target identifier obtained from trusted inventory and operator approval; do not infer it.
- `unbind_context(context_id)`: physically removes the specified binding. Confirm before execution.
- `create_project(project_name, project_path)`: requires an absolute path visible to the storage environment, distinct from the display name and ID.
- `delete_project(project_id, delete_notes)`: requires an explicit decision on retaining or deleting note files; backend deletion must be enabled for the server to perform it. Do not assume that a failed upstream deletion removed routing or bindings.

No unspecified deactivate/reactivate tools should be invented. Verify management results through read-only inspection.

## Trust boundary

The caller-provided `context_id` is a routing selector, **not** authentication or cryptographic authorization. This MCP endpoint is intended for trusted clients and protected transports. Do not claim unrelated or malicious clients are isolated merely because a context binding exists. Administrative tools may be reachable to a trusted connected client; their exposure is not permission to use them.

The legacy diagnostics tool was removed in Basic Memory 1.0; do not invent replacement note tools for health or troubleshooting.
