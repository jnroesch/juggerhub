# Feature Specification: Showcase Image Galleries for Player and Team Profiles

**Feature Branch**: `046-showcase-galleries`

**Created**: 2026-08-20

**Status**: Draft

**Input**: User description: "Showcase image galleries for player and team profiles (GH #99): up to 5 images per player profile and up to 5 per team, displayed on their public pages. Bounded 1:N collection distinct from the identity avatar/team logo. Reuses the #98 image processing pipeline and #97 media storage abstraction. CRUD (list, upload, reorder, delete) for both profile and team surfaces; hard cap of 5 enforced server-side; visibility + ban gating consistent with existing avatar rules; gallery UI on public profile and team pages per DESIGN.md."

## Clarifications

### Session 2026-08-20

- Q: Should each showcase image carry an owner-supplied caption? (#99 open question 1) → A: Yes — one **optional** plain-text caption of at most 120 characters per image, editable and removable after upload. No separate title. The caption doubles as the image's accessible text alternative, which an unlabelled photo otherwise lacks. (FR-005, FR-009, FR-028, FR-029)
- Q: How should showcase images be processed and sized? (#99 open question 2) → A: A **new showcase processing profile**, separate from the avatar one: **fit** within bounds preserving aspect ratio (never square-crop, never upscale), longest side **1280 px**, stored ceiling **1 MB**. Square-cropping would cut the subject out of exactly the pictures this feature exists to show. Caps a full five-image gallery at 5 MB per owner. (FR-014, SC-005)
- Q: Who may add, reorder, and remove a team's showcase images? → A: **Team admins only.** Matches every other team-presentation surface, and keeps the platform from carrying member-posted content it has no tooling to moderate — banning an account remains the only lever. (FR-008, US2 scenario 3)
- Q: Is an enlarged (lightbox) view in scope, or thumbnails only? → A: **In scope**, with next/previous paging, Escape-to-close, and focus restored — otherwise the pictures are only ever seen as thumbnails, which undercuts the point of a showcase. (US3, FR-027, SC-007)
- Q: Independent 5-caps for profile vs team? (#99 open question 3) → A: **Yes**, as the issue assumed — counted per owner and never pooled. (FR-003, US2 scenario 6)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A player showcases their best moments (Priority: P1)

A player has a profile that today shows a small round identity picture and some text. They want to show what playing actually looks like for them: a photo from a tournament, a shot of their pompfen, a team huddle. From their own profile they add up to five pictures, see them appear immediately, and can remove or reorder them later. Anyone entitled to see their profile now sees those pictures on it.

**Why this priority**: This is the smallest end-to-end slice that delivers the feature's whole point — a player gets a showcase, and viewers see it. It is independently valuable without the team half, and every other story either mirrors it (Story 2), protects it (Story 4), or refines it (Story 3).

**Independent Test**: On a profile with no pictures, add one; confirm it appears on the owner's profile and on the profile as another member sees it. Add four more; confirm all five show in the order chosen. Remove one; confirm it disappears everywhere.

**Acceptance Scenarios**:

1. **Given** a player with an empty showcase, **When** they add a valid picture, **Then** it is accepted, normalized, stored, and appears on their profile without a page reload.
2. **Given** a player whose showcase holds five pictures, **When** they attempt to add a sixth, **Then** the attempt is refused with a plain-language message naming the limit, and the existing five are unchanged.
3. **Given** a player with several showcase pictures, **When** they change the order, **Then** the new order is kept and every viewer sees that order on the next load.
4. **Given** a player with showcase pictures, **When** they remove one, **Then** it disappears from their profile for every viewer and the remaining pictures keep their relative order with no gap.
5. **Given** a player with an empty showcase, **When** another member views their profile, **Then** no empty gallery frame or broken image is shown to that viewer.
6. **Given** a player adds a picture, **When** the identity picture (avatar) is inspected, **Then** it is unchanged — the showcase and the avatar are separate and neither replaces the other.

---

### User Story 2 - A team shows what the team is like (Priority: P1)

A team admin opens the team page and adds up to five pictures of the team — training, a tournament, the squad. Every member of the platform who can reach that team page sees them. Ordinary members of the team can look but not change; the gallery is part of what the team's admins present.

**Why this priority**: Equal-highest with Story 1 and the second half of the issue's scope. It is a separate surface with a separate audience and separate permissions, so it is independently deliverable and independently testable — but it shares the same mechanism, which is the reason the issue pairs them.

**Independent Test**: As a team admin, add pictures to a team with an empty gallery; confirm they appear on the team page. Sign in as an ordinary member of that team and as a signed-in non-member; confirm both see the pictures and neither is offered add, reorder, or remove.

**Acceptance Scenarios**:

1. **Given** a team admin, **When** they add a valid picture to the team gallery, **Then** it is accepted and appears on the team page.
2. **Given** a team gallery holding five pictures, **When** an admin attempts to add a sixth, **Then** the attempt is refused with a plain-language message naming the limit.
3. **Given** an ordinary (non-admin) team member, **When** they view the team page, **Then** they see the gallery but are offered no way to add, reorder, or remove pictures, and any direct attempt to do so is refused.
4. **Given** a signed-in member who does not belong to the team, **When** they view the team page, **Then** they see the team's gallery exactly as members do.
5. **Given** a team is deleted, **When** the deletion completes, **Then** its showcase pictures are removed with it and no stored image is left reachable.
6. **Given** a player's showcase already holds five pictures, **When** a team they belong to adds five of its own, **Then** both succeed — the limits are counted per owner, not shared.

---

### User Story 3 - Viewers can look at a picture properly (Priority: P2)

A visitor looking at a profile or team page sees the showcase as a row of thumbnails. Tapping one opens it large enough to actually see, and they can move to the next and previous picture and close it again — on a phone as well as a desktop.

**Why this priority**: Thumbnails alone technically satisfy "galleries render", but a showcase nobody can see properly does not deliver the value the issue asks for. It is sequenced after the mechanism exists because it is presentation over an already-working collection.

**Independent Test**: With a gallery of three pictures, open the first at full size, move forward and back through all three, and close — using a pointer, using a keyboard, and on a narrow (375 px) viewport.

**Acceptance Scenarios**:

1. **Given** a gallery with several pictures, **When** a viewer activates one, **Then** it is shown enlarged with a clear way to close it.
2. **Given** an enlarged picture, **When** the viewer moves to the next or previous, **Then** the enlarged view follows the gallery order and stops sensibly at the ends.
3. **Given** an enlarged picture, **When** the viewer uses only a keyboard, **Then** they can move between pictures and close the view, and focus returns to where it was.
4. **Given** a narrow phone-width screen, **When** the gallery and the enlarged view are shown, **Then** both remain usable with no horizontal page scrolling and no cropped controls.

---

### User Story 4 - The showcase does not open a privacy hole (Priority: P1)

A player who keeps their profile private adds showcase pictures. A signed-out visitor who somehow knows the addresses cannot see them, exactly as they cannot see that player's identity picture today. A player whose account has been banned has no pictures reachable by anyone.

**Why this priority**: Equal-highest, and inseparable from Story 1. The platform has already decided who may see a member's picture (feature 026 visibility, the banned-account rule). Adding five more pictures per member with a weaker gate would silently reverse that decision at five times the volume. Neither story may ship without the other.

**Independent Test**: With showcase pictures on a private profile, request the list and each image (a) signed out, (b) signed in; confirm (a) is refused and (b) succeeds. Repeat on a public profile and confirm (a) now succeeds. Ban the account and confirm every route is refused.

**Acceptance Scenarios**:

1. **Given** a private profile with showcase pictures, **When** a signed-out visitor requests the gallery listing or any of its images, **Then** the request is refused and no image is returned by any route.
2. **Given** a public profile with showcase pictures, **When** a signed-out visitor views it, **Then** the gallery is shown, exactly as that profile's identity picture is today.
3. **Given** an account that is banned, **When** anyone requests that account's showcase listing or images, **Then** nothing is returned.
4. **Given** a member who switches their profile from public to private, **When** the very next request for one of their showcase images arrives from a signed-out visitor, **Then** it is refused — there is no window in which a previously fetched address keeps working.
5. **Given** any team's showcase, **When** a signed-out visitor requests its listing or images, **Then** the request is refused, because the team surface is signed-in-only.
6. **Given** any showcase image, **When** someone tries to reach it directly in the underlying media store, **Then** the store refuses, and no address, key, or link to the stored object ever appears in any response.
7. **Given** a member who is not the profile owner, **When** they attempt to add, reorder, or remove a picture on that profile, **Then** the attempt is refused.

---

### User Story 5 - A bad upload fails clearly and changes nothing (Priority: P2)

Someone picks the wrong file — a PDF, a 40-megapixel panorama, a corrupt download, or a file so large it never should have been sent. They are told plainly what was wrong, their existing pictures are untouched, and they can try again straight away.

**Why this priority**: Upload is the one place a member hands the platform arbitrary bytes, so it is also where the experience most often breaks. The processing pipeline already classifies these failures; this story is about surfacing them honestly rather than as a generic error, and about not leaving half-added pictures behind.

**Independent Test**: Attempt uploads of: a non-image file, an image far above the accepted input size, an image with an absurd pixel count, and a truncated/corrupt image. Confirm each is refused with a distinguishable, non-technical message and that the gallery contents and order are unchanged afterwards.

**Acceptance Scenarios**:

1. **Given** any gallery, **When** an upload is refused for any reason, **Then** the gallery's contents and order are exactly as they were before the attempt.
2. **Given** a file that is not an accepted image type, **When** it is uploaded, **Then** it is refused with a message about the file type — and the decision is made on the file's actual content, not on what the upload claims it is.
3. **Given** a picture that exceeds the accepted input size or pixel count, **When** it is uploaded, **Then** it is refused with a message about size rather than a technical error.
4. **Given** the media store is temporarily unreachable, **When** an upload is attempted, **Then** the member is told the upload could not be completed and is invited to retry, and no record is left describing a picture that was never stored.
5. **Given** the gallery is loading or fails to load, **When** the profile or team page is shown, **Then** the page shows a loading state and, on failure, an error state with a retry — never a permanently blank frame.

---

### Edge Cases

- **Concurrent adds racing the cap**: two uploads submitted at nearly the same moment for an owner already holding four pictures — the server admits exactly one; the second is refused by the same limit message a sixth would get.
- **Reorder referencing a deleted picture**: a reorder submitted from a stale page that still lists a picture someone else (a co-admin) has since removed — the reorder is refused as a whole rather than partly applied, and the client reloads.
- **Reorder that is not a permutation** (duplicates, missing entries, foreign entries, wrong length) — refused; no partial ordering is written.
- **The same person is an admin of several teams**: limits and galleries are per team, never pooled across the teams one person administers.
- **A team's uploader later leaves the team or is banned**: the picture belongs to the team, not to the person who added it, so it stays — a team gallery is not hidden because of one member's account standing.
- **A gallery picture whose stored image has vanished** (reconciliation, storage incident): the entry degrades to the same "no picture" outcome the avatar already has, and the surrounding gallery still renders.
- **Deleting the last picture** returns the gallery to its empty state, not to a broken or zero-height frame.
- **Account deletion**: a deleted member's showcase pictures are removed along with the rest of their profile media; nothing survives pointing at a person who no longer exists.
- **Very tall or very wide source pictures** (panoramas, portrait phone shots): thumbnails stay a uniform grid and the enlarged view shows the whole picture rather than cropping its subject away.

## Requirements *(mandatory)*

### Functional Requirements

**Collection and limits**

- **FR-001**: A player profile MUST be able to hold an ordered showcase collection of **at most 5** images; a team MUST be able to hold its own ordered showcase collection of **at most 5** images.
- **FR-002**: The limit MUST be enforced on the server for every add, independently of anything the client shows or sends; a client-side hint that the gallery is full is a convenience only.
- **FR-003**: The two limits MUST be counted per owner and MUST NOT be pooled — a member's own five and each of their teams' five are separate.
- **FR-004**: The showcase MUST be distinct from the identity picture (avatar): adding, reordering, or removing a showcase image MUST never change the identity picture, and vice versa.
- **FR-005**: Each showcase image MUST carry an owner-supplied caption of at most 120 characters. The caption is **optional** — a picture with no caption is a normal, complete picture, not an incomplete one.
- **FR-006**: Every showcase collection MUST have a defined, stable order that is the same for every viewer, and MUST NOT depend on upload time once an owner has ordered it.

**Managing a gallery**

- **FR-007**: A profile's owner MUST be able to list, add, reorder, and remove images in their own showcase. No other member may do any of those, and platform administrators gain no new capability from this feature.
- **FR-008**: A **team admin** MUST be able to list, add, reorder, and remove images in that team's showcase. Ordinary team members and non-members MUST be able to view it but MUST NOT be able to change it.
- **FR-009**: A caption MUST be editable after upload without re-uploading the picture, and MUST be removable (cleared back to no caption).
- **FR-010**: Reordering MUST be submitted as a complete new order for the collection and MUST be applied all-or-nothing; an order that is not a valid permutation of exactly the collection's current members MUST be refused with nothing written.
- **FR-011**: Removing an image MUST remove both its record and its stored image, leaving no unreferenced stored object behind on the ordinary path, and MUST leave the remaining images contiguously ordered.
- **FR-012**: When an owner is deleted (a team is deleted, a member's account is deleted), that owner's showcase images MUST be removed with it, records and stored images alike.

**Upload handling**

- **FR-013**: Every uploaded showcase image MUST pass through the same server-side processing the platform already applies to uploaded pictures: validation against the file's actual content, a pixel-count guard applied before decoding, metadata stripping, resizing, and re-encoding to the platform's stored image format.
- **FR-014**: Showcase images MUST be processed with a **showcase-sized profile distinct from the avatar profile**: fitted within its bounds preserving aspect ratio (never cropped to a square, never upscaled), with a longest side of **1280 px** and a stored-size ceiling of **1 MB** per image.
- **FR-015**: A refused upload MUST leave the gallery's contents and order exactly as they were, MUST NOT consume a slot, and MUST NOT leave a record describing an image that was not stored.
- **FR-016**: Refusal reasons MUST be distinguishable and non-technical, at least: not an image / unsupported type, too large, too many pixels, unreadable, gallery full. No stack trace, no internal identifier, and no storage location may reach the client.
- **FR-017**: Uploads MUST be rate-limited per caller, and reads of showcase images MUST be rate-limited consistently with existing media reads.

**Visibility, standing, and disclosure**

- **FR-018**: A profile's showcase — its listing and its images alike — MUST be visible exactly where that profile's identity picture is visible today: to any signed-in member, and to a signed-out visitor only when the profile's owner has made it public.
- **FR-019**: A banned account's showcase MUST NOT be returned to anyone by any route, listing or image.
- **FR-020**: A team's showcase MUST require a signed-in caller, matching the rest of the team surface; it MUST NOT widen for members nor narrow for signed-in non-members.
- **FR-021**: A change in visibility or account standing MUST take effect on the very next request; no previously issued address may keep returning an image the viewer is no longer entitled to.
- **FR-022**: The location of a stored image in the media store MUST NEVER be disclosed — not in a listing, a response header, a link, or an error.
- **FR-023**: Every refusal to serve a showcase image MUST be indistinguishable between "does not exist", "not permitted", and "temporarily unavailable", so the endpoint cannot be used to test whether a member or a picture exists.
- **FR-024**: An entry whose stored image cannot be fetched MUST degrade to the ordinary "no picture" outcome rather than an error, and MUST NOT prevent the rest of the gallery from rendering.

**Presentation**

- **FR-025**: A profile page and a team page MUST show that owner's showcase, laid out per DESIGN.md, on both phone and desktop widths.
- **FR-026**: The gallery MUST have distinct loading, error (with retry), and empty states. When a viewer who cannot edit the gallery looks at an owner with no pictures, the gallery MUST be absent rather than shown as an empty frame; an owner or team admin who can add pictures MUST see an invitation to add them.
- **FR-027**: A viewer MUST be able to open any showcase image enlarged, move to the next and previous image, and close it, using pointer, keyboard, or touch, with focus returned on close.
- **FR-028**: Each image MUST carry an accessible text alternative; where a caption exists it is used, and where none exists a sensible generic alternative naming the owner MUST be used.
- **FR-029**: A caption MUST be displayed with its image and MUST be treated as untrusted member-supplied text wherever it is shown.
- **FR-030**: All member-visible text introduced by this feature MUST be available in all three supported languages.

### Key Entities *(include if data involved)*

- **Profile showcase image**: one picture in a player's showcase. Belongs to exactly one profile; carries its position in the collection, an optional caption, and the descriptive details needed to authorize and serve it (what it is, how big, where it is stored). Bounded to five per profile. Inherits the same account-standing rule as the profile's identity picture.
- **Team showcase image**: the same shape, belonging to exactly one team. Bounded to five per team. Not tied to the standing of whoever uploaded it.
- **Existing — Player profile**: gains a showcase collection alongside its identity picture; its public/private setting governs who may see that collection.
- **Existing — Team**: gains a showcase collection; its admins govern the collection, and the platform's signed-in-only team surface governs who may see it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A member can add a picture to their showcase and see it on their profile in **under 30 seconds** from opening the profile, on a normal phone connection, without instruction.
- **SC-002**: Under every attempted route, an owner's showcase never exceeds **5** images: a burst of 10 simultaneous adds against an empty gallery leaves exactly 5 stored and 5 refusals reported.
- **SC-003**: **100%** of attempts to view a private member's showcase from a signed-out session are refused, and **100%** of attempts to view a banned member's showcase are refused, across both the listing and every image address.
- **SC-004**: **Zero** responses anywhere in the feature contain a media-store location, key, or direct link — verified by inspecting every response body and header the feature can produce.
- **SC-005**: Every stored showcase image is at most **1 MB**, so a full five-image gallery costs at most **5 MB** — measured over a corpus of deliberately oversized and high-resolution uploads.
- **SC-006**: Every refused upload leaves the gallery byte-identical to its prior state — **zero** slots consumed and **zero** records describing an unstored image, across the full set of failure categories in FR-016.
- **SC-007**: A viewer can open, page through, and close a five-image gallery using **keyboard only**, and the same flow works at a **375 px** viewport width with no horizontal page scrolling.
- **SC-008**: A profile or team page that shows a gallery issues **no more than one** listing request for it per page load, and a page for an owner with no pictures issues **no** image requests at all.
- **SC-009**: Every member-visible string added by this feature exists in **all three** language catalogues, with **zero** missing keys.
- **SC-010**: Removing a picture, deleting a team, or deleting an account leaves **zero** reachable stored images for the removed subject, verified after the operation completes.

## Assumptions

- **Reuses, does not rebuild**: the server-side processing pipeline (#98 / feature 034) and the media storage abstraction (#97 / feature 035) are already merged and in service; this feature adds a new owner kind to them and introduces no second way to store or process a picture.
- **Teams have no logo today.** The issue's phrase "identity avatar / team logo" describes the *pattern*; no team logo exists in the product. A team's showcase is therefore its first image media, and a team logo remains out of scope — this feature must not become a back-door team logo.
- **Team admins govern the team gallery** and **the two 5-caps are independent** — both settled by the owner in Clarifications, not assumed.
- **The cap is a fixed platform constant, not configuration** — five, the same in every environment, mirroring how other bounded platform limits are handled. Changing it later is a code change.
- **No moderation, reporting, or takedown capability** is added. The platform has no content-removal tooling beyond banning an account, and this feature does not invent any; a banned account's showcase disappears with everything else of theirs, which remains the only lever.
- **No cross-surface reuse**: showcase pictures appear on the profile and team pages only. Browse lists, cards, search results, chat, and the home dashboard are unchanged and keep using the identity picture.
- **Captions are plain text only** — no links, no formatting, no mentions.
- **Existing data**: nothing to migrate. Every showcase starts empty.
- **Ordering is dense and 0-based within an owner**; the platform maintains contiguity after a removal rather than leaving gaps.
- **Anonymous reach** follows the two surfaces' existing rules and is not widened: profile galleries can be reached signed-out for public profiles; team galleries never can.

## Dependencies

- **#98 / feature 034** — server-side image processing pipeline (merged). Supplies validation, the pixel guard, metadata stripping, resize, and re-encode; this feature adds a showcase processing profile alongside the existing avatar and icon profiles.
- **#97 / feature 035** — media storage abstraction and object storage (merged). Supplies object storage, the read-response shaping (caching, validators, no key disclosure), the orphan reconciliation sweep, and the media read rate limit; this feature adds a new media kind to it.
- **Feature 026** — authenticated-only access, which defines what "public" means on each of the two surfaces.
- **Feature 013** — account standing (banned accounts), whose rule the profile showcase inherits.
- **Feature 037** — account deletion, whose erasure path must account for the new media.
- **DESIGN.md** — governs the gallery, its thumbnails, its enlarged view, and its empty/loading/error states.

## Out of Scope

- A team logo or any other identity picture for teams.
- Showcase galleries on any other entity (events, parties, marketplace listings, trainings).
- Video, animated images, or any media that is not a still picture.
- Albums, tags, per-image visibility, likes, comments, or any social interaction on a picture.
- Member-facing reporting, moderation, or takedown tooling.
- Raising, lowering, or making configurable the limit of five.
- Changing anything about the existing identity picture (avatar) behaviour.
- Showing showcase pictures anywhere other than the profile and team pages.
