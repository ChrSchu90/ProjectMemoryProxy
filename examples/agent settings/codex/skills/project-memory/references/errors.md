# Project-memory failures and safe recovery

Treat an MCP transport success (for example HTTP 200) and a successful **tool operation** as different things. Inspect `isError`, returned structured content, and any diagnostic text; Basic Memory 1.0 reports application refusals as MCP tool errors. Do not treat an empty result or an `isError` reply as a successful mutation.

## Context and trust failures

- Invalid, unknown, inactive, ambiguous or unbound `context_id` means **stop** memory operations in that context. Never fall back to a different context, a default project, an unrestricted native Basic Memory endpoint, or a manually chosen project UUID.
- If discovery or a tool is unavailable, inspect the configured PMP tool list and live input schema. Do not guess renamed parameters or call internal project-management, manual or compatibility tools to work around restrictions.
- Authentication/connection errors are distinct from unknown-context errors. Report the blocker and continue the non-memory task where possible; do not manufacture a success confirmation.

## Common Basic Memory 1.0 refusals

| Code | Meaning | Safe next step |
| --- | --- | --- |
| `NOT_FOUND` | Exact note, schema or target is missing | Search/list within the same validated context; create only if the task calls for it |
| `ALREADY_EXISTS` | A create would overwrite an existing note | Read the exact target and decide whether to edit or ask for overwrite approval |
| `REVISION_CONFLICT` | The note changed since its checksum was read | Re-read, reconcile concurrent edits, then retry with a **fresh** checksum only if still authorized |
| `INVALID_ARGUMENTS` | Unsupported arguments, contradictory scope, wrong heading or incomplete command | Check the public PMP schema and the target note; correct only the scoped call |
| `OUTSIDE_PROJECT` | Path escapes the selected project | Use a project-relative path already belonging to the bound context; do not bypass routing |
| `AMBIGUOUS` | Identifier matches more than one target | Resolve to one exact path/permalink within the same project |
| `UNSUPPORTED` | Operation disabled by backend or server policy | Stop; do not bypass PMP or enable a destructive upstream option |

Error payloads may include `error`, `message`, and `try_instead`; treat suggestions as informational, not authorization to change the target context or use an unexposed tool. The live result format remains authoritative.

## Verify outcomes

- Following a create/edit/move/delete, inspect the tool result for errors and verify meaningful changes with a scoped read or list. If an indexing delay is plausible, verify the authoritative read before relying on search indexing.
- For `bm_edit` replace/section operations, distinguish `NOT_FOUND` from `REVISION_CONFLICT`. A missing section is not permission to append somewhere else without reviewing the note.
- On partially completed directory moves, list both affected paths before any retry; never assume an all-or-nothing rollback.
- Context binding and project deletion are **control-plane** changes: require operator approval and verify returned state with the PMP read-only management tools.
