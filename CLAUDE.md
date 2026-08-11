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
`specs/045-wizard-draft-persistence/plan.md` (Wizard drafts survive leaving the page — GH #182. The create-training and create-event wizards keep every answer in component memory, so in-app navigation, back, a reload, or a **backgrounded mobile tab being evicted** returns the user to a blank step 1. Both wizards get a **`sessionStorage` draft** — written on every answer change, restored before first render, cleared on accepted create / trainings' Cancel / sign-out. **FRONTEND ONLY: no backend, no endpoint, no entity, no migration, no new dependency, and ZERO new i18n keys in the main catalogues.** If a task produces an `Add-Migration` or touches `backend/`, something is wrong. **THE #1 TRAP IS THE CITY CHIP**: `CityPickerComponent` consumes `@Input() initial` in **`ngOnInit`** and `AddressFieldsComponent` carries an explicit ⚠ about it — a value pushed in later never reaches the chip. So the restore MUST happen in the **field initialiser** (not `ngOnInit`, not async), and **`[initialCity]` must be ADDED to both create templates** — today only the three *edit* forms pass it, because only they ever had a stored city. Get this wrong and the wizard restores 20 of 21 fields with an **empty city chip** while the review step confidently prints the restored city. **THE TRAINING WIZARD NEEDS ITS PLAIN FIELDS CONVERTED TO SIGNALS** (10 of them) so one `effect()` can persist everything: the app is **zoneless** and an `effect()` cannot see plain properties — the same hazard the file already documents for `locationKind`/`virtualLink`, whose `[ngModel]="x()" (ngModelChange)="x.set($event)"` form at line 92 is the idiom to copy. Saving only inside `next()` is **not sufficient** (FR-005) — it loses the address-and-description step, the expensive one, to exactly the mid-step eviction that was reported. Wrapping the steps in an `NgForm` is a trap: each step lives in an `@if`, so its controls **unregister on leave** and vanish from `form.value`. Event wizard needs no refactor (`form.valueChanges` for 13 controls) but its **7 signals sit OUTSIDE the FormGroup** and must be in the same effect — count 21 at review. **`busy`/`submitting`/`error` are NEVER persisted** (restoring `busy:true` permanently disables the submit button); clear the draft **only after the server accepts**, never on the click, or a rejected create discards the user's work at the worst moment. "Pristine" for the empty-draft rule is **not** all-blank — trainings default `weekday` to *today*, times to 19:00/21:00; events default limit 16 / cap 8 / EUR — so compare against a snapshot, never hand-written emptiness checks. **OWNER DECISIONS, all three against the issue's options**: draft persistence ONLY (no `canDeactivate`, no `beforeunload`, no step-in-route); **every field persisted INCLUDING `feeIban`+`feeRecipientName`**; and **silent restore with NO discard control** — whose accepted residual is that an abandoned event draft, IBAN included, returns in the same tab. That IBAN decision is the *entire* reason the medium is **`sessionStorage`, not `localStorage`** (dies with the tab) plus a clear-on-sign-out hook in `AuthService.logout()`/`clearSession()`. **The privacy policy MUST be corrected in all 3 locales** (German authoritative): the `legalBasis` sentence "Ours stores nothing there — no cookie, no local storage, nothing" becomes false; the no-banner conclusion survives on the § 25 TDDDG strictly-necessary limb. **Do NOT touch the `analytics` section's own "stores nothing on your device" claims** — 038 verified the recorder uses no storage API and those stay true. `legal-catalog.spec.ts` DM-1 **already** enforces key parity (verified by reading it) — editing `en.json` alone turns the suite red; **no new guard is needed**. **⚠ Principle VII is NOT engaged** — this feature adds no network call at all; wrapping a `setItem` in retry/breaker is review-rejectable. **Gate 7 is recorded as not instantiated**: the silent restore ships no new markup, component, copy or style, so there is no surface for a UI review checklist.)

Prior plan, MERGED: `specs/044-team-activity-feed/plan.md` (Team-internal "What's happening" section — GH #178. A **members-only card on the team page** listing the last 30 days of internal happenings (max 10): who joined, what the team was awarded, a training series added, a session cancelled. **NO ENTITY, NO MIGRATION, NO NEW DEPENDENCY, NO WRITE PATH** — one DTO file, one service, one controller action, one Angular component, 7 new + 2 renamed i18n keys × 3. If a task produces an `Add-Migration`, something is wrong. **⚠ THE ISSUE'S FRAMING WAS REJECTED BY THE OWNER MID-CLARIFICATION (D5)**: #178 asked to *merge* everything into the existing "Letzte Aktivität" card; the owner split it into **two features** instead — the existing card stays a signed-in-visible **event** history (untouched, only its heading renamed to name events, FR-016/FR-017) and the internal feed is a **separate members-only card**. That split dissolved three problems the merge had: no per-kind visibility matrix, no team-only-training leak path, no collision between a recency cutoff and the existing event history. **TWO OF THE ISSUE'S PREMISES ARE FACTUALLY WRONG** and were corrected by reading the code: the team detail surface is **NOT anonymous-reachable** (026 made it auth-only — "public" here means *signed-in*), and `ActivityItemDto` is shared with the **profile** surfaces only, not admin (admin has its own `AdminActivityItemDto`). **THE FLOOD HAZARD IS THE TOP IMPLEMENTATION TRAP**: `RecurrenceExpander.MaxSessions` is **520** and `TrainingSeriesService` materialises the whole expansion in one save, so a per-session "scheduled" kind emits up to 520 entries sharing one timestamp — read `Trainings.CreatedDate` (**one entry per series**), never `TrainingSessions.CreatedDate` (D3/SC-004). **`OccurredAt` is a DIFFERENT column per kind and is never `CreatedDate` uniformly** — `JoinedDate` / `EarnedAt` / `Training.CreatedDate` / `CancelledDate`; dating a cancellation by the session's `CreatedDate` puts brand-new information outside the 30-day window. **Do NOT reuse `ActivityEntryDto`/`ActivityKind`** (issue open question 2): `ActivityListComponent` switches exhaustively with `default: return ''`, so team-only kinds would be silently dropped on the dashboard — copy the *pattern*, introduce `TeamHappeningKind`. **Do NOT use `MemberPlaceholder`** — `TeamNewsService` resolves culture server-side but the activity pattern deliberately sends `null` and lets the client translate (`ActivityParamsDto`'s doc comment is the warning); project names via the `_db.PlayerProfiles.Where(...).FirstOrDefault()` sub-projection HomeService uses, because `PlayerProfiles` carries a ban `HasQueryFilter` that makes the `m.User.Profile!` navigation misbehave. Awards **must** filter `Status == Active` (revoked rows are retained for audit). **The feed is derived, and that is load-bearing** — a departed member's join, a revoked award, and a banned name all self-correct for free; a persisted table would reintroduce all three as bugs, and D1 removed the only kinds that would have *required* writing (departures/role changes are **excluded**, follow-up issue). Bounds are **hardcoded constants** (30 days / 10 entries), explicitly not configuration (owner: "we can change it later"). **One constitution deviation, recorded**: returns a bare capped list, not `PagedResult<T>` — FR-013 forbids paging and a `totalCount` would advertise a "show more" that does not exist; precedent is `Roster` (48) and `RecentActivity` (6) on the same page. **⚠ Principle VII is NOT engaged** — no outbound call; wrapping a local `SELECT` in `AddJuggerHubResilience` is review-rejectable. Unlike the dashboard's activity list (which renders nothing when empty) this card **must** show an empty state (FR-014). Real UI risk is the **awards overlap**: for a member one award appears both in the dated card and the undated "Badges & achievements" standing collection (D2) — they must read as a log and a trophy shelf, not two happenings; DESIGN.md governs. German heading is deliberately **"Was passiert gerade", NOT "Was ist los"** — the latter is the dashboard's, and reusing it recreates the exact two-sections-one-name confusion #178 was reported about.)

Prior plan, implemented on this branch's base: `specs/043-browse-public-trainings/plan.md` (Browse public trainings — GH #145. A **fourth Browse tab** at `/browse/trainings` listing every session teams have opened to everyone, **one row per dated session** (owner's call over series-grouping), plus repointing the home empty-state "Browse open trainings" button, which today lands in the **events** browser. **NO ENTITY, NO MIGRATION, NO NEW DEPENDENCY** — every ingredient already exists (018 visibility, 042 city+address, 030 `CityDistance`, 007 browse shell, a session page that already admits outsiders as guests); the work is one query, one endpoint, one card DTO, one Angular page, a fourth tab, ~14 i18n keys × 3. If a task ever produces an `Add-Migration`, something is wrong. **THE TWO LOAD-BEARING POINTS ARE INHERITED, NOT INVENTED**: (1) 042's address block is indivisible and keyed on `CityIdOverride` **in the `WHERE` clause as much as the projection** — a city filter written against `s.Training.City.Name` returns a relocated session under the *series'* city; the one field where `??` would be equivalent is the **city id itself** (the block is keyed on it) but write the ternary anyway, since the shorthand is indistinguishable from the defect the entity comment forbids. (2) `LocationLabelFor` is `internal static` and callable from `Services/Search/` — **call it, never copy it**, or SC-003 (training and event at the same address read byte-identically) stops being structural. Visibility gate is `(s.VisibilityOverride ?? s.Training.Visibility) == Public` **with no membership join at all** — the list does not widen for members and does not narrow for outsiders, which is what makes FR-004 structural. **⚠ THE TWO EXISTING PROXIMITY IMPLEMENTATIONS DISAGREE AND TEAMS IS THE CORRECT ONE**: `TeamSearchService` recomputes the total with the join's own `Any()` predicate, `EventSearchService` counts **before** the join and would overstate a proximity page — copy Teams; the events defect is real but **left alone** (FR-030/SC-010 forbid touching events) → follow-up issue. "Upcoming" is **day-granular** (`SessionDate >= today`) matching all five existing trainings queries, **not** events' `EndsAt >= now` — recorded as spec drift on FR-006: a session that ended this morning stays listed until midnight. **⚠ Principle VII is NOT engaged** — no outbound call is added; city + distance are local SQL, and reaching for `AddJuggerHubResilience` here would wrap a `SELECT`. **The one open decision**: FR-024, that a nearby recurring series would otherwise fill page 1 under nearest-first — resolved **by the plan, not by the owner**, as `(distance, date, id)` + a 14-day default upper bound rendered as a removable chip; alternatives (next-session-per-series under that sort only; accept as-is) stay cheap to switch to. Frontend ships the product's **first city filter** (`jh-city-picker` — `toEventParams`/`toTeamParams` send only `country` today despite both backends accepting `city`, so copying a builder verbatim silently drops it), and the card deliberately carries **no RSVP counts** (3 subqueries/row for decoration that reads as capacity, which trainings do not have). Real UI risk is the **fourth tab at 375px** — Spanish "Entrenamientos" is the binding case; no font shrink, no truncation, DESIGN.md governs.)

Prior plan, MERGED, and the direct foundation of 043: `specs/042-training-locations/plan.md` (Structured locations for trainings — copy the **Event** address model (030) onto `Training`, add a per-session override block to `TrainingSession`, and make `Training.Location` derived like `Event.Location` already is. Enables a later "trainings near me"; **proximity search itself is OUT OF SCOPE**, as is any change to events. **THE LOAD-BEARING DECISION**: the session address override is an **indivisible block keyed on `CityIdOverride`** — every other 018 override is `X ?? Training.X`, and applying that per-field to an address is a *defect*: a session relocated to a venue-less address would inherit the **series'** venue name, and a street-only override would render under the series' **city**. So every projection selects `s.CityIdOverride != null ? (session block) : (series block)`, and the guard test is "series HAS a venue, session relocated to an address WITHOUT one → venue must not leak". Second: **`LocationLabel` is computed server-side** by the existing shared `HomeProjections.LocationLabel(city→venue→legacy)` and shipped on the DTOs, so SC-003 (a training and an event at the same address read character-for-character identically) is structural, not a convention two templates must keep. `EditSessionAsync`'s existing `??=` **freeze** (lines 76-80) extends to the address — consequence: after a *time-only* single-session edit `CityIdOverride` is non-null though the admin never touched the address; that's 018's detach semantics, correct — but a **virtual guard must then null all four overrides**, or a virtual session stores an address (FR-003). Free-text `location` is **removed** from all 6 DTOs (breaking; FE+BE ship together, 020 precedent) — removing the *input* is what stops `Training.Location` being reassigned. **⚠ Principle VII is NOT engaged and must stay that way**: `CityService` resolves against the **seeded local `CityReference` table**, "a local SQL query, not an external geocoder" (030 R8; Photon was never deployed) — no `HttpClient`, retry or breaker belongs in this diff. Extract EventService's two **pure** helpers to `Services/Geocoding/StructuredAddress.cs` (events' own tests prove no behaviour change); do **not** extract the legacy-label helper — Event returns `"Online"` for virtual, Training must keep `null`. Migration adds 8 nullable columns + 2 **Restrict** FKs, **no backfill, nothing dropped** — `Trainings.Location` stays so pre-042 rows keep rendering via the fallback. Frontend: trainings stay **template-driven** (`ngModel`) — `jh-city-picker` is form-API agnostic, so no reactive-forms rewrite; new `shared/address-fields/` earns its place on 3 call sites. **No i18n key-parity guard exists for the main catalogues** (only `legal-catalog.spec.ts`, legal scope) and 031's `useFallbackTranslation` renders English silently — measured at parity today (1238/1240/1240, `_meta.*` deliberate in de/es), so the guard is added here.)

Prior plan, unimplemented and unrelated to 042: `specs/041-community-guidelines-terms/plan.md` (Terms of Use + community rules, actively accepted at registration — the agreement behind the enforcement powers 013 already built. **ONE document, not two** (owner): a single binding Terms of Use at `/terms` with the rules as a `behaviour` section inside it. **Scope is DOCUMENT + ACCEPTANCE ONLY** — `Suspended`/`Banned`/`AdminActionRecord` already exist and are NOT touched; and critically **there is NO content-removal capability anywhere in the product** (no admin endpoint deletes a chat message, team, event, training, listing, description, or image — ban is the only lever) and **no member reporting channel** (027 "contact admins" reaches team/event admins, not the operator). So the document **RESERVES** removal rights the product cannot exercise through any UI — FR-005 reserves, FR-008 forbids *describing* tooling that doesn't exist. Implementation must NOT "helpfully" build moderation surfaces. `CODE_OF_CONDUCT.md` is repo-only and says so — never conflate. **THE LOAD-BEARING DECISION**: the client sends **the version string it actually displayed**; the server refuses anything ≠ `TermsOptions.CurrentVersion` (**409**) and records **its own constant**, never the submitted string. That's the only design where the record evidences *what the person saw* — server-stamping records what the server believed (stale cache → silently wrong), trusting the client is forgeable. **Consequence**: `/register` must load the legal catalogue, and a failed load **blocks submit** — which the spec's edge case requires anyway. `AcceptsTerms`+`TermsVersion`+`TermsLanguage` validated **BEFORE** password/handle/`FindByEmailAsync`, so terms refusals never entangle with `RegisterAsync`'s deliberate **enumeration-neutral `Accepted()`** (folding them in would strand the user on "check your email" for an account that was never created). `RegisterRequest` is a **positional record** — attributes on constructor *parameters* or MVC throws. **`TermsAcceptance` row is written via a navigation on `User` inside `UserManager.CreateAsync`'s SaveChanges** — exactly how `PlayerProfile` already is — making "no orphan record" structural, not a cleanup path. Modelled on `AdminActionRecord`: `Restrict` FK, `CreatedDate` IS the acceptance moment, `1:N` not `1:1` (re-acceptance later needs no migration). **⚠ TOP REGRESSION RISK: it must NEVER be added to `AccountDeletionService.EraseOwnedDataAsync`** — that method is a list of `ExecuteDeleteAsync` over every `UserId`-keyed table and this one reads like it belongs; 037 does **not** delete the `User` row (it neutralises columns), so the record survives pointing at a row identifying nobody. Guarded 3× (Restrict FK fails loudly, explicit survival test, XML-doc warning). **036's guards are already generic** — `legal-catalog.spec.ts` walks the whole parsed file, so identical-key-set + `__TODO__` sentinel cover `terms` for free (verify by running, don't assume); the **new** guard G3 is version parity (catalogue↔`TermsOptions`, and across en/de/es — G1 compares KEYS, and values are *supposed* to differ between translations), via the repo-walk already proven in `TemplateParityTests` (throws, never skips). **`LegalPageComponent`'s `siblingLink`/`siblingLabelKey` encodes "exactly two documents" and must become `siblings[]`** — refactor privacy+imprint FIRST, then add terms. `terms` gets its **own** `version`+`lastUpdated` nested in the document node, because catalogue-level `meta.lastUpdated` is **shared** — editing the privacy policy would change the date on a binding contract. Owner decisions on the two open legal questions: **NO minimum age, no age field/confirmation/gate anywhere** — a guardian-responsibility clause in the text is the whole of it (accepted limits: a minor's contract is provisionally void under German law until a guardian approves, and it's unverifiable by design; defensible because the privacy policy rests on contract + legitimate interest, **not consent**, so GDPR Art. 8 isn't engaged); and **publish-only change notice** — the page + its date are the notice, **no** notification/announcement/re-acceptance promised or built. **No existing-user migration** (all accounts everywhere are test data). Text must not contradict the privacy policy: it already says "What you write and upload is yours until you say otherwise" → grant a **display permission only**, never a broad content licence; erasure is **self-service and immediate** with messages surviving as "A former player"; contact is the existing `hello@juggerhub.com`. `/terms` is **unguarded, off-shell, ZERO backend calls** — the third documented exception to 026.)

Prior plan, supplying the infrastructure 041 extends: `specs/038-umami-session-recording/plan.md` (Umami session recording — extends 033 with the rrweb recorder. **NO backend, NO Angular source change**: nginx config + Terraform + compose + seed SQL + the legal i18n catalogues. **THE DESIGN HINGES ON ONE FACT**: the recorder derives its endpoints from *the directory of its own `src`*, not the origin — `const l=(a||""||r.src.split("/").slice(0,-1).join("/"))`, `c=${l}/api/record`, `u=${l}/api/websites/${i}/recorder`. So it is served at **`/jh-insights/r.js`** (inside a directory) which lands both endpoints in the `/jh-insights` namespace 033 owns. Root placement (`/jh-insights-r.js`) would derive `/api/record` — **the .NET backend's namespace** — and fail SILENTLY (SPA fallback / 404 to a fire-and-forget POST). Locations are **exact matches, never `location /jh-insights/api/`** — a prefix would proxy Umami's whole admin API, incl. `/api/auth/login`, to the app origin. Needs a new `JH_ANALYTICS_WEBSITE_ID` var (today the id is only embedded inside `JH_ANALYTICS_HEAD`). Recorder is appended **inside 033's existing DNT/GPC guard** — it implements **neither signal itself** (no `data-do-not-track` equivalent; the strings aren't in the file), so the guard is the *entire* objection mechanism, not defence in depth. **TWO SPEC PREMISES WERE WRONG AND WERE CORRECTED BY READING THE FILES**: (1) the recorder uses **NO client-side storage API at all** — no cookie/localStorage/sessionStorage/IndexedDB — so the device-storage consent rule stays unengaged, 036's "no cookie banner" section **SURVIVES**, and the policy rewrite is ONE claim ("Nothing in it says who was doing the browsing") not a section; the true "stores nothing on your device" claim must NOT be weakened. (2) `sampleRate` defaults to **0.15**, not all sessions. Both senders use `credentials:"omit"` so the auth cookie never reaches Umami (verified; `proxy_set_header Cookie "";` added anyway, incl. to 033's two older locations). **Privacy behaviour is SERVER-SIDE STATE, not page config**: `website.recorder_enabled` + `website.replay_config` JSONB {replayEnabled, heatmapEnabled, sampleRate, maskLevel, maxDuration, blockSelector}, dashboard-editable with no release and no git trace → seeded as SQL, and **verify the live endpoint, not the seed** (`getRecorderConfig` silently discards invalid keys). `maskLevel` is ONLY `'strict'|'moderate'`; **`maskAllInputs:true` at BOTH levels**, so FR-005/FR-006 can't be switched off through that surface. Owner chose **`moderate`** ⇒ inputs masked, **chat message history rendered on screen IS captured** (FR-006a — the widest exposure; the two one-field reversals are `maskLevel:"strict"` or a `blockSelector`). **Retention is the real build**: Umami has NO expiry/retention/cleanup and neither does the platform (GH #106) → new daily CronJob, and FR-012a makes recording contingent on it. Delete **by session, not by row** (`session_replay` is CHUNKED — a row-wise delete truncates a straddling session so it replays from the middle) and expire `session_replay_saved` too, or "kept 30 days" is false. **This feature REVERSES 033's explicit release gate** ("session replay stays OFF … a gate, not a preference") — 033 `quickstart.md` scenario 7 must be **amended, not failed**. Dev is **already armed** (`replayEnabled:true, sampleRate:1`) and starts recording the moment the snippet ships, so FR-019 sequences the policy text FIRST. Heatmaps stay off. The supplied dashboard snippet was declined: outside the guard, cross-origin to a blocklist-matched `analytics` host, Dev host+id baked into the Prod build.)

Prior plan, now MERGED and directly affecting 038's privacy-policy text: `specs/037-account-deletion/plan.md` (Self-service account deletion — GH #105). Two consequences for 038: it rewrote the same `retention` and `rights` sections of the legal catalogues that 038 edits (merged by hand — both sets of paragraphs kept), and **self-service deletion now EXISTS**, so any statement that the platform only offers a manual by-hand erasure route — including 036's policy text and 038's FR-015 — is out of date and must be checked rather than repeated.

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
