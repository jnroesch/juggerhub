# Feature Specification: Remove the Player-Search Opt-Out

**Feature Branch**: `020-remove-search-optout`

**Created**: 2026-07-21

**Status**: Draft

**Input**: User description: "Since I can still see the user in a team and get to the profile that way, this toggle does not really provide any functionality to the user's privacy. With chat this is even more pronounced. So I would suggest to fully remove the toggle and connected pieces."

## Context & Amendments

This change **removes** an existing capability rather than adding one, and it amends
two already-shipped features:

- **Feature 007 (search)** — retires its central privacy invariant: the player
  "appear in search" opt-in gate that filtered the anonymous player directory. The
  directory becomes unconditionally inclusive of all players.
- **Feature 003 (profile)** — removes the profile "appear in search" preference,
  its presence in the owner's profile data, and the owner-profile toggle control.

**Rationale** (owner decision): The opt-out controlled only whether a player was
listed in the player *directory*. It never made a profile private — a profile is
already reachable by a direct link, through public team rosters, and through chat
people-search (which never applied this gate). The control therefore removed only
*enumerability*, not exposure, while adding complexity and implying a privacy
guarantee that did not exist. The owner has accepted the single real consequence:
a player with no team and no chat history loses their only "don't list me in the
directory" control, though their profile remains publicly reachable by link.

## Clarifications

### Session 2026-07-21

- Q: Should the removal preserve any per-user directory-listing control? → A: No — this is a pure removal; the player directory becomes unconditionally inclusive of every player, with no replacement control.
- Q: What happens to players who had previously chosen to stay hidden? → A: They become listed in the directory like everyone else; there is no grandfathering and no notification requirement in scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Every player is discoverable in the directory (Priority: P1)

Anyone browsing or searching the player directory finds every player on the
platform, with no player invisibly withheld. Discovery is complete and predictable:
the number of players shown reflects the actual player base.

**Why this priority**: This is the core outcome of the change — a directory that
returns all players. It is the observable behavior everything else supports.

**Independent Test**: With a set of players that previously included both listed
and hidden accounts, browse/search the directory and confirm all of them now
appear (subject only to the normal search terms/filters), and that "hidden" is no
longer a possible state.

**Acceptance Scenarios**:

1. **Given** players exist who were previously hidden from the directory, **When**
   an anonymous visitor browses the player directory, **Then** those players now
   appear alongside everyone else.
2. **Given** a search term or filter (name, city, position), **When** applied,
   **Then** results are drawn from the full player base with no player excluded on
   the basis of a visibility preference.
3. **Given** the directory's "showing N players" count, **When** displayed, **Then**
   N reflects all players matching the query, not only those who had opted in.

---

### User Story 2 - The profile no longer offers a search-visibility toggle (Priority: P1)

A signed-in player editing their own profile no longer sees any "appear in player
search" toggle or hidden/visible status text. There is simply no such setting to
manage, and nothing about their profile editing implies one exists.

**Why this priority**: The control's removal must be complete and coherent — a
lingering toggle that no longer does anything would be worse than either extreme.
It ships together with Story 1 as one coherent change.

**Independent Test**: Open the owner profile edit view and confirm no
search-visibility toggle or status text is present, and saving the profile neither
sends nor expects such a preference.

**Acceptance Scenarios**:

1. **Given** I am editing my own profile, **When** the edit view renders, **Then**
   there is no "appear in search" toggle and no "you appear / are hidden from
   player search" text.
2. **Given** I save my profile, **When** the save completes, **Then** the saved
   profile carries no search-visibility preference and my directory listing is
   unaffected by any prior preference.

---

### Edge Cases

- **Previously-hidden players**: accounts that had opted to stay out of the
  directory become listed like everyone else; there is no separate migration path
  or user notification within this scope.
- **Directory count consistency**: the live "showing N" count and the returned
  results must agree once the gate is removed (no off-by-one between a gated count
  and an ungated list, or vice-versa).
- **No new gate leaks back in**: no query, filter, sort, or caller path may
  reintroduce a visibility exclusion; the directory is inclusive by construction.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The anonymous player directory/browse MUST return every player,
  filtered only by the caller's search terms and filters — never by any per-player
  visibility preference.
- **FR-002**: The system MUST NOT expose, store, or act on any per-player
  "appear in player search" preference. The preference and its storage are removed.
- **FR-003**: The owner profile experience MUST NOT present any search-visibility
  toggle or hidden/visible status indicator.
- **FR-004**: *(Removed 2026-07-21 — backward tolerance for a legacy
  search-visibility field is not required: the frontend and backend ship together,
  so no client sends it. ID retained to keep requirement traceability stable.)*
- **FR-005**: The player directory's total/count and its returned results MUST be
  consistent with one another and reflect the full player base for the query.
- **FR-006**: The previously-documented player-search opt-in privacy invariant MUST
  be retired from the feature 007 specification/contracts, and the feature 003
  specification MUST reflect the removed profile field, so the written specs match
  the shipped behavior.
- **FR-007**: The removal MUST NOT change any other visibility surface — the public
  profile page, team-roster visibility, and chat reach remain exactly as they are.

### Key Entities *(include if feature involves data)*

- **Player profile**: the per-player record shown in the directory and on the
  profile page. This change removes its "appear in search" attribute; all other
  attributes are unchanged.
- **Player directory listing**: the anonymous, searchable list of players. After
  this change it is derived from the full set of player profiles with no visibility
  filter.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of players matching a directory query are returned — there is no
  input, account state, or preference under which a matching player is withheld
  from the directory.
- **SC-002**: The owner profile edit view contains zero search-visibility controls
  or status text (0 occurrences).
- **SC-003**: No stored or transmitted data field represents a per-player search
  visibility preference after the change (0 occurrences across stored profile data
  and profile API payloads).
- **SC-004**: *(Removed — see FR-004.)*
- **SC-005**: The directory "showing N" count equals the number of results actually
  returned for the same query, in 100% of checks.
- **SC-006**: The feature 007 and feature 003 specifications no longer describe an
  active player-search opt-in/opt-out, matching the shipped behavior.

## Assumptions

- **Pure removal, no replacement**: No new privacy or listing control replaces the
  removed toggle; the directory is unconditionally inclusive. Any future privacy
  control would be a separate feature.
- **Other surfaces unchanged**: The public profile page remains public, team
  rosters remain as they are, and chat reach is unchanged — this feature touches
  only the directory-visibility preference and its removal.
- **No grandfathering / notification**: Previously-hidden players are not migrated
  specially nor notified; they simply appear in the directory. If a communication
  to affected users is desired, it is out of scope here.
- **No backward compatibility**: The frontend and backend deploy together, so no
  client sends the removed field; no compatibility shim is required. (Incidentally,
  the server would ignore an unknown field anyway, but this is not a guaranteed
  behavior of this feature.)
- **Amends completed features**: This is a deliberate, documented amendment to
  features 007 and 003; the associated specs/contracts are updated as part of the
  change so the source-of-truth documents stay accurate.
- **Relation to feature 021 (profile quick-actions)**: 021's requirement that
  messaging works for search-opted-out players becomes moot once the opt-out is
  gone. This feature does not depend on 021 and must not block or be blocked by it.
