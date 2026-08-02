# CLAUDE.md

This project uses:

* **Spec-Kit** for product requirements, architecture, plans, and tasks.
* **DESIGN.md** for UI style, visual identity, and frontend consistency.
* **GitHub Issues** for intake and prioritization.
* **Graphify** for codebase understanding and impact analysis.
* **Custom skills** for specialist workflows.

Implementation is executed directly — task-by-task with small commits and
verification — or, for a Spec-Kit `tasks.md`, via the `/speckit-implement` skill.

Core rule:

> Spec-Kit decides. DESIGN.md styles. GitHub Issues queue. Graphify maps. Skills specialize.

---

## Source of Truth

When sources conflict, use this priority order:

1. Current user instruction
2. Source code and tests
3. Spec-Kit specs, plans, tasks, and constitution
4. DESIGN.md for UI/design decisions
5. GitHub Issue description
6. Graphify output
7. General model knowledge

Never let GitHub Issues, Graphify, or skills override Spec-Kit.

Do not mix OpenSpec into this workflow.

---

## Tool Responsibilities

### Spec-Kit

Use Spec-Kit for:

* new features
* product behavior changes
* API contract changes
* database model changes
* auth, permissions, billing, or workflow changes
* architecture decisions
* large refactors
* unclear requirements

Before significant implementation, prefer:

1. Read `.specify/memory/constitution.md`
2. Create or update spec
3. Clarify requirements
4. Create or update plan
5. Create tasks
6. Execute the tasks via `/speckit-implement`

Do not implement significant behavior changes directly from a GitHub Issue.

---

### DESIGN.md

Use DESIGN.md for all UI work:

* layouts
* components
* colors
* typography
* spacing
* empty/loading/error states
* responsive behavior
* visual consistency

Do not invent a new visual style unless explicitly asked.

If UI requirements conflict with DESIGN.md, report the conflict.

---

### GitHub Issues

GitHub Issues are for intake and deciding what to work on next.

Use them for:

* ideas
* goals
* bug reports
* chores
* technical debt
* prioritization

A GitHub Issue is not implementation truth.

When an issue is selected, classify it:

* tiny fix
* UI fix
* bug
* feature
* refactor
* architecture change
* research

Then route it:

* Tiny fix → Graphify or direct inspection → implement → verify
* UI fix → DESIGN.md → Graphify → UI skill → implement → verify
* Bug → Graphify → inspect code/tests → implement → verify
* Feature → Spec-Kit → Graphify → DESIGN.md if needed → skills → implement → verify
* Architecture/refactor → Spec-Kit if architecture changes → Graphify → skills → implement → verify
* Research → Graphify/specs → summarize findings, no code changes

Promote an issue into Spec-Kit only when it changes behavior, APIs, schema, auth, permissions, billing, architecture, or has unclear acceptance criteria.

---

### Graphify

Use Graphify before working in unfamiliar code or estimating impact.

Use it to find:

* related files
* existing patterns
* dependencies
* affected modules
* backend/frontend flows
* cross-cutting impact

Prefer scoped queries before broad manual searching, for example:

* `graphify query "where is authentication implemented?"`
* `graphify query "what handles Stripe webhooks?"`
* `graphify query "which modules depend on UserService?"`
* `graphify query "where are team settings implemented?"`

Graphify is contextual, not authoritative. Validate important findings against source code and tests.

---

### Execution

Execute known work directly — or, for a Spec-Kit `tasks.md`, via the
`/speckit-implement` skill. When executing:

* read relevant Spec-Kit files first
* read DESIGN.md before UI work
* query Graphify before unfamiliar code edits
* use relevant skills
* work in small phases with small commits
* verify changes
* report spec drift

Do not invent requirements or silently change scope.

---

### Custom Skills

Use custom skills when the task matches a repeatable domain workflow.

Skills guide execution but do not override user instructions, code/tests, Spec-Kit, or DESIGN.md.

---

## Default Workflows

### New Feature

1. Read constitution
2. Create/update Spec-Kit spec
3. Clarify
4. Create/update plan
5. Create tasks
6. Query Graphify
7. Read DESIGN.md if UI is involved
8. Select skills
9. Execute via `/speckit-implement`
10. Verify
11. Report changes and spec drift

### Bug Fix

1. Read bug report
2. Query Graphify
3. Inspect code/tests
4. Determine expected behavior
5. Use Spec-Kit only if expected behavior is unclear
6. Fix it
7. Add/update tests when useful
8. Verify

### UI Work

1. Read DESIGN.md
2. Query Graphify for affected components/routes/state
3. Use UI/design skill
4. Use Spec-Kit if behavior changes
5. Execute the change
6. Run the **UI review checklist** — for a Spec-Kit feature, copy
   `.specify/templates/ui-review-checklist-template.md` into
   `specs/<feature>/checklists/ui-review.md` and verify each item against the diff
   (DESIGN.md wins on any conflict)
7. Verify layout, responsiveness, states, and basic accessibility

### Refactor

1. Query Graphify for dependencies and impact
2. Use Spec-Kit if architecture changes
3. Preserve behavior unless explicitly told otherwise
4. Execute in small phases
5. Verify after meaningful changes

### Research

1. Read relevant specs/docs
2. Query Graphify
3. Inspect code as needed
4. Summarize findings
5. Do not modify code unless asked

---

## Pre-Implementation Checklist

Before editing code, answer:

* What type of task is this?
* Does it require Spec-Kit?
* Does it affect UI and require DESIGN.md?
* Has Graphify identified the affected area?
* Which skills apply?
* What verification should run?

If the task is significant and has no spec, use Spec-Kit before implementation.

---

## Implementation Rules

* Work in small phases.
* Prefer existing project patterns.
* Avoid unnecessary abstractions.
* Do not silently change scope.
* Do not overwrite unrelated changes.
* Do not ignore failing tests.
* Do not store secrets in code, docs, specs, GitHub Issues, or Graphify.
* Keep code aligned with Spec-Kit and UI aligned with DESIGN.md.

---

## Verification and Reporting

After implementation, run relevant verification:

* tests
* build
* lint
* typecheck
* formatting
* migration checks
* smoke tests

Report:

1. Summary
2. Files changed
3. Verification run
4. Failures or skipped checks
5. Risks/follow-ups
6. Spec or design drift
7. GitHub Issue status, if applicable

Never claim verification passed if it was not run.

---

## Minimal Routing

* “What should I work on next?” → GitHub Issues
* “Build this feature” → Spec-Kit first
* “Implement this task” → Spec-Kit/GitHub Issue context → Graphify → implement
* “Fix this bug” → Graphify → inspect code/tests → implement
* “Change this UI” → DESIGN.md → Graphify → UI skill → implement
* “Refactor this” → Graphify first, Spec-Kit if architecture changes
* “Continue from last time” → git history, specs, and open GitHub Issues, then validate against code

Always choose the smallest responsible process.

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan:
`specs/038-umami-session-recording/plan.md` (Umami session recording — extends 033 with the rrweb recorder. **NO backend, NO Angular source change**: nginx config + Terraform + compose + seed SQL + the legal i18n catalogues. **THE DESIGN HINGES ON ONE FACT**: the recorder derives its endpoints from *the directory of its own `src`*, not the origin — `const l=(a||""||r.src.split("/").slice(0,-1).join("/"))`, `c=${l}/api/record`, `u=${l}/api/websites/${i}/recorder`. So it is served at **`/jh-insights/r.js`** (inside a directory) which lands both endpoints in the `/jh-insights` namespace 033 owns. Root placement (`/jh-insights-r.js`) would derive `/api/record` — **the .NET backend's namespace** — and fail SILENTLY (SPA fallback / 404 to a fire-and-forget POST). Locations are **exact matches, never `location /jh-insights/api/`** — a prefix would proxy Umami's whole admin API, incl. `/api/auth/login`, to the app origin. Needs a new `JH_ANALYTICS_WEBSITE_ID` var (today the id is only embedded inside `JH_ANALYTICS_HEAD`). Recorder is appended **inside 033's existing DNT/GPC guard** — it implements **neither signal itself** (no `data-do-not-track` equivalent; the strings aren't in the file), so the guard is the *entire* objection mechanism, not defence in depth. **TWO SPEC PREMISES WERE WRONG AND WERE CORRECTED BY READING THE FILES**: (1) the recorder uses **NO client-side storage API at all** — no cookie/localStorage/sessionStorage/IndexedDB — so the device-storage consent rule stays unengaged, 036's "no cookie banner" section **SURVIVES**, and the policy rewrite is ONE claim ("Nothing in it says who was doing the browsing") not a section; the true "stores nothing on your device" claim must NOT be weakened. (2) `sampleRate` defaults to **0.15**, not all sessions. Both senders use `credentials:"omit"` so the auth cookie never reaches Umami (verified; `proxy_set_header Cookie "";` added anyway, incl. to 033's two older locations). **Privacy behaviour is SERVER-SIDE STATE, not page config**: `website.recorder_enabled` + `website.replay_config` JSONB {replayEnabled, heatmapEnabled, sampleRate, maskLevel, maxDuration, blockSelector}, dashboard-editable with no release and no git trace → seeded as SQL, and **verify the live endpoint, not the seed** (`getRecorderConfig` silently discards invalid keys). `maskLevel` is ONLY `'strict'|'moderate'`; **`maskAllInputs:true` at BOTH levels**, so FR-005/FR-006 can't be switched off through that surface. Owner chose **`moderate`** ⇒ inputs masked, **chat message history rendered on screen IS captured** (FR-006a — the widest exposure; the two one-field reversals are `maskLevel:"strict"` or a `blockSelector`). **Retention is the real build**: Umami has NO expiry/retention/cleanup and neither does the platform (GH #106) → new daily CronJob, and FR-012a makes recording contingent on it. Delete **by session, not by row** (`session_replay` is CHUNKED — a row-wise delete truncates a straddling session so it replays from the middle) and expire `session_replay_saved` too, or "kept 30 days" is false. **This feature REVERSES 033's explicit release gate** ("session replay stays OFF … a gate, not a preference") — 033 `quickstart.md` scenario 7 must be **amended, not failed**. Dev is **already armed** (`replayEnabled:true, sampleRate:1`) and starts recording the moment the snippet ships, so FR-019 sequences the policy text FIRST. Heatmaps stay off. The supplied dashboard snippet was declined: outside the guard, cross-origin to a blocklist-matched `analytics` host, Dev host+id baked into the Prod build.)

Prior plan, still relevant for the legal pages this feature edits: `specs/036-privacy-policy-imprint/plan.md` (Privacy policy + Impressum — GH #92, deferred from 033's FR-010. **FRONTEND-ONLY: no backend, no endpoint, no entity, no migration** — adding a server surface would mean an `[AllowAnonymous]` hole in the 026 `FallbackPolicy` for data with no server-side dependency, so the 021/026 OpenAPI-allowlist gotcha does **not** apply here. **Remedial, not preventative**: 033 is merged AND deployed to Dev+Prod (`b38cee4`, `47288e6`) and records page paths verbatim (033 FR-008), so `/u/<handle>` and `/t/<slug>` are already stored with zero disclosure anywhere in the product. Owner decisions: **legitimate interest, NO consent banner** (device-storage rule isn't engaged at all — analytics writes nothing, auth cookie is strictly necessary; the open question was only the lawful basis for the path data), DNT/GPC is the objection route and gets **re-verified here rather than cited** from 033; **German AUTHORITATIVE**, en/es informational with a visible divergence notice. Routes `/privacy` + `/imprint`, lazy, **unguarded**, **outside `ShellComponent`** (the anonymous public bar pushes sign-in/register — wrong framing for a reader who hasn't decided to register — and it dodges the fixed mobile bottom bar). Prose lives in a **lazy Transloco scope** `public/i18n/legal/{en,de,es}.json` (the loader already documents scoped paths) — NOT the main catalogs, which load on every page. **TOP GOTCHA: `useFallbackTranslation:true` + `fallbackLang:'en'` is a hazard here** — a missing `de` key renders English *inside the legally binding German document* with no signal; mitigated by a Jest **identical-key-set test**, NOT by changing the global fallback (that would break 031 app-wide). Second guard: a `__TODO__` **placeholder-sentinel test that FAILS until the owner supplies the imprint particulars** (spec Q1, the one open item) — by design, so a TODO can't reach Prod inside the one legally-prescribed document. Imprint particulars are **committed, not nginx-envsubst-injected** — injection protects nothing (the address must be published and is crawled anyway), hides the prescribed text from pre-Prod review, and splits one document across two mechanisms; **the address enters PUBLIC git history irreversibly**, so the real mitigation is choosing a `c/o`/business address. Reachability = one `jh-legal-links` in a new `jh-app-footer` after `<main>` (which already carries `pb-[76px]`, so it clears the bottom bar) + inline on the **9 off-shell screens** — `/register` matters most. DESIGN.md gains a **Long-form content** section built from existing tokens only: `container-sm` 640px measure, in-prose links **underlined** (unlike nav links). Non-obvious disclosures the code forces: `RefreshToken.CreatedByIp` retains a per-session IP; chat is **snapshotted, not deleted**, on team-delete/event-cancel; **no automated retention runs anywhere**; **no self-service export or account deletion exists**, so the policy documents the manual route and must not describe a control that doesn't exist. No Google Fonts (`@fontsource`), no geocoding processor (030's Photon isn't deployed) — processors are Resend + Azure only.)

Prior plan, still relevant for backend/media work: `specs/035-media-storage-abstraction/plan.md` (Media storage abstraction + object storage — GH #97; second ticket of the image split from #13, after #98/034 processing, before #99 galleries. Adds an owner-agnostic **`IMediaStore`** to the existing `backend/Services/Media/` namespace and moves **all three** byte-bearing entities — `ProfileAvatar`, `BadgeIcon`, `AchievementIcon` — from Postgres `bytea` to **Azure Blob Storage**, with **Azurite** in compose + Testcontainers for local/test parity. `Bytes byte[]` → **`ObjectKey` + `SizeBytes`** on the *same three tables*: **do NOT unify into a polymorphic MediaAsset table** — that would destroy `ProfileAvatar`'s `HasQueryFilter(a => a.Profile.User.Status != Banned)`, which is what makes the ban gate structural. **PROXY-ONLY delivery** (owner decision): container private in every env, **no SAS, no redirect, no key/URL ever crosses the client boundary**; reads keep flowing through the unchanged `/profiles/{handle}/avatar` + `/badges|achievements/{id}/icon` endpoints with the 026 visibility gate applied **before** the store call. Public profiles + catalogue icons stay anonymously readable — the gate is "platform decides per request", never "authenticated only". Object keys are random **UUIDv4** hex (`avatars/{32hex}.webp`) — a *deliberate* divergence from the UUIDv7 PK rule, since this is not a key and must be unguessable. **Resilience gotcha**: set `BlobClientOptions.Retry.MaxRetries = 0` and route the SDK transport through a named `HttpClient` carrying 028's `AddJuggerHubResilience` — **Azure Core has NO circuit breaker** (fails Principle VII's stop-condition rule) and leaving both on stacks handlers (3×3=9 attempts). Breaker `MinimumThroughput = 10`, **not** the 100 default (inverse of the 028 email case — media reads actually reach it). Serve via **stream** + `Cache-Control: **private**` (never `public` — shared caches would recreate the exposure) + `ETag`/304. New `MediaRead` rate-limit policy needs a **client-IP fallback partition** (existing `PartitionByUser` can't serve anonymous callers). **NO BACKFILL** — owner accepted total media loss in all envs incl. Prod, so the migration **deletes descriptor rows** then drops `Bytes` (a surviving row would point at a nonexistent object); this **knowingly waives #97's "Existing avatars migrated" AC** (recorded drift). New `infra/modules/storage/` with `allow_nested_items_to_be_public = false`; storage account names must be **3-24 lowercase alphanumeric** so `juggerhub-dev` is invalid — sanitize + `random_string` suffix. Orphans reclaimed by an **admin-triggered sweep** with a grace period, ordering = generate key → put object → save row → delete old object. **No frontend change.**).
<!-- SPECKIT END -->

## GitHub Issues Workflow

This project uses **GitHub Issues** for task and project management and
contributor intake. Use the `gh` CLI (authenticated) to search, read, create,
and update issues so the tracker stays the single source of truth.

Before starting work, search for an existing issue rather than assuming:

- `gh issue list --search "keywords"` — find related issues
- `gh issue list --label bug --state open` — filter by label/state
- `gh issue view <number>` — read an issue and its discussion

Create an issue when work requires planning, decisions, or handoff notes
(bug fixes that need investigation, feature work, API changes, refactors, or
anything that should be reviewed as a commitment). Skip issue creation for
questions, explanations, quick lookups, and obvious mechanical edits.

- `gh issue create --title "..." --body "..." --label "..."` — create
- `gh issue comment <number> --body "..."` — add progress/handoff notes
- `gh issue close <number>` / `gh issue edit <number> --add-label "..."` — update status

Link issues to the work: reference `#<number>` in commit messages and PR
descriptions, and use closing keywords (`Closes #<number>`) in PRs so merging
resolves the issue. Bug reports and feature requests from external contributors
arrive through the templates in `.github/ISSUE_TEMPLATE/`.
