# Search, navigation, and related context (PMP)

Use these public **ProjectMemoryProxy** tools only after establishing the exact trusted `context_id`. This is a usage guide based on Basic Memory 1.0 behavior, not the upstream MCP contract; inspect each tool's live PMP schema before calling it. Every example must include the same validated `context_id`. Never supply `project`, `project_id`, `projects`, `all_projects`, tenant or workspace selectors; PMP injects the bound project UUID.

## Choose the smallest suitable tool

- `bm_search`: ranked text, title, permalink, or semantic retrieval of notes, observations, and relations. Best for questions about content and decisions.
- `bm_list`: list **one directory level** inside the bound project (`path="/"` is its root). Use `glob` to filter filenames; continue using pagination.
- `bm_find`: **recursive** file-name glob (`name`) or frontmatter metadata search (`filters`) under a project-relative `path`. Do not combine `name` and `filters`; `fields` is meaningful only with `filters`. For full-text or meaning search, use `bm_search` instead.
- `bm_context`: follow a note's observations and relations (`depth` 1–5). Use `compact=true` for a small graph first, then `bm_read` for selected full notes.

## Ranked search

- Prefer a narrow `bm_search(query="exact phrase", mode="text", context_id=...)` when wording is known. Try related terms and abbreviations; quote exact phrases where needed. `query` supports `AND`, `OR`, `NOT`, and `tag:name` syntax.
- Use `mode="semantic"` for meaning-based retrieval and `mode="hybrid"` for combined retrieval **only if supported by the deployed backend**. A semantic/hybrid hit need not contain the literal query; verify its content with `bm_read`.
- Use `mode="title"` or `mode="permalink"` for identifier-oriented lookup (permalink mode supports `*` globbing). Use `items=["note"]` when the user wants whole notes rather than observation/relation matches.
- Restrict within the already bound project with `path`, `note_types`, `since`, or `filters`, e.g. `filters={"status":"active", "tags":["architecture"]}`. Metadata filter operators such as `$in`, `$contains`, and `$exists` are supported where the live schema permits them.
- Use `lines=0` for exact matching lines or `lines=2` for nearby context (`lines` 0–10); `max_matches` applies only with `lines` or a single-note `identifier`. File line numbers include YAML frontmatter.
- For authored valid-time questions, use `valid_at`/`valid_overlaps` only when the note data actually contains authored validity, and do not confuse this with file timestamps.

## Pagination and avoiding false negatives

- Check `has_more`, `page`, and `page_size` and request subsequent pages when needed. Do not conclude that an empty page proves no matches remain if `has_more=true`.
- With `lines`, results may omit ranked notes that have no matching line; `total_is_exact=false` means the total counts ranked candidates, not guaranteed matching lines.
- `bm_list` is shallow, `bm_find` is recursive; neither is a substitute for full-text search. Read important search hits by exact returned path, permalink, or identifier before citing or editing them.
- `bm_context` defaults to `since="7d"` for related material; explicitly widen it when older relations matter. Use its `page`/`page_size` for primary results and `max_related` to control fan-out.

## Scoped examples

```text
bm_search(context_id="<trusted-context>", query="cache migration", mode="text", items=["note"], page=1, page_size=10)
bm_find(context_id="<trusted-context>", path="decisions", filters={"status":"active"})
bm_list(context_id="<trusted-context>", path="notes", glob="*.md")
bm_context(context_id="<trusted-context>", identifier="decisions/cache.md", compact=true, depth=1)
```

Treat examples as illustrations; tool schemas are authoritative, including how the host represents booleans and structured arguments.
