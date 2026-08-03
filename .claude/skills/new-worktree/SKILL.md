---
name: new-worktree
description: "Create a git worktree for a new piece of work and open it in a new VS Code window, without starting the work. Use when the user says they want to start a new worktree, spin up a parallel session, or work on something in a separate window."
argument-hint: "Short name for the work, e.g. 042-team-invites or fix/chat-scroll"
user-invocable: true
disable-model-invocation: false
---

# New worktree

Set up an isolated worktree so the user can start a **parallel Claude Code session**
in a second VS Code window. This session is the launcher, not the worker.

## The one rule

**Do not do the work.** The user is asking for an environment, not an implementation.
When this skill finishes, the current session has created a directory, opened a window,
and reported back — nothing else.

Concretely, in this session:

* **Do not call `EnterWorktree`.** It switches *this* session into the new worktree,
  which is the opposite of what was asked. Everything below uses plain git + the
  project hook instead.
* Do not read the feature's spec, plan, or issue "to get oriented".
* Do not query Graphify, inspect the affected code, or sketch an approach.
* Do not create a spec, plan, tasks, or branch beyond the one the hook makes.
* Do not commit anything in the new worktree.

If the user's message mixes a worktree request with an actual task
("make a worktree and fix the chat scrolling"), create the worktree, then **stop and
hand the task over** — say plainly that the fix belongs to the new session, and give
them the prompt to paste. They can always tell you to proceed here instead.

## Procedure

### 1. Resolve the main repo

This session may itself already be inside a worktree, so never assume the cwd is the
main checkout. The common git dir points at the real one:

```bash
git rev-parse --path-format=absolute --git-common-dir   # -> <main-repo>/.git
```

Strip the trailing `/.git` — that is `<main-repo>` for every command below.

### 2. Pick the name

Take it from the user's argument. If they gave a description rather than a name, derive
a short kebab-case slug and say which one you chose. Match the repo's existing branch
conventions (`041-community-guidelines-terms`, `fix/auth-switcher-overlap`) — check
`git -C <main-repo> branch -a` if unsure.

Each `/`-separated segment may contain only letters, digits, dots, underscores and
dashes; 64 characters total. Do **not** de-duplicate the name yourself — the hook
suffixes `-2`, `-3` … if the branch already exists.

### 3. Refresh the base

```bash
git -C "<main-repo>" fetch origin --quiet
```

The hook branches from `origin/HEAD` (falling back to `origin/main`, `origin/master`,
then local `HEAD`). It does not fetch, so without this the new branch starts from a
stale remote ref.

### 4. Create the worktree

Reuse the project's `WorktreeCreate` hook — do not hand-roll `git worktree add`. The
hook is a plain node script with a stdin/stdout contract, so it can be called directly:

```bash
printf '{"cwd":"<main-repo>","name":"<name>"}' \
  | node "<main-repo>/.claude/hooks/worktree-create-with-env.js"
```

It prints the absolute path of the new worktree to stdout, and only that. It owns three
things this skill must not duplicate:

* the layout — worktrees are siblings at `<repo>.worktrees/<name>/`, deliberately
  outside the repo so tools that walk the tree don't index a second copy of the codebase;
* the base ref and branch-name collision handling;
* seeding `.env`, which `git worktree add` never copies because it is untracked.

Treat empty stdout or a non-zero exit as a hard failure: report the stderr line and stop.
Do not fall back to a bare `git worktree add` — that silently produces a worktree with no
`.env`, which fails later and confusingly.

### 5. Open the window

```bash
code -n "<worktree-path>"
```

`-n` forces a **new** window; without it VS Code may reuse the current one and pull the
user out of the session they are already in.

### 6. Verify before reporting

Check, don't assume:

* the path exists and `git -C "<worktree-path>" status -sb` shows the expected branch;
* `.env` is present in the new worktree;
* `code` exited 0.

If `.env` is missing, say so — the new session will fail at runtime otherwise.

### 7. Report and stop

Give the user:

* the worktree path and branch name,
* the base commit it branched from (`git -C "<worktree-path>" log -1 --oneline`),
* a ready-to-paste opening prompt for the new session, phrased as the task they described.

Mention, once, that the new branch's upstream is `origin/main` — `git worktree add -b <branch> origin/main` sets tracking to the base ref, so `git status` in the new worktree reads `## <branch>...origin/main`. Git's default `push.default = simple` refuses a bare `git push` when the upstream name differs, so the first push there needs `git push -u origin <branch>`.

Then stop. Do not offer to start the work in this session; if they want that, they will
say so.

## Cleanup

Not part of this skill, but when the user later asks to remove a finished worktree:

```bash
git -C "<main-repo>" worktree remove "<worktree-path>"
git -C "<main-repo>" branch -d "<branch>"    # only after it is merged
```

Confirm first — the worktree may hold uncommitted work, and `.env` lives there untracked.

On Windows `git worktree remove` regularly reports `failed to delete … Permission denied`
while still de-registering the worktree, leaving the directory behind. When that happens,
delete the leftover directory (`Remove-Item -Recurse -Force`) and run
`git -C "<main-repo>" worktree prune`; verify with `git worktree list` rather than
trusting the remove command's exit.
