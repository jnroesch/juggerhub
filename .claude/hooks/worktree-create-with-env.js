#!/usr/bin/env node
/**
 * WorktreeCreate hook.
 *
 * This event REPLACES Claude Code's built-in worktree creation: it is handed the
 * requested name, and must create the worktree itself and echo the resulting path
 * to stdout. Returning nothing fails the whole EnterWorktree call.
 *
 * Two deliberate differences from the built-in behaviour:
 *
 *  1. Worktrees are created as SIBLINGS of the repo (`<repo>.worktrees/<name>`)
 *     rather than inside `.claude/worktrees/`, per the note in .gitignore: tools
 *     that walk the tree from the repo root do not all honour .gitignore, so an
 *     in-repo worktree gets picked up as a second copy of the codebase. Trade-off:
 *     EnterWorktree's mid-session switch-by-path expects `.claude/worktrees/`, so
 *     switching between existing worktrees may not work — creation and entry do.
 *  2. The files in SEED_FILES are copied across. A git worktree only checks out
 *     TRACKED files, so gitignored local config such as .env never comes along.
 *
 * stdin: {"cwd": "<main repo>", "name": "<requested worktree name>", ...}
 * stdout: absolute path of the created worktree
 */
const { execFileSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const SEED_FILES = ['.env'];

const git = (repo, ...args) =>
  execFileSync('git', ['-C', repo, ...args], { encoding: 'utf8' }).trim();

/** origin/<default-branch>, falling back to local HEAD when there is no usable remote. */
function baseRef(repo) {
  for (const resolve of [
    () => git(repo, 'symbolic-ref', '--short', 'refs/remotes/origin/HEAD'),
    () => (git(repo, 'rev-parse', '--verify', '--quiet', 'origin/main'), 'origin/main'),
    () => (git(repo, 'rev-parse', '--verify', '--quiet', 'origin/master'), 'origin/master'),
  ]) {
    try {
      const ref = resolve();
      if (ref) return ref;
    } catch {
      /* try the next candidate */
    }
  }
  return 'HEAD';
}

/** The requested name, suffixed until it is not already taken by a branch. */
function freeBranch(repo, name) {
  const exists = (b) => {
    try {
      git(repo, 'show-ref', '--verify', '--quiet', `refs/heads/${b}`);
      return true;
    } catch {
      return false;
    }
  };
  if (!exists(name)) return name;
  for (let n = 2; ; n++) {
    if (!exists(`${name}-${n}`)) return `${name}-${n}`;
  }
}

let raw = '';
process.stdin.on('data', (c) => (raw += c));
process.stdin.on('end', () => {
  try {
    const { cwd, name } = JSON.parse(raw);
    const repo = cwd || process.env.CLAUDE_PROJECT_DIR;
    if (!repo || !name) throw new Error(`missing cwd/name in payload: ${raw}`);

    const target = path.join(path.dirname(repo), `${path.basename(repo)}.worktrees`, name);
    const branch = freeBranch(repo, name);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    git(repo, 'worktree', 'add', target, '-b', branch, baseRef(repo));

    for (const file of SEED_FILES) {
      const from = path.join(repo, file);
      const to = path.join(target, file);
      if (fs.existsSync(from) && !fs.existsSync(to)) {
        fs.mkdirSync(path.dirname(to), { recursive: true });
        fs.copyFileSync(from, to);
      }
    }

    process.stdout.write(target);
  } catch (err) {
    // Fail loudly: a silent failure here leaves the session with no worktree.
    process.stderr.write(`worktree-create-with-env: ${err.message}\n`);
    process.exit(1);
  }
});
