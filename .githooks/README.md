# Git hooks — Graphify auto-rebuild

Tracked git hooks that keep the [Graphify](https://github.com/safishamsi/graphify)
code knowledge graph fresh automatically.

## What they do

After a HEAD-advancing git operation, the matching hook launches
`graphify update .` (the fast, **no-LLM** re-extraction) in a **detached
background process**, so your git command returns immediately and the graph in
`graphify-out/` refreshes a moment later.

| Hook | Fires on |
|------|----------|
| `post-commit`   | every commit |
| `post-merge`    | `git merge` and `git pull` |
| `post-checkout` | branch switch (file checkouts are ignored) |
| `post-rewrite`  | `git rebase`, `git commit --amend` |

All four delegate to `graphify-rebuild.sh`, which:
- never blocks or fails a git operation (always exits 0),
- single-flights via a PID lock (`graphify-out/.rebuild.lock`) so rapid commits
  don't stack rebuilds,
- is a quiet no-op if `graphify` isn't installed.

Build output and logs land in `graphify-out/` (git-ignored). Rebuild logs:
`graphify-out/.rebuild.log`.

## Enabling (once per clone)

Git does not allow a repo to set its own hooks path automatically (by design), so
each clone must opt in once:

```bash
git config core.hooksPath .githooks
```

## Notes

- **Community labels:** `update` skips LLM community naming, so labels stay as
  `Community N`. For named clusters, occasionally run a full build with an LLM
  backend configured: `graphify .` (or `graphify label .`). Set `GEMINI_API_KEY`
  / `GOOGLE_API_KEY` for semantic extraction.
- **Disable temporarily:** `git config --unset core.hooksPath` (reverts to
  `.git/hooks`), or `git commit --no-verify` to skip for a single op.
