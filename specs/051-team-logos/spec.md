# Feature Specification: Team Logos

**Feature Branch**: `051-team-logos`

**Created**: 2026-09-16

**Status**: Draft

**Input**: User description: "Teams have logos, we should add a way to upload and edit logos for the team very similar to profile pictures. This logo should also be displayed in the chat message header of this team chat as well as on the team browse list etc." (GitHub #305)

## Context

A player has had a picture since feature 004; a team has never had one. Every surface that
pictures a team renders a stand-in instead:

| Surface | Stand-in today |
|---|---|
| Team page header (`/t/<slug>`) | first letter of the name on a coral gradient tile |
| Browse → Teams rows | `TeamCardDto.LogoInitial` — the same letter, on a dashed tile |
| Onboarding team suggestions | the same `logoInitial`, same card shape |
| "My team" rows | `team.name.charAt(0)` |
| Team chat header | a plain grey circle |
| Team chat inbox row | a grey 2×2 cluster |

The chat case is documented in code rather than left to be discovered: feature 019 ships the
comment *"Team/party/event conversations have no crest image — there is no team-avatar endpoint
(issue #193), so their Url stays null and the client renders its cluster placeholder."* The gap
is an absent endpoint, not an absent design.

Everything needed to close it already exists. Feature 034 built a reusable, owner-agnostic image
pipeline whose profiles are explicitly named "per-context" so new contexts can join. Feature 035
built `IMediaStore`, a storage abstraction that "knows about objects and keys and nothing else —
no profiles, no teams, no badges", already used by three descriptor tables. This feature adds the
fourth owner and the surfaces that render it.

**This is the identity logo, not the showcase gallery.** Issue #99 draws that line itself: "This
is distinct from the identity avatar / team logo (a 1:1 replace-in-place image) — a showcase
gallery is a bounded 1:N collection." A team logo is one image, replaced in place, never a list.

## Clarifications

### Session 2026-09-16

- Q: Which surfaces show the logo? → A: **All of them** — team page header, browse-teams rows,
  onboarding suggestions, "My team" rows, and the team chat (header **and** inbox row). The chat
  was named in the request itself and is the surface that has no letter fallback at all today.
- Q: Can an admin remove a logo again, or only replace it? → A: **Remove is supported.** A profile
  avatar cannot be removed today, so this is new ground rather than parity — but a logo is a
  team's mark, a team can rebrand or lose the right to use one, and "you may never take it down"
  is not a defensible answer for a mark an admin uploaded on the team's behalf. Removing returns
  the team to the letter placeholder.
- Q: How is a non-square logo fitted to a square tile? → A: **Centre-crop to a square**, as
  profile avatars do — not the letterboxed fit used for badge and achievement icons. Every
  surface renders the logo in a square or circular tile; a letterboxed image would put a band of
  empty space inside a coloured tile on six screens. The accepted consequence is recorded under
  Assumptions: a wide wordmark loses its ends, irreversibly, because the original upload is
  discarded.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A team admin gives the team a logo (Priority: P1)

An admin of *Rheinfeuer* opens team settings, picks the club's crest from their phone, and sees
it appear in place of the letter *R*. Nothing else about the team changes. A teammate who is not
an admin sees the same settings page without the control.

**Why this priority**: Without the upload there is nothing to display anywhere. It is also the
whole of the authorization surface — every other story is a read.

**Independent Test**: As a team admin, upload an image from team settings and confirm the team
page header shows it; as a non-admin member and as a non-member, confirm the control is absent
and the endpoint refuses.

**Acceptance Scenarios**:

1. **Given** an admin is on their team's settings page, **When** they choose an image file,
   **Then** the logo is stored and the settings page shows the new logo without a page reload.
2. **Given** a team already has a logo, **When** an admin uploads a different image, **Then** the
   new image replaces it everywhere and the previous one is no longer retrievable.
3. **Given** a member who is not an admin opens team settings, **When** the page renders, **Then**
   no logo control is offered, and a request to change the logo made anyway is refused.
4. **Given** a player who is not a member of the team, **When** they attempt to change its logo,
   **Then** the attempt is refused and reveals nothing about the team beyond what they could
   already see.
5. **Given** an admin chooses a file that is not a supported image, or is too large, **Then** the
   upload is refused with a reason they can act on, and the team's existing logo is untouched.
6. **Given** an admin uploads a photograph with location metadata, **When** it is stored, **Then**
   the stored image carries no metadata from the original.

---

### User Story 2 - The logo appears wherever the team is pictured (Priority: P1)

A player browsing teams sees crests rather than a column of letters, recognises their rivals'
logo in the list, opens the team page and sees the same crest in the header. Their own teams in
"My team" and the onboarding suggestions show theirs.

**Why this priority**: It is the point of the upload. It ships with User Story 1 because a logo
nobody can see is not a feature; both together are the smallest useful release.

**Independent Test**: Give one team a logo, leave another without, and confirm every listed
surface shows the logo for the first and the unchanged placeholder for the second.

**Acceptance Scenarios**:

1. **Given** a team has a logo, **When** any signed-in player views the browse-teams list, the
   onboarding suggestions, the team page header, or their "My team" rows, **Then** the team's
   logo is shown in place of the letter tile.
2. **Given** a team has no logo, **When** the same surfaces render, **Then** they show exactly
   what they show today — the letter tile, unchanged.
3. **Given** a player is not signed in, **When** they reach a surface that would picture a team,
   **Then** nothing changes: those surfaces already require a session.
4. **Given** an admin replaces a logo, **When** the player returns to a surface that had already
   shown the old one, **Then** they see the new one rather than a cached image of the old.
5. **Given** a team's logo cannot be retrieved from storage, **When** a surface renders it,
   **Then** the surface falls back to the placeholder rather than showing a broken image.

---

### User Story 3 - The team chat is recognisable (Priority: P2)

A player with six conversations open can tell the team thread from the party thread at a glance,
because the team's crest is on its inbox row and at the top of the conversation.

**Why this priority**: It is the surface the request named, and the only one with no letter
fallback — a grey cluster carries no information at all. It ranks below the others only because
it depends on the same upload and adds no new authorization.

**Independent Test**: Give a team a logo, open the chat inbox as a member, and confirm the team
conversation's row and header both show it while the party conversation is unchanged.

**Acceptance Scenarios**:

1. **Given** a member of a team with a logo opens their chat inbox, **When** the team conversation
   row renders, **Then** it shows the team's logo instead of the grey cluster.
2. **Given** that member opens the team conversation, **When** the header renders, **Then** it
   shows the same logo next to the conversation name.
3. **Given** a team has no logo, **When** its conversation renders in either place, **Then** the
   existing cluster placeholder is shown, unchanged.
4. **Given** a player has asked a team's admins a question (a team inquiry), **When** they view
   that conversation, **Then** it is pictured with the team's logo — it is the team they are
   writing to. The admins' side of the same conversation keeps showing the asking player's
   avatar, unchanged.
5. **Given** a party or event conversation, **When** it renders, **Then** it is unchanged — this
   feature adds no party or event crest.

---

### User Story 4 - A logo can be taken down (Priority: P3)

A team rebrands, or an admin uploaded the wrong file and wants the tile blank rather than wrong.
They remove the logo and the team returns to its letter.

**Why this priority**: It completes the edit story the request asked for ("upload and edit"), but
a team can already correct a mistake by uploading a different image, so it is the part that can
ship last.

**Independent Test**: Upload a logo, remove it, and confirm every surface is back to the
placeholder and the stored image is gone.

**Acceptance Scenarios**:

1. **Given** a team with a logo, **When** an admin removes it, **Then** every surface returns to
   the placeholder it showed before the logo existed.
2. **Given** a team with no logo, **When** an admin removes the logo anyway, **Then** the request
   succeeds and changes nothing.
3. **Given** a non-admin, **When** they attempt to remove a logo, **Then** the attempt is refused.
4. **Given** a logo has been removed, **When** the removal is accepted, **Then** the stored image
   is deleted rather than left behind.

---

### Edge Cases

- **A team is deleted while it has a logo.** Deleting a team removes its rows; the stored image
  MUST be deleted too rather than left occupying storage forever (FR-019).
- **Two admins upload at the same moment.** One of the two images wins and the team ends with
  exactly one logo. Neither admin sees an error, and no image is left referenced by nothing.
- **An upload is accepted but the browser keeps showing the old image.** The address a logo is
  fetched from does not change when the image does, so the viewer who performed the upload MUST
  be shown the new one immediately (FR-013) rather than their browser's cached copy.
- **A logo's stored image goes missing** (an operational fault, or a sweep that miscounted).
  Every surface degrades to the placeholder; nothing shows a broken image and nothing errors.
- **A player who is not a member browses a team's logo address directly.** They see the logo:
  browse already shows them the team's name, city and size, so its mark is no more private. A
  signed-out caller sees nothing, as with every other team read.
- **An image that is technically valid but hostile** — a decompression bomb, a file whose
  declared type lies, an animated image. The existing processing pipeline is what decides;
  this feature adds no new acceptance path around it.

## Requirements *(mandatory)*

### Functional Requirements

**Setting a logo**

- **FR-001**: A team MUST be able to hold at most one logo at a time, replaced in place.
- **FR-002**: Only an **admin of that team** may set, replace, or remove its logo. The rule MUST
  be enforced server-side, independently of what the interface offers.
- **FR-003**: A non-member's attempt to change a team's logo MUST NOT be distinguishable from an
  attempt against a team that does not exist.
- **FR-004**: An uploaded image MUST be validated, guarded against decompression attacks, stripped
  of metadata, resized, and re-encoded before storage, on the same terms as every other image the
  platform accepts.
- **FR-005**: A stored logo MUST be a **centre-cropped square** (Clarifications).
- **FR-006**: The original upload MUST NOT be retained once the normalized image is stored.
- **FR-007**: A rejected upload MUST leave the team's existing logo untouched and MUST report a
  reason the uploader can act on, without exposing internal detail.
- **FR-008**: Replacing a logo MUST delete the image it replaces.

**Showing a logo**

- **FR-009**: A team's logo MUST be shown in place of the existing placeholder on: the team page
  header, the browse-teams rows, the onboarding team suggestions, the "My team" rows, the team
  chat conversation header, and the team chat inbox row.
- **FR-010**: A team **inquiry** conversation MUST show the team's logo on the asking player's
  side. The admins' side continues to show the asking player's avatar.
- **FR-011**: A team without a logo MUST render exactly as it does today on every surface above.
- **FR-012**: Any signed-in player MUST be able to see any team's logo. Team logos are not
  member-gated. A signed-out caller MUST NOT be served one.
- **FR-013**: After an admin sets or removes a logo, the surfaces visible to that admin MUST show
  the new state immediately, without a page reload and without a stale cached image.
- **FR-014**: A logo whose stored image cannot be retrieved MUST render as "no logo" rather than
  as an error or a broken image.
- **FR-015**: Party and event conversations, and every other surface not listed in FR-009, MUST be
  unchanged.

**Removing, and cleaning up**

- **FR-016**: An admin MUST be able to remove the team's logo, returning the team to its
  placeholder.
- **FR-017**: Removing a logo that does not exist MUST succeed without changing anything.
- **FR-018**: Removing a logo MUST delete the stored image.
- **FR-019**: Deleting a team MUST delete its stored logo image.
- **FR-020**: The media reconciliation sweep MUST treat a team logo's stored image as referenced.
  (The sweep deletes every object it cannot account for; a new kind of media that is not declared
  to it is not merely unswept — it is destroyed.)

### Key Entities

- **Team logo**: the descriptor for a team's identity image — which team owns it, where the image
  is stored, what type and size it is. One per team, optional. Deliberately a separate record from
  the team itself, matching how a player's avatar is separate from their profile, so that reading
  a team never reads its logo's storage details.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A team admin can set a logo and see it on the team page within one interaction from
  team settings, with no page reload.
- **SC-002**: A team with a logo is pictured by it on **6 of 6** surfaces listed in FR-009; a team
  without one is pictured identically to before this feature on all 6.
- **SC-003**: 100% of attempts by a non-admin to set or remove a logo are refused.
- **SC-004**: A replaced or removed logo's image is no longer retrievable from storage after the
  operation completes.
- **SC-005**: Browsing 20 teams costs no additional database round trips per row compared with
  today (the logo's presence is read in the same query as the row).
- **SC-006**: Every stored logo is a square WebP no larger than the configured ceiling, whatever
  was uploaded.
- **SC-007**: A reconciliation sweep run immediately after an upload leaves the uploaded image in
  place.

## Assumptions

- **A wide wordmark will be cropped.** The owner chose centre-crop over letterboxing, so a logo
  whose content spans a wide rectangle loses its ends. The original is discarded (FR-006), so this
  is not reversible by the platform — an admin who dislikes the crop re-uploads a squarer file.
  Accepted deliberately: six of six surfaces render into a square or circular tile.
- **No anonymous logo reads.** Feature 026 made every team surface authenticated, so there is no
  signed-out screen that would need one. If an anonymous team page is ever introduced, its logo
  visibility becomes a new decision rather than an inherited one.
- **No audit trail of logo changes.** Who changed a logo and when is not recorded beyond the
  record's own timestamps. Teams are small and admins are few; the team activity feed (044) is
  deliberately not extended here.
- **No moderation surface.** There is no capability anywhere in the product to remove content an
  admin uploaded other than by banning the account behind it. A logo is uploaded by a team admin
  and removable by a team admin; a platform admin has no logo control, and this feature does not
  build one.
- **Party and event crests are a separate question.** Both keep their placeholders. Nothing here
  prevents them later; nothing here presumes them.
