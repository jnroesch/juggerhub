# Feature Specification: Chat Inbox Search by People and Conversation Names

**Feature Branch**: `046-chat-inbox-search`

**Created**: 2026-09-08

**Status**: Draft

**Input**: User description: "Chat inbox search matches the names of people in your conversations, not message bodies — amends feature 019 (chat) User Story 6; GitHub issue #221. Today the inbox search box returns two sections: "In your messages" (a match over message bodies within the player's own conversations) and "People" (any player on the platform). Owner decision (2026-09-08): the inbox search must NOT search message bodies. Typing a term narrows the inbox to the player's own conversations in which a member's display name matches (the other person in a DM, members of a group, the team or party roster of an auto chat) OR the conversation's own name matches (group name, team name, party name). Selecting a result opens that conversation. Message-body search is removed from the UI and from the API — the inbox was its only caller. The people search that powers the new-chat picker, compose-by-handle (feature 022) and the profile "Message" button (feature 021) keeps its open reach (FR-049) and is unchanged. Blocking rules (FR-031/FR-033) unchanged. Results stay bounded like the inbox list."

## Context & Amendment

This **amends feature 019 (chat), User Story 6** and its requirements FR-034, FR-035,
FR-036 and success criterion SC-006. Today the search field at the top of the chat inbox
returns two groups of results: **"In your messages"** — messages whose text contains the
term, drawn from the player's own conversations — and **"People"** — any player on the
platform, offered as someone to start a chat with.

**Owner decision (2026-09-08, GitHub #221)**: the inbox search must **not** search message
text. Its job is to find a *conversation*: typing a name narrows the inbox to the player's
own conversations that include a person of that name, or whose own name matches. Selecting
a result opens that conversation. Message-text search is removed from the product
altogether — not hidden, removed — because the inbox was the only place that offered it.

Two things are deliberately **unchanged**. Starting a chat with someone new still goes
through the new-chat picker, the compose-by-handle route (feature 022) and the profile
**Message** action (feature 021); those continue to reach any player (FR-049), keep
excluding blocked players (FR-033), and keep opening an existing direct conversation instead
of creating a second one (FR-008). And who may see which conversation is exactly what the
inbox already decides: membership, hide, and block rules are inherited, not restated.

## Clarifications

### Session 2026-09-08

- Q: When a name is typed into the inbox search, what should the results be — the
  conversations filtered by member name, a list of people you share a conversation with, or
  both? → A: **Conversations, filtered by member name.** The alternatives (a people list
  scoped to acquaintances; two sections) were considered and rejected.
- Q: Should group, team and party chat names also match, or strictly people's names? → A:
  **Both** — a conversation's shown name matches as well as its members' names.
- Q: Is message-text search removed from the product entirely, or only from the interface?
  → A: **Entirely** — interface and any request path. Stated as a working assumption
  (the inbox is the only consumer of message-text search) and accepted by the owner.
- Q: Should the inbox search surface conversations the player has hidden, given that hiding
  is currently one-way (the details panel offers Hide and nothing un-hides)? → A: **No** —
  search must not find hidden chats; it mirrors the inbox's visibility exactly. The missing
  un-hide path is a pre-existing gap tracked as its own GitHub issue, outside this feature.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Find a conversation by a person's name (Priority: P1)

A player with a busy inbox wants the conversation with a particular person. They type part
of that person's name into the inbox search. The inbox narrows to the conversations that
person is part of — the direct chat with them, any group they are both in, the team or
party chat whose roster includes them. The player taps the one they meant and lands in it.

**Why this priority**: This is the whole point of the change — the inbox search exists to
find a conversation, and the natural handle for a conversation is who is in it.

**Independent Test**: Seed a player with a direct chat with Lena, a group containing Lena,
a team chat whose roster includes Lena, and several conversations without her. Type "len"
and confirm exactly the three Lena conversations are listed, in the inbox's order, and that
selecting one opens it.

**Acceptance Scenarios**:

1. **Given** a direct chat with Lena, a group containing Lena and a team chat whose roster
   includes Lena, **When** the player types "len", **Then** exactly those three conversations
   are listed and no other.
2. **Given** a conversation none of whose members match the term, **When** the player
   searches, **Then** that conversation is not listed.
3. **Given** a listed result, **When** the player selects it, **Then** that conversation
   opens exactly as it would from its normal inbox row.
4. **Given** results are showing, **When** the player clears the term, **Then** the full
   inbox returns, unchanged.
5. **Given** a term that matches nothing, **When** results are shown, **Then** a plain
   empty state naming the term is shown, never an error.
6. **Given** a player with more conversations than the inbox shows at once, **When** they
   search for a member of a conversation beyond the visible ones, **Then** that
   conversation is found.

---

### User Story 2 - Message text is never searched (Priority: P1)

A player types a word that appears only inside messages — in no member's name and in no
conversation's name. Nothing is listed. There is no "In your messages" group anywhere, and
no way, through the interface or otherwise, to search message text.

**Why this priority**: This is the owner's decision and the reason the feature exists.
Removing the capability, rather than hiding it, is also the honest privacy posture: fewer
places read message text.

**Independent Test**: Seed a conversation containing a message with the word "banana" and
no member or conversation named anything like it. Search "banana" and confirm zero results;
confirm by direct request that message-text search is no longer available at all.

**Acceptance Scenarios**:

1. **Given** the term appears only in message text, **When** the player searches,
   **Then** no conversation is listed.
2. **Given** any inbox search result, **When** it is shown, **Then** no message excerpt,
   snippet or "in your messages" grouping appears.
3. **Given** a direct request for message-text search that bypasses the interface,
   **When** it is made, **Then** it is not served — the capability does not exist.

---

### User Story 3 - Find a conversation by its own name (Priority: P2)

A player wants the team chat "Hamburg Jugger", the group they named "Tournament trip", or
the thread with an event's admins. They type part of that name and the conversation is
listed even if no member's name matches.

**Why this priority**: Team, group and admin threads are often thought of by their name
rather than by who is in them; without this, a team chat could only be found through one
of its members.

**Independent Test**: Seed a group named "Tournament trip", a team chat for "Hamburg
Jugger" and an admin-contact thread, none of whose members' names contain the term. Search
"hamb" and "trip" and confirm the matching conversations are listed by name.

**Acceptance Scenarios**:

1. **Given** a group whose name contains the term and no member matching it, **When** the
   player searches, **Then** the group is listed.
2. **Given** a team chat, **When** the player types part of the team's name, **Then** the
   team chat is listed.
3. **Given** an admin-contact thread, **When** the player types part of the name the inbox
   shows for that thread, **Then** it is listed.
4. **Given** a conversation matches both by name and by a member, **When** the player
   searches, **Then** it is listed once.

---

### User Story 4 - Starting a chat with someone new is unaffected (Priority: P2)

A player who wants to message someone they have never chatted with uses the new-chat
picker, the profile **Message** action, or a compose link — and it works as before, for any
player on the platform.

**Why this priority**: A regression guard. The people search behind these flows shares its
origin with today's inbox search; narrowing the inbox search must not narrow them.

**Independent Test**: With a player who shares no conversation with a target, use the
new-chat picker and the profile Message action to reach that target, and confirm both still
work; confirm a blocked player is still not offered.

**Acceptance Scenarios**:

1. **Given** a player who shares no conversation with a target, **When** they search for
   that target in the new-chat picker, **Then** the target is offered.
2. **Given** the same player, **When** they use the profile Message action on the target,
   **Then** they land in the existing direct chat or in a compose view, as today.
3. **Given** a blocked player, **When** the blocker searches in the new-chat picker,
   **Then** the blocked player is not offered.

---

### Edge Cases

- **Short terms**: fewer than two characters leaves the inbox as it is — no search runs,
  no empty state appears — matching today's behaviour.
- **Hidden conversations**: a conversation the player has hidden from the inbox is not
  surfaced by search either; search mirrors the inbox's visibility (owner decision, see
  Clarifications). Recovering a hidden conversation is a separate, pre-existing gap.
- **Blocked direct chats**: a direct conversation hidden from the inbox by a block stays
  hidden from search.
- **Former members**: a player who left a group, or was removed from a team or party
  roster, no longer causes that conversation to match — membership is as of now. For an
  archived chat, the members preserved at archival are the members.
- **Unavailable members**: a member whose profile is gone or hidden (shown as a neutral
  placeholder such as "A former player") is not matchable, and typing the placeholder text
  lists nothing.
- **Accents and case**: "rosch" finds "Rösch"; "LENA" finds "Lena" — consistent with every
  other search in the product.
- **Party chats**: a party chat carries no name of its own, so it is found through its
  members, like a direct chat.
- **Admin-contact threads**: the requester sees the team's or event's name; an admin sees
  the requester's name together with the team or event — each matches the name that player
  actually sees.
- **Live updates while searching**: a message arriving while results are showing does not
  reorder or clear the results; clearing the term shows the current inbox.
- **Many results**: a common name in a large inbox yields a bounded list, most recently
  active first, exactly as the inbox itself is bounded.

## Requirements *(mandatory)*

### Functional Requirements

**What is searched**

- **FR-001**: The inbox search MUST match a term only against (a) the names of the people
  who are currently members of the player's conversations and (b) the name the inbox shows
  for each conversation. It MUST NOT match message text.
- **FR-002**: A conversation MUST be listed when at least one of its current members'
  names contains the term, or its shown name contains the term. Matching MUST be
  insensitive to case and to accents.
- **FR-003**: The conversations eligible to be listed MUST be exactly those the player's
  inbox would show: conversations they are a member of, not hidden by them, and — for
  direct chats — not with a player either side has blocked.
- **FR-004**: Search MUST cover all of the player's eligible conversations, including any
  beyond the portion of the inbox currently displayed.

**How results are shown**

- **FR-005**: Each result MUST be presented as the conversation's inbox row — the same
  avatar, name, last-message preview, time, unread badge and kind tag — and selecting it
  MUST open that conversation.
- **FR-006**: Results MUST be ordered as the inbox is, most recently active first, and
  MUST be bounded (019 FR-006).
- **FR-007**: While a term of at least two characters is present, results MUST replace the
  inbox list; clearing the term MUST restore the full inbox. A shorter term MUST leave the
  inbox unchanged.
- **FR-008**: A search with no matches MUST show a plain empty state that names the term,
  never an error.
- **FR-009**: The search field's visible hint and its accessible label MUST describe what
  it searches — people and conversations by name — and MUST NOT describe it as searching
  messages, in every supported language.

**What is removed**

- **FR-010**: Message-text search MUST NOT be offered anywhere in the product, and MUST
  NOT be obtainable by a direct request that bypasses the interface. The capability is
  removed, not hidden.
- **FR-011**: No search result MUST include a message excerpt, snippet or grouping of
  messages.

**What is unchanged**

- **FR-012**: The people search used to start a new chat — the new-chat picker, the
  compose-by-handle route (feature 022) and the profile Message action (feature 021) — MUST
  be unchanged: it MUST still reach any player (019 FR-049), still exclude blocked players
  (019 FR-033) and still resolve to an existing direct conversation where one exists
  (019 FR-008).
- **FR-013**: The 019 requirements FR-034, FR-035 and FR-036 and success criterion SC-006
  are superseded by this feature; the 019 specification MUST be annotated to point here so
  the source of truth stays accurate.

### Key Entities *(include if feature involves data)*

- **Conversation**: a chat thread as the inbox lists it — its kind, the name shown to this
  player, its current members, and its recency. Unchanged in shape; this feature only
  changes how it is found.
- **Member**: a person currently part of a conversation, as the conversation's member list
  shows them. Membership is live for team, party and admin-contact threads and stored for
  direct chats, groups and archived chats — exactly as 019 defines.
- **Search term**: the text the player types; at least two characters before anything
  happens.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a seeded player, typing part of a member's name lists **every**
  conversation that person shares with the player across direct, group, team and party
  chats, and **no** conversation that person is not part of — 100% in the seeded test.
- **SC-002**: A term that appears only inside message text, and in no member's or
  conversation's name, returns **zero** results — through the interface and by direct
  request.
- **SC-003**: A term present only in a conversation the player is **not** a member of
  returns **zero** results by direct request.
- **SC-004**: A conversation beyond the inbox's first displayed page is found by a
  member's name in 100% of attempts.
- **SC-005**: A player with 20 or more conversations can find and open a specific one by
  a member's name in under 10 seconds.
- **SC-006**: Starting a chat with a player the searcher shares nothing with still works
  from the new-chat picker and the profile Message action, and a blocked player is still
  not offered — all existing coverage for those flows continues to pass.
- **SC-007**: With no term entered, the inbox lists exactly the same conversations in the
  same order as before this change.

## Assumptions

- **Blocked direct chats stay out**: a direct chat the inbox suppresses because of a block
  is not found by search either — one rule for "what can I see in my inbox". (Hidden
  conversations are an owner decision, not an assumption; see Clarifications.)
- **Names include handles**: a member matches on display name **or** handle, as the
  product's existing people search does. The conversation-name match uses the label the
  inbox shows that particular viewer.
- **Order and bound**: results use the inbox's own order and bound; no separate ranking.
- **Minimum length and debounce**: the two-character minimum and the brief pause before
  searching stay as they are today.
- **Removal reaches the whole product**: message-text search is removed everywhere,
  including any request path outside the interface, because the inbox was its sole
  consumer. The people search used by other flows keeps working unchanged.
- **No new data**: nothing new is stored; the feature reads what the inbox already reads.
- **Copy changes in all three languages**: the field's hint and label, the results
  heading and the empty state are updated in every supported language at once.
- **Amends 019**: FR-034, FR-035, FR-036 and SC-006 in `specs/019-chat/spec.md` are
  annotated as superseded by 046, following the precedent of feature 022.
