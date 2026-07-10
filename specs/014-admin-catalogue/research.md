# Phase 0 Research: Admin catalogue management

The spec had a single clarification (team-assign mounting), already resolved in
`/speckit-clarify`. The remaining decisions below are technical approach choices;
none require new infrastructure and none change the 012 public contract.

## D1 — Reinstate (un-retire) shape

**Decision**: Add `POST /api/v1/admin/badges/{id}/reinstate` (and the achievements
twin) that sets `IsRetired = false`; mirror `RetireDefinitionAsync` as
`ReinstateDefinitionAsync` returning `bool` (false ⇒ 404).

**Rationale**: Symmetric with the existing `DELETE {id}` retire; keeps the
`…UpsertRequest` (name/description/applies-to) free of lifecycle flags, so edit
never accidentally toggles retirement. One tracked mutation; audit interceptor
sets `ModifiedDate`. No schema change.

**Alternatives**: (a) add `isRetired` to the upsert request — rejected: conflates
edit with lifecycle and lets a stale form silently un-retire; (b) make edit
un-retire implicitly — rejected: you couldn't edit a retired type without
reactivating it.

## D2 — Grant count

**Decision**: `GrantedCount` = number of `Status == Active` awards referencing the
definition, produced as a projected correlated COUNT inside the existing
`ListDefinitionsAsync` `.Select(...)`. No denormalized column.

**Rationale**: Always consistent (no drift on grant/revoke), no migration, cheap
at catalogue scale (tens of types), indexed by `BadgeDefinitionId` /
`AchievementDefinitionId`. Revoked awards are excluded per spec assumption.

**Alternatives**: a stored counter maintained on grant/revoke — rejected: adds a
migration and a consistency burden for no measurable benefit here.

## D3 — Created date

**Decision**: Surface `CreatedAt` from the existing `BaseEntity.CreatedDate`
(populated by `AuditFieldsInterceptor`).

**Rationale**: Already captured for every definition; zero new data or migration.

## D4 — Remove icon

**Decision**: Add `DELETE /api/v1/admin/badges/{id}/icon` (+ achievements) →
`RemoveIconAsync` deleting the `BadgeIcon` / `AchievementIcon` row; 204 on success,
404 if the definition doesn't exist (idempotent if already absent).

**Rationale**: `HasIcon` is derived from row existence, so deleting the row flips
it to false and the public icon endpoint 404s → the frontend already renders the
placeholder fallback. Consistent with the raw-body `PUT {id}/icon` replace path.

## D5 — Icon upload transport & preview

**Decision**: Keep the raw-body `PUT {id}/icon` (`ReadBodyBytesAsync` →
`SetIconAsync`); the client sends the chosen `File`/`Blob` directly and the server
sniffs the type (`ImageValidation`, PNG/JPEG/WebP) and enforces
`RecognitionOptions.MaxIconBytes` (~512 KB). Client shows a local `URL.createObjectURL`
preview at 32/40/56 px with a shape mask (circle for badges, rounded square for
achievements) before committing.

**Rationale**: Reuses the exact 012 endpoint contract (not the multipart avatar
path); server remains the validation boundary. Preview is client-only and needs no
image processing — `object-fit: cover` inside the masked box matches how icons
render elsewhere. Interactive cropping is out of scope (spec).

## D6 — Admin teams area

**Decision**: New `AdminTeamsController` with `GET /admin/teams?q=&skip=&take=`
(paged `AdminTeamListItemDto`) and `GET /admin/teams/{slug}` (`AdminTeamDetailDto`),
backed by `IAdminTeamService`, mirroring `AdminUsersController`/`IAdminUserService`.
The team detail reuses the **existing** `GET /admin/teams/{slug}/awards` read and
the existing `teamSlug` grant/revoke on the badge/achievement admin controllers.

**Rationale**: Consistency with the users area (search → detail → Assign picker) is
the clarified choice. Team search filters `Team.Name`/`Team.City` (both already on
the entity); `Slug` is the immutable address used in routes and award calls.

**Alternatives**: reached-by-slug-only or inline-on-public-page — rejected in
clarify for discoverability/separation reasons.

## D7 — Reusable Assign picker

**Decision**: Extract the player Assign picker currently inlined in
`admin-user-detail.component` into a standalone `AssignPickerComponent`
(`features/admin/shared`) parameterized by subject type (`player` | `team`) and
subject ref, emitting on grant so the host reloads awards. Player detail is
refactored to consume it (behavior-preserving); team detail consumes the same.

**Rationale**: Avoids two divergent copies of grant logic (badges/achievements
tabs, held-marking, note, achievement context). Existing e2e (`recognition.spec`,
`admin-area.spec`) plus new tests guard the refactor. Uses the same
`RecognitionAdminService.grantBadge/grantAchievement/subjectAwards` which already
accept `teamSlug`.

**Alternatives**: duplicate the picker into team detail — rejected: maintenance
drift across two subjects.

## D8 — Kind immutability on edit

**Decision**: On create, the Kind toggle selects which catalogue/endpoint is used
(`admin/badges` vs `admin/achievements`); on edit the Kind is displayed but locked.

**Rationale**: There is no cross-catalogue move in the API by design; a type is a
badge or an achievement for its lifetime. Locking Kind on edit matches the data
model and prevents an impossible request.
