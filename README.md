# JuggerHub

The JuggerHub repository, with a Docker-based development setup, CI/CD workflows,
and an integrated **AI-assisted development toolchain** for use with Claude Code.

The full workflow rules and routing live in [CLAUDE.md](CLAUDE.md) — read that
first. This README focuses on **what each tool is, how it runs, and where its UI
is** (if any).

---

## Structure

```
├── backend/              # .NET 10 API (JuggerHub.Api: Controllers/Services/Entities/Data/Dtos/Common, tests/, Dockerfile)
├── frontend/             # Nx + Angular workspace (apps/web, apps/web-e2e, nginx.conf, Dockerfile)
├── .specify/             # Spec-Kit: specs, plans, tasks, constitution (source of truth)
├── backlog/              # Backlog.md: tasks, drafts, decisions, docs
├── .claude/              # Claude Code: Spec-Kit skills + settings.json
├── .githooks/            # Git hooks: auto-rebuild Graphify graph on commit/pull/checkout
├── .github/workflows/    # CI/CD pipelines
├── CLAUDE.md             # Workflow rules & tool routing — read first
├── DESIGN.md             # Visual identity / design tokens (UI source of truth)
├── .claudeignore         # Context-exclusion hints (see Security & ignore rules)
└── docker-compose*.yml   # Local development orchestration
```

---

## AI Development Toolchain

Five tools cooperate under the rules in [CLAUDE.md](CLAUDE.md). In one line:

> **Spec-Kit** decides · **DESIGN.md** styles · **Backlog.md** queues · **Graphify** maps · **claude-mem** remembers.

| Tool | Version | Role | How it runs | Dedicated UI |
|------|---------|------|-------------|--------------|
| **Spec-Kit** | 0.11.9 | Specs, plans, tasks, constitution | On-demand CLI + `/speckit-*` slash commands | — |
| **DESIGN.md** (design.md) | 0.3.0 | Visual identity / design tokens | On-demand CLI (`designmd`) + the `DESIGN.md` file | — |
| **Backlog.md** | 1.47.1 | Intake & prioritization (Kanban) | On-demand CLI | ✅ Web UI + terminal Kanban |
| **Graphify** | 0.9.1 | Codebase knowledge graph / impact analysis | 🔄 **Auto-rebuild on git ops** + on-demand CLI / `/graphify` | ✅ Interactive graph (HTML) |
| **claude-mem** | 13.8.1 | Cross-session memory | 🔄 **Background worker (auto-starts)** + lifecycle hooks | ✅ Web viewer |

Implementation is executed directly (task-by-task with small commits and
verification) or, for a Spec-Kit `tasks.md`, via the `/speckit-implement` skill.

---

### 🔄 Runs automatically in the background

These need no manual start once Claude Code is open in this project:

- **claude-mem worker** — a persistent service on **`http://localhost:37777`**.
  It **auto-starts on every Claude Code launch** via the plugin's `SessionStart`
  hook (also on `/clear` and `/compact`), and captures observations passively as
  Claude reads, edits, and runs commands. Its lifecycle hooks
  (`UserPromptSubmit`, `PostToolUse`, `PreToolUse:Read`, `Stop`) also fire
  automatically. Data lives in `~/.claude-mem` (outside the repo).
  - Manual controls if needed: `npx claude-mem status` · `start` · `stop` · `restart` · `doctor`.
  - It only runs while Claude Code is open (which is all that's needed for memory
    capture). For an always-on viewer, add a logon task that runs `claude-mem start`.
- **Graphify auto-rebuild** — tracked git hooks in `.githooks/` run
  `graphify update .` (fast, no-LLM) in the background after **commit, pull,
  branch switch, and rebase**, keeping `graphify-out/` fresh. Editor-agnostic and
  non-blocking; single-flighted via a PID lock; logs to `graphify-out/.rebuild.log`.
  Enable once per clone with `git config core.hooksPath .githooks` (see Setup).
  Community *labels* still need an occasional full build — see the on-demand list.

### 🖥️ Tools with a dedicated UI

- **claude-mem — web viewer:** `http://localhost:37777` (live stream of captured
  observations). Open it in a browser while working.
- **Backlog.md — web UI & terminal board:**
  - `backlog browser` → web Kanban/task UI on **port 6420** (auto-opens browser).
  - `backlog board` → Kanban board in the terminal.
  - Both are launched on demand and run only while open.
- **Graphify — interactive graph:** building the graph writes
  **`graphify-out/graph.html`** (an interactive visualization), plus
  `GRAPH_REPORT.md` and `graph.json`. Open the HTML in a browser. `graphify-out/`
  is gitignored.

### ⌨️ On-demand: CLIs & Claude slash commands

No background process; invoke when needed.

- **Spec-Kit** — `specify` CLI; in Claude: `/speckit-constitution`,
  `/speckit-specify`, `/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`,
  `/speckit-analyze`, `/speckit-checklist`, `/speckit-implement`,
  `/speckit-converge`.
- **Graphify** — `graphify .` (full build, with LLM community labels),
  `graphify update .` (fast re-extract, no LLM — what the git hooks run),
  `graphify label .` (re-name clusters), `graphify watch <path>` (live rebuild on
  file changes, foreground), `graphify explain "X"`, `graphify path "A" "B"`; in
  Claude: `/graphify`. Run a full build occasionally to refresh community labels.
- **Backlog.md** — `backlog task|draft|doc|decision|milestone`, `backlog search`,
  `backlog overview`, `backlog instructions <guide>`. (Also exposes `backlog mcp`.)
- **design.md** — `designmd lint DESIGN.md`, `designmd diff`, `designmd export`
  (Tailwind / W3C tokens). *Note: `designmd spec` is broken upstream in v0.3.0.*

---

## Setup on a new machine

Global CLIs and the Claude plugin are **not** committed, so after cloning,
reinstall the toolchain. Requires **Node 20+**, **uv**, and **git**.

```bash
# Global CLIs
npm  install -g backlog.md @google/design.md
uv   tool install specify-cli --from git+https://github.com/github/spec-kit.git@v0.11.9
uv   tool install graphifyy && graphify install --platform claude

# Claude Code plugin (registers + enables; worker auto-starts thereafter)
npx claude-mem install

# Enable Graphify auto-rebuild git hooks (tracked in .githooks/)
git config core.hooksPath .githooks
```

Committed config that *does* travel with the repo: `.specify/`, `backlog/`,
`.claude/skills/` (Spec-Kit) and `.claude/settings.json`, `.githooks/`,
`CLAUDE.md`, `DESIGN.md`, `.claudeignore`.

---

## Security & ignore rules

- **`.gitignore`** — respected by Claude Code's file discovery by default. Also
  excludes machine-specific/generated tooling output (`.claude/settings.local.json`,
  `graphify-out/`).
- **`.claudeignore`** — soft "don't auto-pull into context" hints (deps, build
  output, logs, secrets, cruft). ⚠️ Claude Code does **not** natively enforce this
  yet — it's a shared convention / honored by some hooks and other agents.
- **`permissions.deny` in `.claude/settings.json`** — the **enforced** block: Claude
  cannot read `.env*`, `*.pem/key/pfx/p12/crt`, `secrets/`, `~/.ssh`, `~/.aws`, or
  .NET user-secrets. This is what actually protects credentials.

Never store secrets in code, specs, Backlog.md, Graphify, or claude-mem.

---

## Running the application & tests (Docker-only)

The application and **all** tests run in containers — there is no host-level
dev-server workflow (no `ng serve`) and no host .NET/Node runtime is required.
You only need **Docker + Docker Compose**.

### Start the stack

```pwsh
Copy-Item .env.sample .env      # review values; defaults work for local
docker compose up -d --build    # database, backend, frontend, mailpit
docker compose ps               # all services Up; backend/frontend become healthy
docker compose down             # stop (add -v to also drop the db volume)
```

The backend **auto-applies EF Core migrations on startup** against the (initially
empty) PostgreSQL 18 database — no manual migration step.

| Surface | URL |
|---------|-----|
| Web app (dashboard) | http://localhost:3000 |
| API (same-origin via the app proxy) | http://localhost:3000/api/v1/health |
| API (direct) | http://localhost:8080/api/v1/health |
| **Scalar** API reference (Development only) | http://localhost:8080/scalar/v1 |
| Mailpit (local email capture) | http://localhost:8025 |

> Interactive API docs are served by **Scalar** over the built-in
> `Microsoft.AspNetCore.OpenApi` document (no Swagger UI), and only in the
> Development environment.

### Authentication (try it locally)

The first product feature is **email + password authentication** (spec:
[`specs/002-authentication`](specs/002-authentication)). Everything is enforced
server-side; access/refresh tokens live only in `httpOnly` cookies (never
`localStorage`). With the stack up, you can run the whole cycle locally:

1. **Register** at http://localhost:3000/register — the password meter shows the
   live policy (8+ chars, upper/lower/digit/symbol, 3 unique). You land on a neutral
   "check your email" screen.
2. **Verify** — open the **Mailpit inbox** at http://localhost:8025, click the link
   in the "Verify your email" message. (Sign-in is **blocked until verified**.)
3. **Sign in** at http://localhost:3000/sign-in (with optional "remember me"). Wrong
   credentials return a single generic error; 5 failures lock the account for 15 min.
4. **Sign out** from the top nav — the session is revoked server-side and cookies cleared.
5. **Forgot / reset** via http://localhost:3000/forgot-password → open the reset link
   in Mailpit → set a new password. Existing sessions are invalidated and a
   change-notification email is sent.

Endpoints live under `/api/v1/auth/*` (see
[`contracts/openapi.yaml`](specs/002-authentication/contracts/openapi.yaml)).
Registration, forgot-password, and resend-verification are **enumeration-neutral**
(identical responses whether or not the account exists). Local mail is captured by
**Mailpit**; deployed environments send via **Resend** (selected by `Email__Provider`).

### Run the test suites — all in containers

A `docker-compose.test.yml` overlay runs each suite with no host runtimes:

```pwsh
$test = "docker compose -f docker-compose.yml -f docker-compose.test.yml"

iex "$test run --rm backend-test"    # xUnit + Testcontainers (real Postgres)
iex "$test run --rm frontend-test"   # Jest unit tests
iex "$test run --rm playwright"      # Playwright e2e (desktop + mobile) vs the running stack
```

`backend-test` mounts the Docker socket so Testcontainers can spin up a sibling
Postgres; `playwright` targets the running `frontend` container, so start the
stack first.

---

## Documentation

- [CLAUDE.md](CLAUDE.md) — workflow rules, source-of-truth priority, tool routing.
- [DESIGN.md](DESIGN.md) — colors, typography, spacing, components.
- `.specify/memory/constitution.md` — project principles (Spec-Kit).
- `backlog instructions overview` — Backlog.md workflow guidance.
