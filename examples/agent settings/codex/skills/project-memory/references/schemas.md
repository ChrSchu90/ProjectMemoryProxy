# Note schema validation (PMP)

Use `bm_schema` through the public ProjectMemoryProxy endpoint with the same verified `context_id` as every other note tool. Inspect the live schema before calling. This is for **note content schemas**, not PMP context bindings or project lifecycle operations.

Basic Memory 1.0 uses Picoschema notes: a schema definition is itself a note with `type: schema` whose `entity` field identifies the note type it describes. Do not invent schema content or create schema notes without a reason grounded in the current project.

## Pick a command

- `bm_schema(context_id=..., command="validate", note_type="decision")`: check notes of one type against its schema. Optionally use `identifier` to validate just one note; a type with no available schema or no matching notes may return `NOT_FOUND`.
- `bm_schema(context_id=..., command="infer", note_type="decision")`: suggest a schema from existing notes. `threshold` (default `0.25`) is the fraction of notes in which a field must appear to be suggested. Inference is a proposal, **not** authorization to change a schema.
- `bm_schema(context_id=..., command="diff", note_type="decision")`: compare notes of one type with its defined schema to spot drift and newly used fields.

Use validation when diagnosing inconsistent note structure, inference when designing a convention based on real notes, and diff for schema-maintenance reviews. Read sample notes and the current schema before changing anything. Report warnings separately from errors; a report with warnings does not necessarily mean validation failed. Never claim schema changes were applied merely because an inference/diff returned results.

```text
bm_schema(context_id="<trusted-context>", command="validate", note_type="decision", output_format="json")
bm_schema(context_id="<trusted-context>", command="infer", note_type="decision", threshold=0.25)
```

The live PMP schema determines supported fields and response formatting; do not add native backend project selectors.
