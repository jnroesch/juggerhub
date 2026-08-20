# Phase 0 Research — Showcase Image Galleries (046 / #99)

Every decision below was taken by reading the code named in it, not from the issue text. Where the
issue and the code disagree, the code wins (CLAUDE.md source-of-truth order).

---

## R1 — Two descriptor tables, not one polymorphic media table

**Decision**: add **two** entities — `ProfileShowcaseImage` (owner: `PlayerProfile`) and
`TeamShowcaseImage` (owner: `Team`) — each with its own table, mirroring `ProfileAvatar`.

**Rationale**: `AppDbContext.cs:204-217` carries an explicit instruction not to merge media
descriptor tables:

> *"this filter is the reason avatars, badge icons and achievement icons kept three separate
> descriptor tables instead of being merged into one polymorphic media table. A polymorphic row has
> no single owner navigation, so this expression could not exist and the ban gate would have to be
> re-checked by hand at every call site … Do not 'simplify' these three tables into one."*

The showcase needs exactly that expression: `HasQueryFilter(g => g.Profile.User.Status != Banned)`
makes FR-019 structural. A shared `Media` table with a nullable `ProfileId`/`TeamId` could not carry
it, and the two owners want *different* rules anyway (see R3).

**Alternatives considered**: one `Media` table with an owner discriminator (issue's `ProfileMedia` /
`TeamMedia` sketch) — rejected as above; extending `ProfileAvatar` with a `Slot` column — rejected,
it would put the identity picture and the showcase in one collection where every avatar read would
have to filter, and FR-004 (the two never affect each other) would become a convention.

**Naming**: `ProfileShowcaseImage` / `TeamShowcaseImage`, not `ProfileMedia` / `TeamMedia`. The
avatar *is* profile media too; a name that does not distinguish them invites exactly the confusion
FR-004 forbids.

---

## R2 — Cap enforcement: pessimistic lock on the owner row, inside the execution strategy

**Decision**: an add runs `SELECT 1 FROM "PlayerProfiles"|"Teams" WHERE "Id" = {ownerId} FOR UPDATE`
inside a transaction, then counts, then inserts at `Position = count`. The whole transaction runs
through `db.Database.CreateExecutionStrategy()` with `ChangeTracker.Clear()` first.

**Rationale**: this is not invented — `TeamService.MutateMembershipAsync`
(`backend/Services/Teams/TeamService.cs:344-393`) already does exactly this to keep the last-admin
guard atomic, including the `ChangeTracker.Clear()` before `BeginTransactionAsync` and the comment
explaining why a replay must start clean. Copying an in-repo pattern beats introducing a second
concurrency idiom. It also delivers SC-002 *literally*: ten simultaneous adds against an empty
gallery serialize on the owner row, so exactly five are admitted and five are refused — the
alternatives below cannot promise that.

Constitution Principle VII requires multi-step transactions to run through the execution strategy
with **all** state mutation inside the retried delegate. The blob write deliberately stays *outside*
it (R5) — it is not database state and must not be replayed.

**Alternatives considered**:

- **Unique index on `(OwnerId, Position)`** — the classic cap trick, rejected twice over: ten
  concurrent adds would all pick position 0 and *one* would win, leaving 1 stored and 9 refused
  (fails SC-002's "exactly 5"); and it makes reorder require either a deferrable constraint or a
  two-phase negative-offset shuffle, since EF issues one `UPDATE` per row and Postgres checks a
  non-deferrable unique constraint per statement.
- **Check the count without a lock** — a plain read-then-insert is a TOCTOU race; two adds against a
  four-image gallery both see 4 and both insert. Client-side "gallery full" hiding is UX only
  (Principle I).
- **Serializable isolation** — heavier, and turns a routine conflict into a retry loop the platform
  has nowhere else.

---

## R3 — Ban filter on the profile gallery, none on the team gallery

**Decision**: `ProfileShowcaseImage` gets `HasQueryFilter(g => g.Profile.User.Status != Banned)`,
matching `ProfileAvatar`. `TeamShowcaseImage` gets **no** query filter.

**Rationale**: the profile gallery belongs to a person, so it inherits that person's standing
(FR-019). A team's gallery belongs to the team; hiding it because the member who uploaded a picture
was later banned would punish the team for someone else's conduct, and there is no
"uploaded-by" navigation to filter on anyway — see R4.

**Consequence recorded**: reconciliation and account deletion must both use `IgnoreQueryFilters()`
when reading `ProfileShowcaseImage.ObjectKey`, exactly as they already do for `ProfileAvatars` — see
R6 and R7.

---

## R4 — No `UploadedByUserId` on a team showcase image

**Decision**: `TeamShowcaseImage` stores no uploader reference.

**Rationale**: nothing in the spec needs it (no per-uploader permission, no attribution shown, no
moderation surface — see the spec's Out of Scope), and storing it would create a `UserId`-keyed
column that account deletion (037) would have to neutralise, plus a FK decision, for data nobody
reads. `CreatedDate` from `BaseEntity` already answers "when".

**Alternative considered**: keep it "for audit" — rejected; feature 041's plan records the same
hazard in reverse (a `UserId`-keyed row that survives erasure pointing at a row identifying nobody).
Do not add the column unless a later feature actually reads it.

---

## R5 — Write ordering: process → put object → transaction → (on refusal) reclaim

**Decision**: for an add, in order:

1. `IImageProcessor.Process(bytes, options.Showcase)` — reject before anything is stored.
2. Mint `MediaObjectKey.Create(MediaKind.ProfileShowcase | TeamShowcase)`.
3. `IMediaStore.PutAsync` the normalized WebP.
4. Execution strategy → transaction → `FOR UPDATE` owner → count → refuse if 5 → insert descriptor →
   commit.
5. If the transaction refused (cap reached) or threw, best-effort `DeleteAsync` the object just
   written, logged on failure and left to the sweep.

**Rationale**: `ProfileService.SetAvatarAsync` (lines 291-355) states the rule this follows — *"mint
the key, write the object, commit the descriptor, and only then delete the object this one
replaces… a row and a blob cannot share a transaction, so some failure window is unavoidable — this
ordering picks the harmless one."* An add has no superseded object, so the mirror-image cleanup is
the refusal path: a refused add must not leave a permanent orphan (FR-015 is about the *gallery*
being unchanged, which holds either way, but leaving litter for every hit of the cap is sloppy when
the key is right there).

**Alternative considered**: check the cap *before* writing the object, to avoid the reclaim
entirely — rejected as the *primary* control (that is the TOCTOU race of R2), but kept as a cheap
pre-check that avoids the blob write in the overwhelmingly common non-concurrent case. Both: read
the count first and refuse early; the locked re-count inside the transaction is the guarantee.

---

## R6 — ⚠ The reconciliation sweep must learn the two new tables, or it deletes live galleries

**Decision**: `MediaReconciliationService.SweepAsync` gains two more `referenced.Add` loops, over
`ProfileShowcaseImages` and `TeamShowcaseImages`, both with `IgnoreQueryFilters()`.

**Rationale**: this is the single highest-consequence integration point in the feature. The sweep
(`backend/Services/Media/MediaReconciliationService.cs:57-100`) builds a set of referenced keys from
the three descriptor tables it knows about and **deletes every object not in that set** that is older
than the grace period. Ship the galleries without touching it and the next operator-triggered sweep
irreversibly deletes every showcase image in the environment. `IgnoreQueryFilters()` matters for the
same reason the file already documents: without it a banned member's rows are invisible, the sweep
sees their objects as unreferenced, and it deletes media belonging to a suspended-not-gone account.

**Test**: an integration test that stores a showcase image, runs a sweep with a zero grace period,
and asserts the object survives. Feature 035's `MediaReconciliationTests` is the place.

---

## R7 — Deletion paths: two cascades that run with no application code

**Decision**: both owner-deletion paths harvest object keys **before** the delete and reclaim the
objects **after** it commits.

- **Account deletion** (`AccountDeletionService`): `EraseOwnedDataAsync` ends with
  `PlayerProfiles.ExecuteDeleteAsync`, and the file notes the avatar descriptor "cascades" with it.
  The gallery rows will cascade the same way. Extend the existing pre-read at line 134 to collect
  the gallery keys too, and extend `ReclaimAvatarObjectAsync` into a list-taking reclaim called
  after commit (its existing contract — never fail the request, log at error, leave to the sweep —
  is already right).
- **Team deletion** (`TeamService.DeleteAsync`): `Teams.ExecuteDeleteAsync` relies on
  `ON DELETE CASCADE`, so no application code runs for the gallery rows at all. Harvest the keys
  before the delete (next to the existing "archive the chat BEFORE the team goes" step, which
  establishes that this method already does ordered pre-work) and delete the objects after.

**Rationale**: `ProfileAvatar`'s own XML doc states the rule — *"Deleting this row does not delete
the object… Application code that deletes media must delete the object explicitly."* Without this,
FR-012/SC-010 hold only as far as *reachability* (the container is private and the keys are gone
with the rows), and the bytes linger until an operator triggers a sweep. That is not what "removed
with it" should mean for an erasure request.

**Note**: the sweep is **operator-triggered, not scheduled** (035 clarification), so "the sweep will
get it" is not a schedule — it is "someone, eventually". This is the reason the explicit reclaim is
required rather than nice-to-have.

---

## R8 — A new showcase processing profile, not the avatar one

**Decision**: add `ImageProcessingOptions.Showcase` = `{ ResizeMode = Fit, MaxDimension = 1280,
Quality = 80, MaxOutputBytes = 1 MB }`.

**Rationale**: the mechanism is already built for this — `ImageProcessingProfile`'s own doc says
*"Named profiles let avatars and the future gallery (#99) share one processor with different limits
(spec Clarifications: avatar = square-crop, gallery = fit)"*. Feature 034 anticipated this exact
profile; adding it is completing a designed extension point, not widening one. `Fit` is what keeps a
team photo from being cropped to a square (owner decision, spec Clarifications). Input acceptance
(`MaxInputBytes` 8 MB, `MaxDecodePixels` 40 MP, `AllowedContentTypes`) is global and unchanged.

**Alternatives considered**: reuse `Avatar` (512 px square-crop) — rejected by the owner; a
1600 px/1.5 MB profile — rejected by the owner as too heavy for phone viewing.

---

## R9 — `MediaKind` gains two members; keys stay UUIDv4

**Decision**: `MediaKind.ProfileShowcase` → prefix `profile-showcase`, `MediaKind.TeamShowcase` →
prefix `team-showcase`.

**Rationale**: `MediaObjectKey` already documents that prefixes exist "for operator legibility and
lifecycle rules only — **not** a security boundary", and that keys are UUIDv4 **deliberately**, with
a paragraph asking implementers not to "correct" it to UUIDv7. Two prefixes rather than one shared
`showcase` prefix so an operator can tell the two owners apart in a container listing, and so a
future lifecycle rule can address them separately.

---

## R10 — Endpoints: per-owner subresources, bytes served through the owner's gate

**Decision**:

| Method | Path | Auth |
|--------|------|------|
| `GET` | `/api/v1/profiles/{handle}/showcase` | `[AllowAnonymous]` + visibility gate |
| `GET` | `/api/v1/profiles/{handle}/showcase/{imageId}/image` | `[AllowAnonymous]` + gate + `MediaRead` |
| `POST` | `/api/v1/profiles/me/showcase` | owner |
| `PATCH` | `/api/v1/profiles/me/showcase/{imageId}` | owner (caption) |
| `DELETE` | `/api/v1/profiles/me/showcase/{imageId}` | owner |
| `PUT` | `/api/v1/profiles/me/showcase/order` | owner |
| `GET` | `/api/v1/teams/{slug}/showcase` | signed-in |
| `GET` | `/api/v1/teams/{slug}/showcase/{imageId}/image` | signed-in + `MediaRead` |
| `POST` `PATCH` `DELETE` `PUT …/order` | `/api/v1/teams/{slug}/showcase…` | team admin |

**Rationale**: the byte endpoint hangs off the owner because the owner is what the authorization rule
is expressed in — the same reason `GET /profiles/{handle}/avatar` exists rather than a global
`/media/{id}`. A global media endpoint would have to re-derive the owner to gate it, which is the
"gate delegated to storage" failure `IMediaStore`'s doc comment forbids. `me` for the write side
matches `PUT /profiles/me/avatar`; the team write side is guarded by `TeamMembershipGuard` exactly
like every other team mutation.

Every refusal is `404` (never `403`), matching `GetAvatar`'s comment — *"not found, not permitted,
and store-unavailable are deliberately indistinguishable, so the endpoint never becomes an existence
oracle"* — which is FR-023.

**Anonymous surface**: the two profile `GET`s are new `[AllowAnonymous]` endpoints under the 026
`FallbackPolicy`. `Security/AnonymousAllowlistTests.cs` asserts what stays anonymous; the profile
showcase reads join the same category as `GET /profiles/{handle}` and `…/avatar` and need a test
there. The team endpoints add nothing anonymous — `TeamsController` is `[Authorize]` at class level.

---

## R11 — Reorder contract: the full ordered id list, applied all-or-nothing

**Decision**: `PUT …/order` takes `{ "imageIds": [ "…", "…" ] }` — the complete list — and is
rejected unless it is an exact permutation of the owner's current image ids (same length, no
duplicates, no strangers). Applied inside the same locked transaction as R2.

**Rationale**: FR-010. A delta ("move id X to index 2") cannot detect that the client's view is
stale, which is the reorder-races-a-delete edge case; a permutation check detects it for free and
turns it into one clean refusal the client answers by reloading. With at most five ids the payload
is trivial.

---

## R12 — Rate limiting: a new `MediaUpload` policy; reads reuse `MediaRead`

**Decision**: reads use the existing `RateLimitPolicies.MediaRead` (300/min, partitioned by caller
including an IP fallback for anonymous). Add `MediaUpload` at **20/min**, partitioned by user
(`PartitionByUser`, since every upload path requires a session).

**Rationale**: `MediaRead` was built for exactly this — its comment already reasons about "one per
displayed member" pages, and a five-image gallery is well inside 300/min. Uploads are the expensive
direction (decode + resize + encode + blob write) and have no existing policy; 20/min is far above a
person filling a five-slot gallery twice over and far below a script. `PartitionByUser` is correct
because there is no anonymous upload path (contrast `MediaRead`, which needed the IP fallback).

**Principle VII note**: this feature adds **no new outbound integration**. The media store's
resilience pipeline (`Resilience:Outbound:MediaStore`) already covers every blob call; adding a
retry, a `Task.Delay`, or a second breaker anywhere in this diff is review-rejectable. The frontend
must **not** auto-retry the upload — it is a browser-hop mutation.

---

## R13 — Frontend: one read component, one manage component, no new dependency

**Decision**: `frontend/apps/web/src/app/shared/showcase/`

- `showcase-gallery` — thumbnails + enlarged view. Used on the public profile, the owner profile,
  and the team page.
- `showcase-manager` — add / reorder / caption / remove. Used on the owner profile and by team
  admins only.

**Rationale**: the read component has three call sites, which is what earns a shared component (the
same bar `shared/address-fields/` cleared in 042). Splitting read from manage keeps the viewer
bundle free of upload code and makes "a non-admin is offered nothing" (US2 scenario 3) structural
rather than a set of `@if`s inside one component.

**Reorder interaction**: **move-up / move-down buttons**, not drag-and-drop. `@angular/cdk` is not a
dependency of this repo (checked `frontend/package.json`) and the plan adds none; beyond that,
buttons are keyboard- and touch-accessible by construction, where drag-and-drop needs a parallel
keyboard affordance anyway to satisfy SC-007.

**Enlarged view**: no shared dialog component exists (`shared/ui/` holds alert, button, card,
empty-state, icon, legal-links, loading, lowercase-input, page). `team-detail.component.html:250-252`
has the established modal markup — `fixed inset-0 z-50 … bg-black/40` with
`role="dialog" aria-modal="true"` — copy that shape rather than adding a dialog library.

**Images in `<img src>`**: works unchanged. Auth is a JWT in an httpOnly cookie, so the browser sends
it with the image request; `ProfileService.avatarUrl()` already relies on this. No blob-URL fetching,
no `Authorization` header plumbing.

**Angular idiom**: signals + `computed`, `.html`/`.css`/`.ts` kept separate (Principle VI), services
returning Observables from `HttpClient` like every existing feature service.

---

## R14 — i18n: one shared `showcase.*` block

**Decision**: new keys live under a single top-level `showcase.*` node in
`frontend/apps/web/public/i18n/{en,de,es}.json`, with only the two surface-specific headings under
`profile.*` and `teams.*`.

**Rationale**: the strings ("Add a picture", "Gallery full", the failure reasons, the lightbox
controls) are identical on both surfaces; duplicating them under `profile.` and `teams.` would be
~20 keys × 2 surfaces × 3 languages of pure duplication, and would let the two surfaces drift.
Catalogues are at parity today (en 1308 / de 1310 / es 1310, the two extra being the deliberate
`_meta.*` entries), and feature 042 added a key-parity guard, so a key added to `en.json` alone turns
the suite red — by design.

---

## R15 — Pagination: a bare capped list, as feature 044 did

**Decision**: `GET …/showcase` returns `IReadOnlyList<ShowcaseImageDto>` — not `PagedResult<T>`.

**Rationale**: this is a deliberate, recorded deviation from Principle III's "pagination is
mandatory", identical in shape to the one feature 044 recorded and to the `Roster` (48) and
`RecentActivity` (6) precedents on the same team page. The collection is hard-capped at 5 by the
feature's central requirement; a `totalCount` and `skip`/`take` on a five-element list advertise a
paging affordance that cannot exist. Carried into Complexity Tracking in `plan.md`.

---

## R16 — Spec drift found while planning

- **SC-002 is met literally** under R2's per-owner lock (exactly 5 stored, 5 refused). Recorded here
  because it *would not* have been met under the unique-index alternative — if implementation ever
  drops the lock, SC-002 has to be renegotiated, not quietly reinterpreted.
- **FR-011's "leaving no unreferenced stored object behind on the ordinary path"** is satisfied by
  the explicit reclaims in R5/R7. The two extraordinary paths (a crash between blob write and
  commit; a Postgres cascade) remain the sweep's job, exactly as feature 035 designed.
- **Nothing in this feature needs a `beforeunload`, a `canDeactivate`, or draft persistence** — an
  upload either completes or does not; there is no multi-step form to lose.
