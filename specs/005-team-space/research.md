# Phase 0 — Research & Decisions: Team Space & Member Handling

Resolves the design questions the spec + clarifications left for planning, and records the decisions that shape Phase 1. Everything below stays inside the constitution (thin controllers, service-centric, EF Core directly, `BaseEntity`/UUIDv7, Mapster DTOs, mandatory pagination, server-side authz, env parity, `.ps1`-only).

## §1 — Team identity: slug (like the profile handle) + non-unique names

**Decision**: A team's identity is a **creator-chosen, unique, immutable slug** addressing it at `/t/<slug>` and inside invite links `join/<slug>/<token>`. Format mirrors the profile handle exactly: `^[a-z0-9]+(?:-[a-z0-9]+)*$`, length **3–30**, normalized (trim, lowercase) before validation, rejected if it matches a **team reserved-word set**. Uniqueness is guaranteed by a **unique index** on `Team.Slug` (race-safe) plus a pre-check for a friendly message and a live availability endpoint. **Display names are free text (2–50), NOT unique**, and are not part of identity.

**Reuse**: Introduce `Services/Teams/TeamSlugPolicy.cs` mirroring [`HandlePolicy`](../../backend/Services/Profile/HandlePolicy.cs) (same regex/length/normalize, a `SlugRejection` enum + `Describe`) with a **team-specific reserved set**: `t, teams, team, new, join, invitations, invite, settings, members, api, admin, u, null, undefined, me`. Team slugs and profile handles occupy **separate URL namespaces** (`/t/…` vs `/u/…`), so a team slug MAY equal a user handle — uniqueness is only within teams. (A future refactor could hoist a shared `SlugPolicy` core; kept separate now to avoid touching profile code.)

**Immutability**: no update path exists — `Slug` is set once at creation (init-only setter, no service/endpoint mutates it), asserted by a test. **Rename of the display name is deferred** (no endpoint this iteration), but the model does not make the name immutable, so a later rename never breaks a link (the slug is fixed).

**Rationale**: matches the proven handle model (SC-009), keeps `/t/<slug>` clean and case-unambiguous, sidesteps confusables, and lets two cities field a same-named team. **Alternatives**: auto-derived slug from name (rejected by the user — they chose creator-chosen parity); globally-unique names (rejected — cross-city land-grab, blocks legitimate teams, forbids rename).

## §2 — Membership model & the actor key

**Decision**: `TeamMembership : BaseEntity` links a **`Team`** to a **`User`** (the account/actor) with a `Role` (`TeamRole` enum: `Admin`/`Member`) and `JoinedDate`. Unique index on **(`TeamId`, `UserId`)** — one membership per user per team. A user MAY hold unlimited memberships across teams and types (FR-018), so there is **no** global/per-type cap. Roster **display** data (name, handle, pompfen, avatar) is projected via `User.Profile` (each account has exactly one `PlayerProfile`).

**Rationale**: the JWT subject, invitation targets, and "who is admin" are all **account** concepts, so keying membership on `UserId` makes every authz check a direct `(TeamId, UserId)` lookup, while the 1:1 `User↔PlayerProfile` nav supplies display fields in the same projection. **Alternative**: key on `ProfileId` (rejected — forces a `userId→profileId` hop on every authz check; the actor is the account, not the profile).

**EF insert (gotcha)**: create the team and the creator's admin membership with explicit `DbSet.Add` calls inside one transaction — **not** via a populated navigation collection — because client-set UUIDv7 keys on nav-inserts can be misclassified by the change tracker (project EF gotcha). `db.Teams.Add(team); db.TeamMemberships.Add(new TeamMembership { TeamId = team.Id, UserId = creator, Role = Admin });`

## §3 — Roles & the last-admin guard (atomic, server-side)

**Decision**: Admins promote/demote (`Member↔Admin`), remove members, and step down; any operation that would drop a team to **zero admins** is blocked server-side. Promotion/demotion/removal/step-down/leave all funnel through service methods that, **inside a single transaction**, (a) load the target membership, (b) if the change removes/demotes an admin, **count remaining admins with a row-locking read** (`FROM … FOR UPDATE` via a tracked query in a `Serializable`/`RepeatableRead` transaction) and reject when the resulting admin count would be `< 1`, then (c) apply the change. The guard returns a typed result (`LastAdmin`) the controller maps to **409 Conflict** with a friendly reason.

**Rationale**: the guard is a correctness invariant (SC-005) and must hold under the concurrent "two admins demote each other" race (spec edge case), so it cannot be a read-then-write without isolation. A short transaction with an admin-count check under `RepeatableRead`/`Serializable` (Npgsql supports both) is the simplest correct mechanism; retries on serialization failure are acceptable and rare. **Alternative**: a DB `CHECK`/trigger enforcing ≥1 admin (rejected — cross-row invariants are awkward as constraints; the transactional check is clearer and testable).

## §4 — Invitations: one shared link + targeted invites

**Decision**: `TeamInvitation : BaseEntity` with `Kind` (`InvitationKind`: `Link`/`Targeted`), `Token` (opaque, high-entropy, URL-safe), `TeamId`, `CreatedByUserId`, `TargetUserId` (targeted only), `ExpiresDate` (issued + **7 days**), and `Status` (`InvitationStatus`: `Pending`/`Accepted`/`Declined`/`Revoked`). Effective state is derived: an invite is *usable* iff `Status == Pending && ExpiresDate > now`; "expired" is computed, not a stored status (a background sweeper is unnecessary).

- **Shared link**: at most **one active** `Link` invitation per team (partial unique index on `TeamId WHERE Kind = Link AND Status = Pending`). It is **reusable by distinct users** — accepting it creates a membership but does **not** consume the link (stays `Pending` until expiry/revoke). "Regenerate" revokes the current link and issues a new one; "revoke" sets `Status = Revoked`, immediately invalidating the URL.
- **Targeted**: bound to one `TargetUserId`, delivered by email (§5), **consumed** on accept (`Status = Accepted`) or decline (`Status = Declined`). At most one `Pending` targeted invite per (`TeamId`, `TargetUserId`) — a duplicate request is a no-op that surfaces the user as already "invited".

**Token storage**: tokens are stored **raw** (not hashed) because the active link MUST be **re-displayable** to admins ("copy link" on the Invitations screen). Risk is bounded: a token is a low-privilege capability (join **one** team as a **member**), unguessable (≥128-bit, `base64url`), **expires in 7 days**, and is **revocable**. This matches how org/workspace invite links work (GitHub/Slack). **Alternative**: hash at rest (rejected for `Link` — cannot re-display the copyable URL; could hash `Targeted` tokens since they're emailed and never re-shown, but uniform raw storage is simpler and the risk profile is the same).

**Rationale**: a single reusable link matches the wireframe (one "Invite Link · expires in N days · Revoke"); targeted invites are the searchable backup. Deriving "expired" avoids a scheduler and keeps parity simple.

## §5 — Targeted-invite delivery by email (reuse 002 infra)

**Decision**: Targeted invites are delivered by transactional email, reusing [`IEmailSender`](../../backend/Services/Email/IEmailSender.cs) + `IEmailTemplateService` exactly like [`AuthEmailService`](../../backend/Services/Email/AuthEmailService.cs). Add a `TeamEmailService` that renders a **new `team-invite` HTML template** (extending the existing base header/footer) and sends it; the link is built from `EmailOptions.FrontendBaseUrl` as `"{base}/join/{slug}/{token}"`. Mailpit locally, Resend on Dev/Prod (no new infra/secrets). The in-app notifications alternative is **out of scope** (GitHub issue #14).

**Rationale**: parity-first — the whole email pipeline already exists; this is one service + one template. **Alternative**: bespoke mailer (rejected — duplicates infra).

## §6 — Accept / decline flow & routing

**Decision**: The invitee acts on a **token**, not a team id. `GET /api/v1/invitations/{token}` is **anonymous** and returns an `InvitePreviewDto` = **public** team info (name, type, city, member count) + inviter display name + a `state` (`usable`/`expired`/`invalid`). Accept/decline are **authenticated**: `POST /api/v1/invitations/{token}/accept` and `/decline`. Accept adds the caller as a `Member` (idempotent if already a member → returns joined), rejects expired/revoked/used with **409**. An unauthenticated visitor opening a link is sent to sign-in/register and **returned to the same `/join/:slug/:token`** to complete the action (reuses the existing return-URL mechanism).

**Frontend**: `/join/:slug/:token` is a **full-screen route outside the shell** (like `/u/:handle`); it renders the preview and accept/decline. The slug in the path is cosmetic; the token is the credential.

**Rationale**: token-addressed accept keeps the invitee decoupled from team internals and lets the preview stay anonymous (public info only — no roster/news, "no preview" of internal content). **Alternative**: team-scoped accept routes (rejected — leaks team identity/needs membership to resolve).

## §7 — Team activity: reuse Events, add a real `TeamId`

**Decision**: Realize FR-035 by adding a **nullable `TeamId` FK** to `EventParticipation` (→ `Team`, `OnDelete(SetNull)`), keeping the existing `TeamLabel` string as a **denormalized display snapshot**. Team activity = participations `WHERE TeamId == team`, joined to `Event`, ordered by `Event.Date` desc, projected to the **unchanged** `ActivityItemDto { eventName, date, location, teamLabel }`, paginated + capped. Profile activity is unchanged (still shows `TeamLabel`). On team delete, `SetNull` blanks `TeamId` but the `TeamLabel` snapshot remains, so history reads as a "former team" (FR-036, preserve-history clarification).

**Rationale**: augmenting (FK for attribution + label for display continuity) preserves the 003 activity DTO and profile behavior *and* survives team deletion without erasing anyone's profile activity. **Alternative**: replace `TeamLabel` outright (rejected — a `SetNull`'d participation would then show nothing after deletion; loses history).

## §8 — Team news: read-only feed now, composer later

**Decision**: `TeamNewsPost : BaseEntity` (`TeamId`, `AuthorUserId`, `Body`, created via audit `CreatedDate`). `GET /api/v1/teams/{slug}/news` returns a paginated feed projected to `TeamNewsDto { authorDisplayName, authorRole, createdDate, body }` (author role resolved from the author's current membership). **No create/edit/delete endpoint** this iteration (FR-011) — the "+ Post" affordance is disabled; posts exist via dev seed. Deleting a team cascades its news.

**Rationale**: matches "read-only feed for now; composer + polls come later" while still exercising the real feed + author-role join. **Alternative**: defer the entity entirely (rejected — the News tab is in the wireframe and spec; the read path is real).

## §9 — Data classification & visibility (public vs team-internal)

**Decision**: Two boundaries, enforced server-side (never-trust-the-client):
- **Public** (safe outside membership): team name, type, city, member count, recent activity → served by anonymous `GET /api/v1/teams/{slug}/public` (a `TeamPublicDto`) and embedded in the invite preview.
- **Team-internal** (members only): the roster (identities + roles), the news feed, and all management. The internal endpoints require an authenticated **member**; a non-member (or anonymous) receives **404** (friendly not-found) and **no** roster/news bytes. Admin-only actions additionally require the `Admin` role.

There is **no non-member preview** of internal content. A dedicated browsable public team page/route is **deferred**; the `TeamPublicDto` is ready for it. This iteration's frontend team page is members-only.

**Rationale**: encodes the clarification ("name/city/activity public; roster/news internal; no preview") as distinct DTOs + auth scopes, so a public caller physically cannot receive internal data. **Alternative**: one team DTO gated in the UI (rejected — leaks internal data on the wire).

## §10 — Team deletion semantics

**Decision**: `DELETE /api/v1/teams/{slug}` is **admin-only**, requires explicit confirmation client-side, and in one transaction removes the team and cascades **memberships**, **invitations**, and **news** (`OnDelete(Cascade)` from `Team`), while `EventParticipation.TeamId` is `SetNull` (§7). Irreversible. After deletion the team page and any former invite links resolve to a friendly not-found/expired state.

## §11 — City field

**Decision**: City is short **free text** (1–80, mirroring profile `Hometown`), **required for `CityTeam`**, **absent/null for `Mixteam`** (validated server-side: reject a city on a Mixteam and a missing city on a CityTeam). No controlled list this iteration. Discovery/search by city is out of scope.

## §12 — Enum serialization & conventions

**Decision**: `TeamType`, `TeamRole`, `InvitationKind`, `InvitationStatus` serialize as **names** via the already-global `JsonStringEnumConverter` (Program.cs) — the Angular client speaks `"CityTeam"`, `"Admin"`, `"Link"`, `"Pending"` (project gotcha: this converter is required for enum names). Angular keeps `.html`/`.css`/`.ts` separate; any scripts are `.ps1`; Tailwind styled from `DESIGN.md`. Integration tests extend the existing xUnit + `WebApplicationFactory<Program>` + `Testcontainers.PostgreSql` project.

## Summary of resolved unknowns

| Unknown | Resolution |
|---|---|
| Team identity | Creator-chosen unique **immutable slug** (handle-parity) via `TeamSlugPolicy`; names free & non-unique (§1) |
| Membership key | `TeamMembership(TeamId, UserId, Role, JoinedDate)`, unique (TeamId,UserId), unlimited per user; display via `User.Profile` (§2) |
| Last-admin guard | Transactional admin-count check under RepeatableRead/Serializable → 409 `LastAdmin` (§3) |
| Invitations | One active `Link` (reusable) + `Targeted` (emailed, consumed); raw capability token, 7-day expiry, revoke; "expired" derived (§4) |
| Invite delivery | `TeamEmailService` + new `team-invite` template, link `/join/{slug}/{token}` (§5) |
| Accept/decline | Anonymous token preview; authed accept/decline; `/join/:slug/:token` outside shell (§6) |
| Team activity | Add nullable `TeamId` FK (SetNull) to `EventParticipation`, keep `TeamLabel` snapshot; DTO unchanged (§7) |
| Team news | Read-only feed entity + GET, author-role join, seeded; composer deferred (§8) |
| Visibility | Public DTO (name/type/city/count/activity) vs members-only roster/news/mgmt; non-member → 404 (§9) |
| Delete | Admin-only; cascade memberships/invites/news; SetNull participations; irreversible (§10) |
| City | Free text, required for CityTeam, absent for Mixteam (§11) |
| Enums/conventions | Names via global `JsonStringEnumConverter`; existing test/DI/versioning patterns (§12) |
