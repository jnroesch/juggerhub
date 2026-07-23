# Feature Specification: Authenticated-Only Access with Opt-In Public Profiles

**Feature Branch**: `026-authenticated-only-access`

**Created**: 2026-07-22

**Status**: Draft

**Input**: User description: "profiles, teams and events are currently accessible without authentication. Lets revert that. All data should be accessible only for authenticated users. People can then decide whether they want to make their personal profile public. This then shows their own profile including the teams they are in and their personal activity but teams and events are always only accessible from within the app"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Data is behind sign-in (Priority: P1)

As the operator of JuggerHub, I want everything in the app — player profiles,
teams, events, and all search/browse — to require a signed-in account, so that
no player, team, or event data is exposed to anonymous visitors on the internet.

An anonymous visitor who lands on any team page, event page, or any browse/search
view is sent to the sign-in screen instead of seeing content. Once signed in,
they retain the full access they have today: every profile, every team, every
event, and all search/browse.

**Why this priority**: This is the core reversal the feature exists for. It
closes the anonymous-exposure surface established in features 006/007/009 and is
the minimum shippable slice — even without the public-profile opt-in (Story 2),
locking everything behind auth already delivers the requested privacy outcome
(with public profiles simply defaulting to unreachable-while-anonymous).

**Independent Test**: Sign out completely, then attempt to open a team page, an
event page, and each browse view (players/teams/events) by direct URL and via
in-app links — every attempt lands on sign-in. Sign in, and confirm all the same
destinations load normally.

**Acceptance Scenarios**:

1. **Given** I am not signed in, **When** I open a team's page by direct link,
   **Then** I am redirected to the sign-in screen and see no team data.
2. **Given** I am not signed in, **When** I open an event's page by direct link,
   **Then** I am redirected to the sign-in screen and see no event data.
3. **Given** I am not signed in, **When** I open any browse/search view (players,
   teams, or events), **Then** I am redirected to the sign-in screen and see no
   results.
4. **Given** I am signed in, **When** I open any profile, team, event, or
   browse/search view, **Then** it loads exactly as it does today.
5. **Given** I am not signed in, **When** an automated client calls a team, event,
   or browse/search read endpoint directly, **Then** the server refuses the
   request as unauthenticated — the block is not merely a UI redirect.

---

### User Story 2 - Opt in to a public profile (Priority: P2)

As a player, I want to choose whether my personal profile is publicly visible, so
that I can share a link to my JuggerHub profile with people who don't have an
account — while keeping it private by default.

From my own profile/account settings I can turn "public profile" on or off. It is
off (private) by default. When it is on, anyone with my direct profile link can
see my profile card, the teams I'm a member of, and my personal activity — without
signing in. When it is off, my profile is invisible to anonymous visitors (a
signed-in member still sees it normally).

**Why this priority**: This is the single, deliberate exception to Story 1. It
delivers the "people can decide" half of the request. It depends on Story 1 being
in place (the default-deny world) but adds real user value by enabling shareable
profiles.

**Independent Test**: As a signed-in player, toggle "public profile" on. Sign out
and open my profile link — my card, teams, and activity render. Toggle it off
(from another session), reload the link while signed out — it now behaves as
not-found. Throughout, a different signed-in player can always see my profile.

**Acceptance Scenarios**:

1. **Given** my profile is private (the default), **When** an anonymous visitor
   opens my profile link, **Then** they see a not-found / sign-in prompt, not my
   profile — and cannot tell whether the profile exists or is merely private.
2. **Given** I have turned my profile public, **When** an anonymous visitor opens
   my profile link, **Then** they see my profile card, the teams I'm a member of,
   and my personal activity.
3. **Given** I have turned my profile public, **When** an anonymous visitor tries
   to open one of the teams listed on my profile, **Then** they are sent to
   sign-in — team pages are never anonymous, even when reached from a public
   profile.
4. **Given** any visibility setting, **When** a signed-in player opens my profile,
   **Then** they see it normally, regardless of whether it is public or private.
5. **Given** I toggle my profile between public and private, **When** the change is
   saved, **Then** anonymous access to my profile link starts/stops matching the
   new setting on the next request.
6. **Given** the visibility control, **When** the client is manipulated to request
   a private profile anonymously, **Then** the server still refuses — visibility
   is enforced server-side, not just hidden in the UI.

---

### User Story 3 - Public profiles are not anonymously discoverable (Priority: P3)

As a player who made my profile public, I want it reachable only by the direct
link I choose to share — not listed in any anonymous search — so that "public"
means "shareable," not "crawlable directory."

Anonymous visitors have no players/teams/events browse at all (Story 1). A public
profile surfaces only when someone opens its direct link.

**Why this priority**: This scopes the public-profile exception so it does not
re-introduce an anonymous discovery surface. It falls out naturally from Story 1
(browse is auth-only) but is called out to prevent scope creep back toward an
anonymous directory.

**Independent Test**: While signed out, confirm there is no way to enumerate or
search public profiles — no anonymous players browse exists — and a public
profile is reachable only when its direct link is entered.

**Acceptance Scenarios**:

1. **Given** several players have public profiles, **When** an anonymous visitor
   looks for any players/teams/events browse or search, **Then** none is available
   to them.
2. **Given** a public profile, **When** an anonymous visitor has its direct link,
   **Then** they can open it; without the link, they have no anonymous means to
   find it.

---

### Edge Cases

- **Private profile vs. missing profile (no oracle)**: An anonymous request for a
  private profile and for a non-existent profile must be indistinguishable — same
  not-found response, no timing or wording that reveals a private profile exists.
- **Banned / suspended owner**: A banned account is already hidden globally; a
  public flag never overrides that — a banned owner's profile is not anonymously
  visible even if the flag was on.
- **Owner viewing own private profile**: The owner (signed in) always sees their
  own profile via the owner view, independent of the public flag.
- **Newly registered user**: Starts private; their profile is not anonymously
  visible until they opt in.
- **Existing users at rollout**: All existing profiles become private on rollout,
  even if they were effectively public (anonymously visible) before.
- **Avatar and activity sub-resources**: When a profile is private, its avatar and
  activity are also not anonymously reachable (same not-found behavior); when
  public, they are anonymously reachable to match the profile.
- **Deep links shared before rollout**: Previously shareable anonymous links to
  teams/events stop working for anonymous visitors and route to sign-in.
- **Team/event links on a public profile**: Rendered so that following one while
  anonymous leads to sign-in, not to an error page.

## Requirements *(mandatory)*

### Functional Requirements

#### Authenticated-only access (Story 1)

- **FR-001**: The system MUST require an authenticated session for all read and
  write access to team data, including team detail, team public page, roster,
  activity, news, contacts, and any team search/browse.
- **FR-002**: The system MUST require an authenticated session for all read and
  write access to event data, including event detail, participants, news,
  contacts, and any event search/browse.
- **FR-003**: The system MUST require an authenticated session for all
  player/team/event search and browse.
- **FR-004**: The system MUST enforce every access decision in FR-001–FR-003
  server-side; client-side redirects are for user experience only and MUST NOT be
  the security boundary.
- **FR-005**: The system MUST preserve the current experience for authenticated
  users — signing in grants the same access to profiles, teams, events, and
  search/browse that exists today.
- **FR-006**: When an anonymous visitor attempts to reach any authenticated-only
  destination, the system MUST route them to sign-in rather than reveal any of the
  requested content.

#### Opt-in public profile (Story 2)

- **FR-007**: Each profile MUST carry a visibility setting with two states, public
  and private, defaulting to private.
- **FR-008**: The profile owner MUST be able to switch their own profile between
  public and private from their own profile/account settings.
- **FR-009**: A profile's visibility MUST be changeable only by its owner
  (enforced server-side against the authenticated subject, never a client-supplied
  identity).
- **FR-010**: When a profile is public, the system MUST allow an anonymous visitor
  with its direct link to view the owner's profile card, the teams the owner is a
  member of (display only), and the owner's personal activity.
- **FR-011**: When a profile is private, the system MUST return the same
  not-found response to an anonymous visitor as for a non-existent profile, with no
  signal that distinguishes a private profile from a missing one.
- **FR-012**: The system MUST allow an authenticated visitor to view any profile
  regardless of its visibility setting.
- **FR-013**: The public profile view MUST NOT expose any account, email, or
  security data — only the public profile fields shown today.
- **FR-014**: Teams listed on a public profile MUST remain authenticated-only;
  following such a link while anonymous MUST lead to sign-in, never to team content.
- **FR-015**: A change to a profile's visibility MUST take effect for subsequent
  anonymous requests without requiring the owner to re-share the link.

#### Discovery scope (Story 3)

- **FR-016**: The system MUST NOT provide any anonymous means to enumerate,
  search, or browse public profiles; a public profile is reachable anonymously
  only via its direct link.

#### Data & migration

- **FR-017**: On rollout, the system MUST set every existing profile to private.
- **FR-018**: New profiles created after rollout MUST default to private.
- **FR-019**: The system MUST NOT make a banned or otherwise globally-hidden
  owner's profile anonymously visible, regardless of the visibility setting.

### Key Entities *(include if feature involves data)*

- **Profile visibility**: A property of an existing player profile indicating
  whether the profile is anonymously viewable. Two states — public / private —
  default private. Owned and changeable only by the profile's account. No new
  standalone entity is required; this is an attribute of the existing profile.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of team, event, and search/browse destinations are unreachable
  by an anonymous visitor — every attempt (in-app link or direct URL) results in a
  sign-in prompt with no content disclosed.
- **SC-002**: 100% of anonymous read attempts against team, event, and
  search/browse data are refused at the server, verified independently of the UI.
- **SC-003**: A signed-in user retains access to every profile, team, event, and
  browse view they could reach before this change (no authenticated-access
  regressions).
- **SC-004**: With a profile set to private (including all profiles immediately
  after rollout), an anonymous visitor cannot distinguish it from a non-existent
  profile — responses are identical.
- **SC-005**: A profile owner can make their profile public or private and see the
  anonymous-visibility change reflected within one page load of sharing/opening the
  link.
- **SC-006**: Anonymous visitors have zero available means to enumerate or search
  public profiles.

## Assumptions

- **Public-profile content is unchanged in shape**: A public profile shows the
  same card + team memberships + activity that the current public profile shows;
  this feature only gates that behind the owner's opt-in and reworks discovery.
  No new public fields are introduced.
- **Owner view is unchanged**: The signed-in owner's own profile experience and the
  authenticated cross-profile view are unchanged except that they no longer depend
  on anonymous accessibility.
- **Sign-in/redirect mechanics exist**: The app already has an authentication guard
  and sign-in flow (used by most routes today); this feature extends that guarding
  to the currently-anonymous routes and endpoints rather than inventing new auth.
- **Direct-link-only discovery** (confirmed with owner): Public profiles are not
  anonymously searchable; all player/team/event browse becomes authenticated-only.
- **Private-by-default at rollout** (confirmed with owner): Existing profiles are
  migrated to private; new registrations start private.
- **Reference-icon and health/auth endpoints stay anonymous**: Non-user-data
  endpoints that are intentionally anonymous today (health checks, auth flows,
  static recognition icons) are out of scope and remain as-is.
- **Spec/constitution drift to reconcile**: This feature reverses the
  "anonymous by design" invariants documented in features 006 (event public
  reads), 007 (anonymous search/browse), and 009 (anonymous team public page), and
  narrows the surface that feature 020 (removed search opt-out) operated on. These
  prior specs and the constitution's Principle I public-route note (SC-002) MUST be
  annotated as superseded/updated by feature 026 during planning, not silently left
  in conflict.
