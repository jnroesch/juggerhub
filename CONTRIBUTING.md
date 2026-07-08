# Contributing to JuggerHub

Thanks for your interest in JuggerHub — a warm, community-run home for the sport
of [Jugger](https://en.wikipedia.org/wiki/Jugger). Contributions of all kinds are
welcome: bug reports, ideas, feedback, code, and docs.

By taking part, you agree to our [Code of Conduct](CODE_OF_CONDUCT.md).

> **You don't need to be a developer to help.** The fastest way to shape
> JuggerHub is to [open an issue](https://github.com/jnroesch/juggerhub/issues/new/choose)
> — there are guided forms with no Markdown or git knowledge required.

## Ways to contribute

- 🐞 **Report a bug** — use the
  [bug report form](https://github.com/jnroesch/juggerhub/issues/new/choose).
- 💡 **Request a feature** — use the
  [feature request form](https://github.com/jnroesch/juggerhub/issues/new/choose).
- 💬 **Share feedback** — use the
  [feedback form](https://github.com/jnroesch/juggerhub/issues/new/choose).
- 🔒 **Report a security issue** — please **don't** open a public issue; follow
  [SECURITY.md](SECURITY.md) instead.
- 🛠️ **Contribute code or docs** — see below.

## Contributing code or docs

1. **Start with an issue.** For anything beyond a trivial fix, open (or comment
   on) an issue first so we can agree on the approach before you invest time. This
   avoids duplicated or wasted work.
2. **Fork the repo and branch** off `main`. Use a descriptive branch name, e.g.
   `fix/event-signup-error` or `docs/readme-typo`.
3. **Keep it focused.** Small, single-purpose pull requests are easier to review
   and merge. Unrelated changes belong in separate PRs.
4. **Write clear commits.** Conventional-commit prefixes (`feat:`, `fix:`,
   `docs:`, `refactor:`, `test:`, `chore:`) are appreciated and match the existing
   history.
5. **Open a pull request** and fill in the template. Link the issue it addresses
   (e.g. `Closes #123`) and include screenshots for any UI change.

A maintainer will review it. Please be responsive to feedback — and thanks for
helping grow the Jugger community. 🎉

### Preferred development workflow

JuggerHub is built with the spec-driven, AI-assisted workflow described in
[CLAUDE.md](CLAUDE.md) — Spec-Kit for specs and tasks, DESIGN.md for UI, and the
project [constitution](.specify/memory/constitution.md) for architecture and
conventions. Following that flow is the **preferred** way to contribute code, and
it's how changes stay consistent with the rest of the project. It isn't a hard
requirement for a small fix, but for anything larger please read CLAUDE.md and
align with it.

## Running & testing locally

JuggerHub runs **entirely in Docker** — there is no host-level .NET or Node
setup, and running everything in containers keeps local development at parity
with the deployed Dev and Prod environments. You only need
[Docker](https://www.docker.com/) and Docker Compose.

**First-time setup — the `.env` file is required:**

```bash
cp .env.sample .env            # PowerShell: Copy-Item .env.sample .env
docker compose up -d --build   # database, backend, frontend, Mailpit
```

The stack will **not start without `.env`** (the JWT signing key has no built-in
default). The baked-in sample values work as-is for local development. See the
[README](README.md#quick-start) for the full URL map and the local auth-flow
walkthrough.

### Checks to run before opening a PR

CI runs lint, build, and tests on every pull request, so run them locally first.
Every suite runs in a container via the `docker-compose.test.yml` overlay:

```bash
t="docker compose -f docker-compose.yml -f docker-compose.test.yml"
$t run --rm backend-test    # xUnit + Testcontainers (Docker must be running)
$t run --rm frontend-test   # Jest unit tests
$t run --rm playwright      # Playwright e2e (desktop + mobile)
$t run --rm --entrypoint npx frontend-test nx run-many -t lint   # lint gate
```

### VS Code workspace

Open [`project.code-workspace`](project.code-workspace) for one-click **Tasks**
(Terminal → Run Task) and terminal profiles covering the whole Docker lifecycle:
compose up / debug / down, drop-DB-volume, each test suite above, the CI-parity
lint, and attach-in-container debuggers (`Full Stack Debug`). It is the fastest
way to drive the stack without memorizing the commands.

## Licensing of contributions

JuggerHub is licensed under the [Apache License 2.0](LICENSE). By submitting a
contribution, you agree that it is licensed under those same terms. Don't submit
code you don't have the right to license.
