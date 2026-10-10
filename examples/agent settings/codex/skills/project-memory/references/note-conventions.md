# Durable note conventions

These are **content quality guidelines**, not required backend syntax. Use the notation below only when the connected project-memory service supports it.

## Language invariant

All new durable notes must be in English, including titles, headings, prose, observations, and human-readable metadata. Newly edited prose must also be in English. Preserve exact technical identifiers and relevant original quotes. Do not silently bulk-translate legacy notes.

## Useful note anatomy

- A stable, descriptive title for one coherent subject
- Context: the relevant background and why the note exists
- Current decisions and their rationale, including important tradeoffs
- Verified status/facts and their evidence or as-of date
- Open questions, blockers and specific next steps where appropriate
- Optional structured observations and links to closely related **existing** notes

Notes should be readable as standalone reference documents. Prefer current, synthesized material over chronological conversation logs. Avoid gratuitous subdivisions, repeated project plans, empty template sections or needless schemas.

## Optional structured observations

Where supported, use one specific fact per observation:

```markdown
## Observations
- [decision] Run the service in a VM for the first deployment #architecture
- [constraint] Keep internal service endpoints private #security
- [finding] The new endpoint is reachable after a service restart #operations
- [risk] Tunnel and MCP path configuration must match #deployment
```

Other useful categories include `[tradeoff]`, `[requirement]`, `[status]`, and `[next_step]`. Do not invent a fact just to create an observation.

## Optional relations

Where supported, use a small number of meaningful existing-note links:

```markdown
## Relations
- depends_on [[Runtime Deployment]]
- implements [[Approved Architecture]]
- relates_to [[Client Migration Checklist]]
```

Avoid cross-project links that imply visibility outside the authorized context. If the target is not known to exist in the current project, do not create placeholder notes merely to satisfy a relation.

## Search and updating

- Search for the concept under its full title, abbreviations and alternative names before creating it.
- Metadata filters are useful for status, tags or task type when the service exposes them; ordinary content queries are better served by text search.
- Read the exact existing note immediately before changing it. Make the smallest coherent update. For mutable facts, replace stale text; for changes of direction, retain concise rationale where historically useful.
- Do not overwrite a note built from an old snapshot of a concurrent working session.
