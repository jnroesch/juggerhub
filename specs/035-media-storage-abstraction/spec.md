# Feature Specification: Media Storage Abstraction + Object Storage

**Feature Branch**: `035-media-storage-abstraction`

**Created**: 2026-07-31

**Status**: Draft

**Input**: User description: "Media storage abstraction + object/blob storage (GitHub issue #97). Introduce an owner-agnostic storage seam (e.g. `IMediaStore`) so binary media lives outside the primary Postgres row, backed by Azure Blob Storage in deployed environments with a local emulator (Azurite) in docker-compose for parity. Carries forward the storage half of #13; sibling tickets: image processing #98 (feature 034, already merged) and showcase galleries #99 (next). Today three entities store bytes inline as Postgres `bytea`: `ProfileAvatar`, `BadgeIcon` and `AchievementIcon`. Rationale: avatars alone were fine inline — tiny and singular — but the upcoming bounded 5-image galleries per profile AND per team multiply stored bytes and pull them over the wire on every page view. Scope: define the storage-backend interface behind the existing service seams; keep a metadata row in Postgres for auth/visibility/content-type and redirect only the byte fetch, because the avatar read currently enforces the feature-026 visibility gate and a global banned-user query filter — that gate MUST be preserved, so blob URLs must not become an unauthenticated bypass. Azure Blob provisioned once in Terraform, present in every environment identically in shape. No Key Vault. Data migration required for existing bytes. No change to public URLs or frontend behavior."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pictures load exactly as before, but the bytes no longer live in the primary database (Priority: P1)

A member opens their profile, a team page, or the player directory. Avatars, badge icons, and achievement icons appear exactly as they always have — same addresses, same images, same speed or better. Nothing about the experience signals that the underlying picture files now live in a dedicated media store rather than inside the application's primary database records.

**Why this priority**: This is the whole point of the change and the smallest slice that delivers standalone value. Moving bytes out of the primary database is what makes the upcoming bounded galleries (per profile *and* per team) affordable; if the move is invisible to members, the feature has succeeded. Every other story protects or enables this one.

**Independent Test**: Upload a new avatar, then fetch it through the existing public address. Confirm the image returned is byte-identical to what was stored, that the address and response shape are unchanged, and that the primary database record for that picture no longer carries the image bytes.

**Acceptance Scenarios**:

1. **Given** a member with no picture, **When** they upload a valid image, **Then** the image is stored in the media store, a lightweight record describing it is kept alongside the owning entity, and fetching the picture through the unchanged public address returns that image.
2. **Given** a member who already has a picture, **When** they upload a replacement, **Then** subsequent fetches return only the new image and the superseded stored object is not left behind indefinitely.
3. **Given** any picture-bearing entity (member avatar, badge icon, achievement icon), **When** its picture is fetched, **Then** the response content type and image content match what was stored, with no change to the address the frontend calls.
4. **Given** a member deletes their picture (or the owning entity is deleted), **When** the deletion completes, **Then** both the descriptive record and the stored object are removed and later fetches report "no picture" as they do today.

---

### User Story 2 - Moving the bytes does not open a privacy hole (Priority: P1)

A member keeps their profile private. Another person — signed out, or holding nothing but a guessed address — tries to reach that member's picture directly. They are refused, exactly as they are today. The same holds for a member whose account has been banned: their picture is not reachable, whatever route is attempted.

**Why this priority**: Equal-highest with Story 1 and inseparable from it. The current read path enforces the feature-026 visibility rule (a private profile's avatar is not served to an anonymous caller) and the global banned-account filter *inside the query that loads the bytes*. Moving bytes to a separate store breaks that coupling by construction, so the gate must be deliberately re-established. Shipping Story 1 without Story 2 would silently reverse a privacy decision the platform has already made — a regression severe enough that neither story may ship alone.

**Independent Test**: With a private member's picture stored, attempt to fetch it (a) anonymously through the public address, (b) as a signed-in member, and (c) directly against the media store. Confirm (a) is refused, (b) succeeds, and (c) is refused. Repeat for a **public** member's picture and confirm (a) now succeeds. Repeat for a banned member's picture and confirm all routes are refused.

**Acceptance Scenarios**:

1. **Given** a private profile, **When** an anonymous caller requests that profile's picture, **Then** the request is refused exactly as it is today and no image bytes are returned by any route.
2. **Given** a private profile, **When** a signed-in member requests that profile's picture, **Then** the picture is returned.
3. **Given** a banned account, **When** anyone requests that account's picture, **Then** it is not returned.
4. **Given** any stored picture, **When** someone who knows or guesses its location in the media store requests the store directly, **Then** the store refuses — it is not readable by the general public in any environment.
5. **Given** a **public** profile, **When** an anonymous visitor views it, **Then** its avatar, badge icons, and achievement icons are all displayed, exactly as they are today.
6. **Given** a member who makes their profile private, or an account that is banned, **When** the very next request for their picture arrives, **Then** it is refused — there is no window in which previously-granted access keeps working.

---

### User Story 3 - The cutover is clean, with no half-migrated state left behind (Priority: P2)

An operator deploys the change to an environment that already holds pictures stored the old way. Those pictures are discarded as part of the cutover — the owner has accepted that loss (see Clarifications). What matters is that afterwards there is exactly one storage mechanism: no orphaned inline bytes, no records pointing at objects that were never written, and no member left looking at a broken image. Members whose picture was dropped simply see the placeholder they would see before uploading anything, and can upload again; catalogue icons are re-seeded by an administrator.

**Why this priority**: Sequenced after the mechanism exists — there is nothing to cut over to until Stories 1 and 2 are built. It ranks P2 because the correctness that matters is *end-state consistency*, not data preservation: the owner has waived preservation, so the risk is a half-migrated environment, not lost pictures.

**Independent Test**: Start from an environment holding inline pictures, deploy, and confirm that no inline byte storage remains, that no descriptive record points at a missing object, that every affected owner reports "no picture" cleanly rather than erroring, and that a fresh upload afterwards works end to end.

**Acceptance Scenarios**:

1. **Given** an environment holding pictures stored inline, **When** the change is deployed, **Then** the inline byte storage is gone and no descriptive record survives pointing at an object that was never written.
2. **Given** a member whose picture was discarded by the cutover, **When** they view their profile, **Then** they see the standard no-picture placeholder — not a broken image or an error — and can upload a new picture successfully.
3. **Given** the deployment is applied to an environment that has already been cut over, **When** it runs again, **Then** it completes without error and changes nothing.
4. **Given** the cutover has completed, **When** an administrator re-uploads the catalogue icons, **Then** they are stored in the media store and display everywhere icons are shown.

---

### User Story 4 - The same storage shape exists in every environment (Priority: P2)

A developer runs the stack on their machine and uploads a picture; it works exactly as it does in Dev and Prod, against a local stand-in for the media store rather than a cloud account. An operator provisioning a new environment gets the media store from the same single infrastructure definition that produced the others, differing only in sizing and configuration.

**Why this priority**: Environment parity is a non-negotiable platform principle, but it is a delivery quality of the mechanism rather than a separate user-facing capability, so it follows the mechanism itself. Getting it wrong means "works on my machine" defects that only appear after deployment.

**Independent Test**: Bring the stack up locally with no cloud credentials, upload and fetch a picture successfully; then confirm the infrastructure definition declares the media store once and that each environment's settings differ only in sizing/configuration values, not in which resources exist.

**Acceptance Scenarios**:

1. **Given** a developer machine with no cloud account access, **When** the stack is started from the standard local setup, **Then** picture upload and retrieval work end to end against the local stand-in.
2. **Given** the infrastructure definition, **When** any environment is provisioned from it, **Then** the media store resource is present in that environment with the same shape as every other environment.
3. **Given** deployment configuration, **When** the application is deployed, **Then** the credentials and connection settings for the media store are supplied from environment-level configuration and no credential value is present in the repository.

---

### User Story 5 - A media store outage degrades gracefully and never corrupts state (Priority: P3)

The media store becomes briefly unreachable. Members browsing the site still see pages render — profiles, teams, and lists all work; only the pictures are missing or fall back to their placeholder. A member who tries to upload during the outage gets a clear "try again" message rather than a broken profile, and once the outage ends nothing is left half-written.

**Why this priority**: The platform gains a new external dependency in the request path, which is exactly the situation the resilience principle exists for. It ranks P3 because it governs behavior during an abnormal condition rather than the primary flow, but it must be settled before the feature is considered done.

**Independent Test**: Make the media store unreachable, then (a) load pages that display pictures, (b) attempt an upload, and (c) restore the store. Confirm pages render with placeholders, the upload fails with a clear non-technical message and leaves no descriptive record pointing at a nonexistent object, and normal service resumes without manual cleanup.

**Acceptance Scenarios**:

1. **Given** the media store is unreachable, **When** a page containing pictures is loaded, **Then** the page renders and the missing pictures degrade to their existing placeholder rather than failing the page.
2. **Given** the media store is unreachable, **When** a member uploads a picture, **Then** they receive a clear, non-technical failure message and their previous picture (if any) is unchanged.
3. **Given** an upload where the object is stored but the accompanying record cannot be saved, **When** the operation fails, **Then** the system does not leave a permanently orphaned object without a means of reclaiming it.
4. **Given** a descriptive record whose stored object is missing, **When** the picture is fetched, **Then** the response is the same "no picture" outcome members already see, not an error page or a leaked technical detail.

---

### Edge Cases

- **Record without object**: a descriptive record exists but the object is absent from the media store (failed migration, manual deletion, wrong environment) — the fetch reports "no picture" and the condition is observable to operators.
- **Object without record**: an object exists that nothing references (interrupted upload, replaced picture, deleted owner) — it must be reclaimable rather than accumulating forever.
- **Concurrent uploads**: the same member uploads two pictures at nearly the same moment — exactly one wins, the loser's object does not become the served picture, and no object is left orphaned without a means of reclamation.
- **Replacement**: uploading a new picture over an existing one must not leave the old object permanently addressable if that would let a superseded picture outlive its deletion.
- **Cross-environment contamination**: Dev and Prod must never read or write each other's objects even if they share an account or a naming scheme.
- **Deleted owner**: banning, deleting, or anonymizing an account must not leave that account's picture retrievable.
- **Guessed addresses**: the location of an object in the media store must not be derivable in a way that turns a public identifier into an unauthenticated byte fetch for gated media.
- **Post-cutover empty state**: every owner whose picture was discarded by the cutover must behave exactly like an owner that never had one — placeholder, not error.
- **Large future volume**: the mechanism must not require reading objects to render a list page — list projections must remain free of image bytes, as they are today.

## Clarifications

### Session 2026-07-31

- **Q: Which media kinds move in this feature?** → **A: All three.** `ProfileAvatar`, `BadgeIcon`, and
  `AchievementIcon` all move to the media store, leaving zero inline media bytes in the primary
  database. This proves the seam is genuinely owner-agnostic before galleries (#99) depend on it, and
  forces the anonymously-readable catalogue-icon case into the design now rather than as a retrofit.
- **Q: Migrate existing media in place, or reseed?** → **A: No backfill and no backward
  compatibility. Losing all existing images is acceptable, in every environment including Prod.**
  Existing inline bytes are dropped at cutover; catalogue icons are re-uploaded by an administrator
  and members re-upload their own avatars. **This knowingly waives the "Existing avatars migrated"
  acceptance criterion on GitHub issue #97** — recorded as drift, not an oversight. The consequence is
  that no data-preserving migration step is written, which removes the riskiest part of the feature.
- **Q: Must a public profile's avatar and badges stay visible to signed-out visitors?** → **A: Yes.**
  Opting a profile public means an anonymous visitor sees that profile's avatar and its badge and
  achievement icons. This is existing feature-026 behaviour and is preserved unchanged; the gate is
  "the platform decides per request", never "authenticated callers only".
- **Q: How do media bytes reach the browser?** → **A: The platform proxies every byte. The media
  store is never publicly reachable, and no direct or time-limited link to it is ever handed to a
  client.** The alternatives — a publicly-readable store, or short-lived signed links — were rejected
  because both move the visibility decision out of the platform. A publicly-readable store with
  derivable object names would let anyone reconstruct a **private** profile's avatar address, and
  would let a banned or newly-private member's media stay fetchable indefinitely; signed links reduce
  that to an expiry window rather than eliminating it, and cannot be revoked without rotating the
  store's credentials. Proxying keeps the gate exactly where it is today — welded to the request —
  and is affordable at this platform's volume. **Trade-off accepted**: media bytes traverse the
  backend, so there is no delivery-network offload. If gallery volume later makes that cost real, the
  remedy is response caching on the platform's own endpoints, decided with measurements rather than
  by opening the store.

- **Q: How are orphaned media objects reclaimed, and is a time bound promised?** → **A: An
  operator-triggered sweep, with no time bound claimed.** Deleting a media object's owner and deleting
  its bytes cannot share a transaction — one is a database row, the other is an object in a separate
  store — so "remove both, atomically" is not achievable by any implementation and was removed as a
  requirement rather than left as an aspiration. Application-code deletions remove the object
  synchronously; database-level cascade deletions cannot, and leave the object unreferenced. Those are
  reclaimed by a sweep an operator runs, **not** by a scheduled job: orphans are rare (nothing
  hard-deletes an owner today) and inert, whereas an unattended process whose job is deleting media is
  itself a hazard if its grace-period logic is wrong. Keeping a human in the loop is worth more than a
  tighter bound on a failure mode that currently cannot occur. **Revisit when a hard-delete or
  right-to-erasure path is added** — at that point the bound starts to matter, and the sweep will have
  proven itself.

## Requirements *(mandatory)*

### Functional Requirements

**Storage seam**

- **FR-001**: The system MUST provide a single, owner-agnostic media storage capability used by every part of the platform that stores binary media, so that profiles, teams, catalogue icons, and future galleries all share one mechanism.
- **FR-002**: The storage capability MUST support, at minimum: store an object, retrieve an object, delete an object, and determine whether an object exists.
- **FR-003**: The storage capability MUST be independent of what owns the media — it MUST NOT contain knowledge of profiles, teams, badges, achievements, or galleries.
- **FR-004**: Callers of the existing picture upload/retrieve operations MUST NOT change: the public addresses, request shapes, and response shapes stay exactly as they are today, and no frontend change is required for this feature.
- **FR-005**: The stored bytes MUST be the already-normalized output of the existing image-processing step; this feature MUST NOT alter, re-encode, or re-validate image content.

**What stays in the primary database**

- **FR-006**: For every stored media object the system MUST keep a lightweight descriptive record in the primary database, carrying at least the owning entity, the object's location in the media store, its content type, and its size — sufficient to authorize and describe a fetch without touching the media store.
- **FR-007**: The primary database MUST NOT store media bytes after this feature ships.
- **FR-008**: Existing list and detail projections MUST continue to avoid loading media bytes; a page that shows many pictures MUST NOT require loading picture content to render its non-picture data.
- **FR-009**: Deleting a media object's owner (or the picture itself) MUST remove the descriptive record **synchronously**. Where the platform's own code performs the deletion, the stored object MUST be removed synchronously as well. Where a deletion is performed by the data store's own cascade rules — which run beneath the application and cannot invoke it — the object is left unreferenced and MUST be reclaimable by the mechanism in FR-030. **No time bound is promised on that reclamation**, deliberately: reclamation is operator-initiated (see Clarifications), so a bound would be a claim the system does not enforce. This is safe because an unreferenced object is *inert* — its location existed only in the deleted record, never left the platform, and the store is not publicly readable — so the residual concern is storage and retention, never access.

**Authorization and privacy (non-negotiable)**

- **FR-010**: Every authorization and visibility rule enforced on picture retrieval today MUST continue to be enforced after the move — specifically that a private profile's picture is not served to an anonymous caller, and that a banned account's media is not served at all.
- **FR-011**: Authorization MUST be decided by the platform before any media bytes are released; the media store MUST NOT be the place where visibility is decided.
- **FR-012**: The media store MUST NOT be readable by the general public by any direct address. All media bytes MUST reach a caller through the platform's own endpoints, after the platform has authorized the request. No route may exist that returns media bytes without that authorization step.
- **FR-013**: The platform MUST NOT hand callers a link that grants direct access to the media store, time-limited or otherwise. A media object's location MUST never be disclosed to a client.
- **FR-014**: Badge and achievement icons MUST remain retrievable by anonymous callers, and a public profile's avatar MUST remain retrievable by anonymous callers, exactly as today — anonymous access is granted by the platform's decision on each request, never by the media store being open.
- **FR-015**: Object locations MUST NOT be derivable from public identifiers, so that a leaked or guessed location is not by itself sufficient to reach media even if the store's access configuration is later changed in error.
- **FR-016**: A change in an owner's visibility or account standing — going private, being banned, being deleted — MUST take effect on the next request, with no window during which previously-issued access continues to work.

**Cutover**

- **FR-017**: All three existing media kinds — member avatars, badge icons, and achievement icons — MUST be served from the media store after this feature ships; none may keep using inline storage.
- **FR-018**: Existing inline media bytes MUST be discarded at cutover. No data-preserving backfill is written, and no backward compatibility with the inline mechanism is retained (owner decision, Clarifications 2026-07-31).
- **FR-019**: The cutover MUST NOT leave a descriptive record pointing at a media object that was never written; owners whose media was discarded MUST end up in the same state as an owner that never had media.
- **FR-020**: After cutover, inline byte storage MUST be absent from the primary database so that the old and new mechanisms cannot diverge, and re-applying the deployment MUST change nothing.
- **FR-021**: An administrator MUST be able to re-upload catalogue icons after cutover through the existing administration surface, with no new tooling required.

**Environments and configuration**

- **FR-022**: The media store MUST be declared once in the infrastructure definition and MUST exist in every environment with the same shape, differing only in sizing and configuration.
- **FR-023**: Local development MUST work end to end against a local stand-in for the media store, requiring no cloud account and no cloud credentials.
- **FR-024**: Media-store credentials and connection settings MUST be supplied as environment-level configuration — a local environment file locally, and deployment-environment configuration when deployed — and MUST NOT be committed to the repository or exposed to the client.
- **FR-025**: Each environment MUST address a media location distinct from every other environment's, so no environment can read or overwrite another's objects.
- **FR-026**: The media store MUST be configured to refuse general-public read access in every environment, and that configuration MUST come from the shared infrastructure definition rather than being set per environment by hand (enforcing FR-012).

**Resilience and failure behavior**

- **FR-027**: Every call to the media store MUST have a bounded time limit and MUST NOT be able to hang a request indefinitely.
- **FR-028**: Transient media-store failures MUST be retried with growing, jittered delays and a stop condition; non-transient rejections (not found, not permitted, invalid) MUST fail immediately without retry.
- **FR-029**: A media-store outage MUST NOT prevent pages from rendering; missing pictures MUST degrade to the existing placeholder behavior.
- **FR-030**: An upload MUST NOT leave the system in a state where the descriptive record and the stored object disagree without a means of reconciling them; orphaned objects MUST be reclaimable.
- **FR-031**: Media-store failures MUST surface to the client as generic, non-technical messages; provider errors, credentials, and object locations MUST NOT leak to the client.
- **FR-032**: Media-store failures, retries, and outages MUST be observable to operators without recording credentials or media content.
- **FR-033**: Because every media byte now flows through the platform's own endpoints (FR-012), media retrieval MUST NOT be able to exhaust the platform's request capacity — a caller repeatedly requesting media MUST NOT degrade availability for other requests.

### Key Entities *(include if feature involves data)*

- **Media object**: an individual stored binary file (today: an avatar, a badge icon, an achievement icon; next: a gallery image). Lives in the media store, not in the primary database. Identified by a location that is meaningful only to the platform.
- **Media descriptor**: the lightweight primary-database record that points at a media object — which entity owns it, where the object lives, its content type, and its size. This is what authorization, visibility filtering, and "does this member have a picture?" checks read; it never carries bytes.
- **Media kind**: the category of a media object (member avatar, badge icon, achievement icon, and later gallery image), which determines how it is addressed and which visibility rule applies to it — notably that catalogue icons are anonymously readable while member avatars are gated.
- **Media container**: the environment-scoped destination in the media store into which objects are written; one shape everywhere, one distinct destination per environment.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: After cutover, a newly uploaded picture of every covered kind (member avatar, badge icon, achievement icon) is retrievable through its unchanged public address, and no owner is left displaying a broken image.
- **SC-002**: Zero image bytes remain in the primary database — verified by inspecting the stored data after cutover across all three covered media kinds.
- **SC-003**: A private member's picture is not retrievable by any anonymous route — verified against both the platform's own address and the media store directly — while a **public** member's picture and all catalogue icons remain retrievable anonymously, and a banned account's picture is not retrievable at all.
- **SC-010**: Direct requests to the media store from outside the platform are refused in every environment, verified per environment rather than assumed from configuration.
- **SC-004**: Picture display is no slower than before from a member's point of view; a page showing many pictures loads within the same time budget it meets today.
- **SC-005**: A developer with no cloud credentials can start the stack, upload a picture, and see it displayed, in a single documented setup step.
- **SC-006**: With the media store made unreachable, every page that displays pictures still renders, and an attempted upload returns a clear failure without altering the member's existing picture — verified by test.
- **SC-007**: Re-applying the deployment to an already-cut-over environment changes nothing and reports no errors, and no descriptive record anywhere points at a media object that does not exist.
- **SC-008**: Adding a new kind of stored media (e.g. team avatars or gallery images) requires no change to the storage capability itself — demonstrated by the follow-up gallery feature consuming it unchanged.
- **SC-009**: No credential or connection secret for the media store appears anywhere in the repository.

## Assumptions

- **The image-processing step is already in place and unchanged.** Uploaded images are validated, guarded, stripped of metadata, resized, and re-encoded before storage by the previously delivered processing pipeline. This feature receives already-normalized bytes and only decides where they live.
- **No stored media is worth preserving.** The owner has confirmed that all existing images may be lost in every environment, including Prod. This assumption is what removes the backfill entirely; if it stops holding before this ships, FR-016 must be revisited before deployment.
- **Volume today and tomorrow is small.** A modest number of avatars and catalogue icons, and later a bounded five images per profile and per team — not a media platform. Throughput-oriented delivery optimisations are not assumed.
- **Team avatars do not exist yet.** The storage capability is designed to serve them, but adding team avatars is not part of this feature.
- **Galleries are a separate feature.** This feature delivers the plumbing galleries will consume; gallery entities, limits, upload flows, and UI are out of scope.
- **Caching and delivery optimization are out of scope beyond parity.** Because the platform now proxies every media byte, response caching on the picture endpoints is the designated remedy if volume ever makes proxying expensive — but it is not required by this feature unless needed to meet the "no slower than today" criterion. Opening the media store is explicitly *not* an available remedy.
- **Cost and lifecycle policy are not tuned here.** Redundancy tier, retention, and lifecycle rules are per-environment configuration values, not behavior this feature defines.
- **Placeholder behavior already exists.** The frontend already renders a fallback when a picture is absent, so "degrade to placeholder" needs no new UI.

## Dependencies & Known Drift

- **Depends on**: the delivered image-processing pipeline (feature 034 / issue #98) — this feature stores its output and must not alter it.
- **Enables**: showcase galleries (issue #99), which are the reason the move is worth doing now, and future team avatars.
- **Carries forward**: the storage half of issue #13, and the explicit "when scale warrants, move bytes to object storage" migration path recorded in the profile feature's research notes.
- **Preserves**: the feature-026 visibility decision (private profiles are not served anonymously) and the platform-wide banned-account filter. Any weakening of either is a regression, not a trade-off.
- **New external dependency in the request path**: the platform gains a runtime dependency on an external storage service for picture display. This is the first such dependency on a member-visible read path and brings the resilience principle into scope for reads as well as writes. The proxy decision (Clarifications) makes that dependency *synchronous within* the platform's own request handling, which is what FR-027–FR-033 exist to bound.
- **Infrastructure change**: adds a resource to the single infrastructure definition applied to every environment, and a local stand-in service to the local stack.
- **Schema change**: removes inline byte storage and introduces descriptor fields. Because preservation is waived, this is a **schema-only** change — no data-moving step.
- **Deferred by design — bounded orphan reclamation**: the sweep is operator-initiated and promises no time bound (Clarifications). The trigger to revisit is the addition of a **hard-delete or right-to-erasure path**, which does not exist today (bans are soft-delete). At that point reclamation becomes a retention obligation rather than housekeeping, and a scheduled sweep — single-execution across replicas via the Redis already in the stack — is the intended upgrade.
- **Recorded drift from GitHub issue #97**: the issue's acceptance criterion *"Existing avatars migrated"* is **deliberately not met**. The owner accepted total loss of existing media in every environment (Clarifications 2026-07-31), trading data preservation for a materially smaller and lower-risk change. The issue's other three acceptance criteria are unaffected, and *"ban/visibility gating preserved"* is strengthened rather than waived.
