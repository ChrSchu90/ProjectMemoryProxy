# Read, write, edit, move, and delete notes (PMP)

Operate exclusively through the public ProjectMemoryProxy `bm_*` tools with the **exact validated `context_id`**. Never supply Basic Memory project/workspace selectors or call the upstream endpoint. Follow the approval and English-language rules in `SKILL.md`; `context_id` is routing, not authentication.

## Read precisely before acting

- `bm_read(context_id, identifier, ...)` accepts an exact path, permalink, `memory://` URL, exact title, or note ID. Prefer the `file_path`/permalink returned by search instead of guessing from a title. Missing targets return `NOT_FOUND`; search first rather than assuming fuzzy read fallback.
- Whole-note reads include YAML frontmatter by default. Use `include_frontmatter=false` if only the body is needed, or `include_content=false` with `output_format="json"` for metadata, paths, `checksum` and update timestamp.
- For long Markdown notes read `section`, `start_line`/`end_line`, or `max_tokens`; follow returned continuation coordinates. Line numbers include YAML frontmatter. For non-Markdown text files use line ranges; other binary files are returned whole where supported.
- Read the current note immediately before any change; save the returned revision `checksum` when available.

## Creating and replacing notes

- `bm_write(context_id, content, identifier="folder/file.md", ...)` targets an exact project-relative file; alternatively use `title` plus `directory`. The `title:` frontmatter takes precedence over the `title` argument; an H1 Markdown heading alone does not set the stored note title.
- Prefer creating without overwrite. An existing target returns `ALREADY_EXISTS` unless overwrite is explicitly enabled by the live tool/backend configuration. Never silently replace it.
- For a specifically authorized whole-note replacement, use the **exact path from a fresh read**, `overwrite=true`, and `expected_checksum` to prevent overwriting a concurrent update. Prefer `bm_edit` for focused changes. Read back meaningful writes.
- Supported Markdown observation and relation notation: `- [decision] ... #tag` and `- depends_on [[Existing Note]]`. Indexing may finish after the write response; do not treat an immediate missing search hit as proof the write failed.

## Editing in place

- `bm_edit` selects an operation with `command`, **not** `operation`: `append`, `prepend`, `find_replace`, `replace_section`, `insert_before_section`, `insert_after_section`, or `set_metadata`.
- For exact replacement, provide `find_text` and the expected number of occurrences (`expected_replacements`, default 1). Do not replace ambiguous occurrences without reviewing the note first.
- For section operations, name an actual heading (for example `Decisions`, `Auth/Decisions`, or `Decisions[1]`). `replace_section` includes subsections by default (`replace_subsections=true`); set false only when the scope must stop at the first nested heading. Do not use `section` with plain `append`.
- `set_metadata` merges frontmatter fields; identity fields such as `title` and `permalink` cannot be changed that way. Use a supported move/rename operation where appropriate.
- Supply `expected_checksum` for revision-guarded edits when available; re-read and merge deliberately if `REVISION_CONFLICT` occurs.

## Move and delete with care

- `bm_move(context_id, identifier, destination, ...)` moves a note **within the same bound project**. A trailing slash in `destination` means move into that directory under the existing filename; a renamed note destination needs a filename extension. `path` selects a directory move; some files may move before an error, so inspect both source and destination before retrying.
- `bm_delete(context_id, identifier, ...)` deletes a note. `path` instead selects a directory; directory deletion requires `recursive=true` and removes its contents. There is no undo. Require explicit approval for destructive actions and use `expected_checksum` for guarded note deletion when possible.
- Never simulate a cross-project move by reading, writing in a different context, and deleting: that would bypass the configured isolation and administrative approval boundary. Project creation/deletion and bindings are **separate PMP control-plane operations**.

## Scoped examples

```text
bm_read(context_id="<trusted-context>", identifier="notes/plan.md", include_content=false, output_format="json")
bm_write(context_id="<trusted-context>", identifier="notes/new-plan.md", content="# New Plan\n...")
bm_edit(context_id="<trusted-context>", identifier="notes/plan.md", command="find_replace", find_text="old wording", content="revised wording", expected_replacements=1, expected_checksum="<checksum>")
```

Use the actual tool schema to construct calls. Never invent a checksum or reuse one from an outdated read.
