<div align="center">

# 🏐 JuggerHub

**A warm, community-run home for the sport of Jugger** — find teams, book
training, follow matches, and start local groups.

[![Live (dev)](https://img.shields.io/badge/live-dev.juggerhub.com-16a34a.svg)](https://dev.juggerhub.com/)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Backend: .NET 10](https://img.shields.io/badge/backend-.NET%2010-512BD4.svg)](backend/)
[![Frontend: Angular + Nx](https://img.shields.io/badge/frontend-Angular%20%2B%20Nx-DD0031.svg)](frontend/)
[![Runs on: Docker](https://img.shields.io/badge/runs%20on-Docker-2496ED.svg)](docker-compose.yml)

[Report a bug · Request a feature · Leave feedback](https://github.com/jnroesch/juggerhub/issues/new/choose)
· [Contributing](CONTRIBUTING.md)
· [Security](SECURITY.md)

</div>

---

## What is JuggerHub?

[Jugger](https://en.wikipedia.org/wiki/Jugger) is a fast, full-contact team sport
played with padded weapons ("pompfen") and a running discipline all of its own — a
grassroots scene that has always run on word-of-mouth, group chats, and shared
spreadsheets. **JuggerHub replaces that patchwork with one warm
home** where players and teams can find each other, organize, and stay in touch
without the friction.

One account gives you everything below:

- **Your player profile** — a shareable public page for who you are and what you play.
- **Teams** — create or join one, manage the roster, and run a public team page
  that lets new players request to join.
- **Events, parties & the mercenary market** — post events, and for team events
  form a **party** (a temporary crew for one event) or pick up free-agent
  **mercenaries** from the event marketplace when you're short a player.
- **Trainings** — team-scoped recurring sessions with per-session Going / Maybe /
  Can't responses and a "your trainings" agenda on your dashboard.
- **Chat** — built-in 1:1, group, team, and party conversations with live typing,
  read state, and link unfurls — no more scattered group chats.
- **Search & discovery** — browse and search teams, players, and events near you.
- **Notifications** — an in-app alerts inbox with per-user preferences.
- **Recognition** — admin-granted badges and achievements on profiles and teams.
- **Secure by default** — email + password auth with verified email, password
  reset, and lockout, all enforced server-side.

> **Status:** JuggerHub is in active **pre-1.0** development. Expect rapid change.
> A live development environment runs at **[dev.juggerhub.com](https://dev.juggerhub.com/)**;
> the public production site (**juggerhub.com**) is not deployed yet.

<!-- TODO: add screenshots or a short demo GIF here once the UI is stable. -->

---

## Contributing & feedback

Contributions of all kinds are welcome — and **you don't need to be a developer**
to help shape JuggerHub.

- 🐞 **Found a bug?** · 💡 **Have an idea?** · 💬 **Just want to share feedback?**
  → [Open an issue](https://github.com/jnroesch/juggerhub/issues/new/choose).
  The guided forms need no Markdown or git knowledge.
- 🛠️ **Want to write code or docs?** Read [CONTRIBUTING.md](CONTRIBUTING.md) for
  how to get started and the pull-request process.
- 🔒 **Found a security issue?** Please report it **privately** — see
  [SECURITY.md](SECURITY.md). Don't open a public issue.

Everyone participating agrees to our [Code of Conduct](CODE_OF_CONDUCT.md).

---

## Feature overview

Each feature is specified before it's built (see [`specs/`](specs/)):

| Area | What it does |
|------|--------------|
| **Accounts** | Email + password auth, email verification, password reset, account lockout |
| **Onboarding** | A first-login flow that gets new players set up quickly |
| **Profiles** | Player profiles with a shareable public link |
| **Teams** | Team spaces, member handling, and public team pages with request-to-join |
| **Events** | Create events and let players and teams sign up |
| **Event parties** | For team events, form a temporary crew ("party") of players for one event — invite/remove members, co-admins, and private party news |
| **Event marketplace** | A mercenary board on team events — free agents post themselves, short-handed parties post open spots, with a two-way accept |
| **Trainings** | Team-scoped recurring training sessions with per-session Going / Maybe / Can't and a "your trainings" agenda |
| **Chat** | Built-in messaging — 1:1, groups, and auto-created team & party chats with live typing, read state, and link unfurls |
| **Search** | Browse and search teams, players, and events |
| **Home & nav** | A personalized dashboard and top-level navigation |
| **Notifications** | In-app notification system with per-user preferences |
| **Badges & achievements** | Admin-granted badges and achievements shown on player profiles and team pages |
| **Admin area** | A gated platform-admin area — user management, account actions (suspend/ban/reset), and a badge/achievement catalogue |

---

## Tech stack

| Layer | Choice |
|-------|--------|
| Backend | **.NET 10**, Entity Framework Core |
| Database | **PostgreSQL 18** (UUIDv7 keys, automatic audit fields) |
| Auth | Microsoft Identity, Argon2 password hashing, JWT in `httpOnly` cookies |
| Frontend | **Angular + Nx + Tailwind CSS** |
| Realtime | **SignalR** (chat, live typing, notifications) with a **Redis** backplane + distributed rate limiting |
| Mapping | Mapster (entity → DTO) |
| Email | Mailpit (local), Resend (deployed) |
| Containers | Docker (per-service) + Docker Compose (local) |
| CI/CD | GitHub Actions + Terraform → GHCR → **Azure Kubernetes Service (AKS)** |

Architecture, security, and convention rules live in the project
[**constitution**](.specify/memory/constitution.md); the visual identity lives in
[**DESIGN.md**](DESIGN.md).

---

## Quick start

Everything runs in containers — **you only need [Docker](https://www.docker.com/)
and Docker Compose**. No host-level .NET or Node runtime is required.

```bash
git clone https://github.com/jnroesch/juggerhub.git
cd juggerhub

cp .env.sample .env            # PowerShell: Copy-Item .env.sample .env
docker compose up -d --build   # database, Redis, backend, frontend, Mailpit
docker compose ps              # wait for services to become healthy
```

The backend **auto-applies EF Core migrations on startup** against the (initially
empty) database — there's no manual migration step. Stop with
`docker compose down` (add `-v` to also drop the database volume).

| Surface | URL |
|---------|-----|
| Web app | http://localhost:3000 |
| API (via the app proxy) | http://localhost:3000/api/v1/health |
| API (direct) | http://localhost:8080/api/v1/health |
| Scalar API reference (Development only) | http://localhost:8080/scalar/v1 |
| Mailpit (captured local email) | http://localhost:8025 |

### Try the auth flow locally

1. **Register** at http://localhost:3000/register — the password meter shows the
   live policy. You land on a neutral "check your email" screen.
2. **Verify** — open the **Mailpit inbox** at http://localhost:8025 and click the
   verification link. (Sign-in is blocked until verified.)
3. **Sign in** at http://localhost:3000/sign-in.
4. **Forgot / reset** via http://localhost:3000/forgot-password → open the reset
   link in Mailpit → set a new password.

All local mail is captured by Mailpit; deployed environments send via Resend.

### Run the tests

All suites run in containers via the `docker-compose.test.yml` overlay:

```bash
test="docker compose -f docker-compose.yml -f docker-compose.test.yml"

$test run --rm backend-test    # xUnit + Testcontainers (real Postgres)
$test run --rm frontend-test   # Jest unit tests
$test run --rm playwright      # Playwright e2e (start the stack first)
```

---

## Project structure

```
├── backend/              # .NET 10 API (Controllers/Services/Entities/Data/Dtos), tests/, Dockerfile
├── frontend/             # Nx + Angular workspace (apps/web, apps/web-e2e), Dockerfile
├── specs/                # Spec-Kit feature specs, plans, and tasks
├── .specify/             # Spec-Kit constitution & templates (architecture source of truth)
├── DESIGN.md             # Visual identity / design tokens (UI source of truth)
├── CLAUDE.md             # AI-assisted development workflow rules
├── .github/              # CI/CD workflows, issue forms, PR template
└── docker-compose*.yml   # Local development orchestration
```

---

## How JuggerHub is built (the AI-assisted toolchain)

JuggerHub is developed with an integrated, spec-driven AI toolchain. It's not
required to use the app or to contribute a small fix, but it's part of what makes
this repo tick — and it's all here in the open.

> **Spec-Kit** decides · **DESIGN.md** styles · **GitHub Issues** queue ·
> **Graphify** maps.

| Tool | Role | UI |
|------|------|----|
| **[Spec-Kit](https://github.com/github/spec-kit)** | Specs, plans, tasks, constitution — the source of truth for behavior | — |
| **DESIGN.md** | Visual identity / design tokens | the `DESIGN.md` file |
| **GitHub Issues** | Intake & prioritization | GitHub issue tracker + `gh` CLI |
| **Graphify** | Codebase knowledge graph / impact analysis | interactive graph (HTML) |

The full workflow rules, source-of-truth ordering, and tool routing live in
[**CLAUDE.md**](CLAUDE.md).

### Setting up the toolchain (optional)

The global CLIs and Claude plugin aren't committed, so reinstall them after
cloning. Requires **Node 20+**, **uv**, and **git**.

```bash
# Global CLIs
npm install -g @google/design.md
uv  tool install specify-cli --from git+https://github.com/github/spec-kit.git@v0.11.9
uv  tool install graphifyy && graphify install --platform claude

# Enable Graphify auto-rebuild git hooks (tracked in .githooks/)
git config core.hooksPath .githooks
```

---

## Security & secrets

- **`.env` is gitignored** and never committed — only the placeholder
  [`.env.sample`](.env.sample) travels with the repo. Local defaults work out of
  the box; deployed secrets flow through GitHub Environments.
- Tracked `appsettings*.json` files hold **no secret values** — configuration is
  injected via environment variables.
- The project is written security-first (server-side enforcement, OWASP Top 10);
  see the [constitution](.specify/memory/constitution.md), Principle I, and
  [SECURITY.md](SECURITY.md) for reporting.

---

## License

JuggerHub is licensed under the [Apache License 2.0](LICENSE). See [NOTICE](NOTICE)
for attribution details.
