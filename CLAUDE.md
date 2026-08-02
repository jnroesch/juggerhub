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
`specs/039-transactional-email-templates/plan.md` (Transactional email templates + notification preference gating — GH #109. Brings the **four** emails that bypass `EmailTemplateService` — event cancellation, party request/nudge, party news, market invite — onto the shared template path, and gates them on feature 011 preferences. **NO MIGRATION**: preferences are sparse (absence ⇒ enabled) and both enums are `int` + append-only, so `NotificationType.EventCancelled = 8` and `NotificationCategory.Events = 3` need zero backfill. **PHASE ORDER IS A CORRECTNESS CONSTRAINT, not preference**: the four services `HtmlEncode` today but `ReplaceVariables` does a raw `string.Replace`, so migrating them *before* making the template layer encode-by-default opens an injection hole — Phase A (`RawHtml` wrapper + encode-by-default) MUST precede Phase B. That also closes the **pre-existing** raw `{{NEWS_EXCERPT}}` hole in `team-news.html`. Audit result: only `PLAN_FEATURES` + `STATUS_STYLE` need `RawHtml`, both in unused boilerplate. **Subjects are EXCLUDED from escaping** — they never pass through `ReplaceVariables`, and encoding them would put `&amp;`/`&lt;b&gt;` visibly in the inbox. **TOP GOTCHA: `NotificationCategories.For` ends in `_ => NotificationCategory.TeamNews`**, so omitting the `EventCancelled` case compiles, passes existing tests, and silently files cancellations under the user's *Team news* toggle. Second gotcha: template fallback in `LoadTemplateAsync` is per-**file**, not per-placeholder — a `de` template missing `{{PARTY_URL}}` ships a German email with no CTA and no error; guarded by a placeholder-parity test (same lesson as 036's identical-key-set test). Owner decisions: **all 12 templates authored in en/de/es** (not en-only fallback like invitation/team-news, which stay #84's job); party news **truncates to 140 chars** like team news instead of emailing the full body; **new Events category with a real toggle** (not always-on) *because* cancellation simultaneously gains an in-app notification as its backstop. **In-app needs NO gating work** — `NotificationService.Create/CreateManyAsync` already filter on the InApp channel centrally, so the new type inherits gating free once mapped. Culture rides on the **existing `.Select` projections** (+`PreferredLanguage`), never `ResolveByEmailAsync` (N+1 in a loop). `IEmailLocalizer` gains a `params object[]` **positional-format** overload — word order differs across en/de/es. **Party request has TWO call sites** (`PartyService` fan-out *and* `PartyRosterService` nudge) — both must be gated. Also adds `PRIVACY_URL`/`IMPRINT_URL` to `AddSharedUrls` + all three `footer.html` (036 shipped the pages, never wired them into mail). `.csproj` already globs `EmailTemplates/**/*.html` — no project-file change. Issue #109's step 4 "delete `EmailLegalFooter.cs`" is a **no-op — that file exists in no branch**.)

Prior plan, still relevant for email/erasure overlap (it added `account-deleted.html`, the `GenerateAccountDeletedEmailAsync` generator, and the erased-author placeholder these templates now sit alongside): `specs/037-account-deletion/plan.md` (Self-service account deletion — GH #105, **deletion half only**; export (Art. 15/20) deferred to a follow-up. Replaces the manual by-hand erasure route that 036's policy currently has to describe. **TOP FINDING, reframes everything: the `User` row CANNOT be deleted** — ~20 FKs into it are `DeleteBehavior.Restrict` *by explicit design*, each protecting a record the spec independently says must survive (admin action log, chat messages, blocks, awards granted, news posts, invitations both directions, party/training/conversation ownership). So the shape is **delete `PlayerProfile`, neutralise the `User` row**: everything the member owns cascades off the profile, everything others depend on keeps pointing at an id that identifies nobody. **SECOND FINDING: the "neutral placeholder" already exists and already works** — `ChatConversationService.PlaceholderName = "A former player"` keys on `Sender.Profile.DisplayName` **projecting to null**, NOT on ban status, so deleting the profile lands on the existing path for free (it's English-only + `internal` to Chat, so it gets promoted + localised — recorded in Complexity Tracking). **TOP RISK: adding `AccountStatus.Deleted = 3` silently passes all seven existing `!= Banned` predicates.** Four are incidentally safe (their query filters sit on the now-deleted profile); **three in `ChatConversationService` (L53/161/822) query `_db.Users` directly and FAIL OPEN** — replace with an explicit positive `== Active || == Suspended`. **SECOND TRAP: `Conversation.Name` is a frozen string** — archival copies a display name into a column no cascade or filter can reach; the single most likely place a name survives every other check. Owner decisions: **erasure IMMEDIATE** (no grace period, no scheduled job — the platform has no scheduled retention process at all, and a recoverable state would resemble the 013 ban soft-delete this must stay distinct from), **authored content RETAINED VERBATIM under the neutral author** (chat + news posts — so the disclosure MUST say messages remain, incl. that self-typed identifying text survives; this is a correctness surface, not copy), **email FREED for re-registration** (safe only because FR-005 refuses the flow to suspended/banned — that refusal is load-bearing). **Owner-confirmed asymmetry, now FR-032: a BAN must bar re-registration with the same address; a SELF-DELETION must permit it.** Both fall out of ONE code path — `FindByEmailAsync` at [AuthService.cs:79](backend/Services/Auth/AuthService.cs#L79) finds the *retained* banned row and returns a neutral `Accepted()` creating nothing, vs finds nothing for a released address and proceeds; **do not add a second branch, but test BOTH directions** (SC-013) since a regression in either looks like a passing test of the other. **THIRD TRAP: releasing the email is NOT enough** — registration does `new User { UserName = email, Email = email }` and `NormalizedUserName` is **uniquely indexed**, so a residual username collides, `CreateAsync` fails, and the failure lands on registration's anti-enumeration neutral `Accepted()` — **telling a returning member they registered when no account was created**. FR-034 generalises this: release *every* uniqueness-constrained identifier, and test by completing a real re-registration + sign-in, never by asserting the email column is null. Pre-emptive deletion (an Active user deleting to dodge an anticipated ban) is **accepted, not mitigated** — the denylist is a speed bump anyway since a banned user can already register from any other address. Migration is a **code-only enum extension**: no new table, no new column, no index; `Status=Deleted` + existing `StatusChangedAt` *is* the non-identifying deletion record. Reuses `CheckPasswordSignInAsync(lockoutOnFailure:true)` for re-auth and `RevokeAllForUserAsync` for sessions — but tokens must be **deleted not just revoked** (`RefreshToken.CreatedByIp` retains a per-session IP). Two endpoints on the existing `AccountController`, **neither accepts an account id**. Transaction inside `IExecutionStrategy`; **blob reclaim AFTER commit** (can't be rolled back); failed confirmation email must NOT roll back the erasure; **frontend must exclude this POST from retry** (Principle VII browser-hop rule). Last-admin guard exists for **teams only** (`TeamService.cs:396`) — event/party equivalents are *defined here*; FR-011 needs a new precondition query gathering ALL blockers in one pass. 035 is unmerged so media reclaim is a no-op seam today.)

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
