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
`specs/014-admin-catalogue/plan.md` (Admin Catalogue management).
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
