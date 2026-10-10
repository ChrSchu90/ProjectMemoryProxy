# Canonical Git context identity

Read this reference when the `context_id` must be derived from a Git remote (Codex, OpenCode, Hermes, or an explicitly requested Git context in ChatGPT).

## Identification

1. Identify the actual repository containing the current task, including nested repositories and worktrees. Inspect its configured **fetch** remote `origin`; favor one verified, usable `origin` URL. For example, `git remote get-url --all origin` reports effective URLs. Inspect locally and **redact any credentials** before quoting/outputting; command output is not a safe secret store.
2. If `origin` is absent, unusable, or represents ambiguous destinations, stop routing and request an explicit technical context. Never infer identity from worktree or clone directory names, topics, repository aliases, or other project memories.
3. Normalize the selected remote to `git:<host>[:<port>]/<repository-path>`. Accept HTTPS, HTTP, SSH URLs and SCP-style SSH remotes. Remove URL scheme, username, password/token, query and fragment. Lowercase host only; preserve a meaningful non-default port.
4. Remove leading path slashes, collapse repeated path separators, remove a trailing slash, and remove **exactly one** trailing `.git` suffix (case-insensitive). Preserve all other repository path spelling and case unless authoritative hosting metadata establishes different canonical spelling.
5. Reject an empty host or path and ambiguous results. Never silently modify an already explicitly specified `context_id`. Never persist credentials from the remote.

## Examples

| Verified fetch remote | Context ID |
| --- | --- |
| `https://github.com/ChrSchu90/DevHatch.git` | `git:github.com/ChrSchu90/DevHatch` |
| `git@github.com:ChrSchu90/DevHatch.git` | `git:github.com/ChrSchu90/DevHatch` |
| `ssh://git@git.example.com:2222/group/repo.git` | `git:git.example.com:2222/group/repo` |

Multiple independently valid canonical Git IDs always require separate proxy context bindings, even when they intentionally route to the same persistent project-memory target. Do not use `list_projects` to guess which one to select.
