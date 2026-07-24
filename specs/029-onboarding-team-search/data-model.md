# Phase 1 Data Model: Onboarding Team Search

**Feature**: `specs/029-onboarding-team-search/` | **Date**: 2026-07-24

## Persisted entities

**None introduced, none changed.** No entity, no column, no migration, no DTO.

Two existing server-side concepts are touched, both through capabilities that already ship:

| Concept | Owner | This feature's relationship |
|---|---|---|
| `Team` (feature 005) | backend | **Read only.** Searched and listed. Nothing about a team is created, changed, or deleted. |
| Join request (feature 005/009) | backend | **Creates one**, via the existing endpoint, when the player uses the ask-to-join action. This is the sole persistence the step produces. |
| `Profile` (feature 003) | backend | **Untouched.** The team selection is never written to it, so the onboarding finish payload is unchanged (spec FR-019). |

The onboarding completion record (feature 004) is likewise unchanged — this step does not
participate in it.

## Client view-models (existing, reused as-is)

`TeamCard` — `core/models/search.models.ts`, unchanged:

| Field | Type | Used for |
|---|---|---|
| `slug` | `string` | Row identity (`track`), selection key, and the join-request path segment |
| `name` | `string` | Row title; also interpolated into the ask action's label |
| `city` | `string \| null` | Row subtitle (omitted when null) |
| `playerCount` | `number` | Row subtitle, set in the mono face per DESIGN.md |
| `beginnersWelcome` | `boolean` | "Beginners" pill |
| `logoInitial` | `string` | Row initial chip |

`TeamBrowseParams`, `PagedResult<T>`, and `BrowseState` are reused unchanged.

## Component state (new, transient, never persisted)

Added to `OnboardingComponent`, replacing the removed `teamStub`. All of it dies with the component
— none of it reaches the finish payload.

| Signal | Type | Notes |
|---|---|---|
| `teamQuery` | `string` | The **applied** query. Written by the debounced input, read by the fetcher. |
| `selectedTeam` | `TeamCard \| null` | Single-select (FR-010). Replaced, never accumulated. |
| `requestedSlugs` | `Set<string>` (in a signal) | Slugs asked during this flow. Drives the "asked" row marker and blocks a second ask (FR-015). A set, not a single slug, because asking a second team is allowed. |
| `askingSlug` | `string \| null` | In-flight guard; the ask button reads "Asking…" while set. Never disables Continue. |
| `teamRequestError` | `string \| null` | One plain sentence. Cleared when the selection changes. |
| `teams` | `BrowseList<TeamCard>` | Results + the five display states. Owns its own subscription; `destroy()` on `ngOnDestroy`. |

### State transitions

```text
opening          ── fetch(beginnersWelcome) ──▶ loading ──▶ ready | empty | error
typing (250ms)   ── fetch(q, all teams)     ──▶ loading ──▶ ready | no-results | error
query cleared    ── back to the opening fetch
row tapped       ── selectedTeam := card, teamRequestError := null
ask pressed      ── askingSlug := slug ──▶ 204  ──▶ requestedSlugs += slug, confirmation shown
                                        └─▶ 409  ──▶ teamRequestError := "already on that team"
                                        └─▶ else ──▶ teamRequestError := "couldn't send that"
continue/back    ── no transition here at all; the wizard just moves (FR-012, FR-018)
```

### Invariants

1. `selectedTeam` is one card or none — selecting replaces.
2. A slug in `requestedSlugs` is not askable again from this step.
3. No component state here is ever read by `finish()`. The `updateMine` payload is unchanged.
4. Leaving the step (`next()`/`back()`) reads none of this state and issues no request.
