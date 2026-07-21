# Phase 1 Data Model: "My team" home for teamless players

**No schema change.** This feature adds no entities, columns, indexes, or migrations. It reads existing entities and introduces one response DTO.

## Existing entities (read-only for this feature)

### TeamInvitation (`backend/Entities/TeamInvitation.cs`)

Relevant fields (unchanged):

| Field | Type | Use in this feature |
|---|---|---|
| `Id` | Guid (UUIDv7) | Identity of the invite |
| `TeamId` | Guid | Join to the team for name/type/city/member count |
| `Kind` | `InvitationKind` (`Link` / `Targeted`) | Filter: **only `Targeted`** are listed |
| `Token` | string | Returned so the UI can accept/decline via the existing token endpoints |
| `Status` | `InvitationStatus` (`Pending`/`Accepted`/`Declined`/`Revoked`) | Filter: **only `Pending`** |
| `ExpiresDate` | DateTime (UTC) | Filter: **only `> now`** (unexpired) |
| `CreatedByUserId` / `CreatedBy` | Guid / User | Source of `inviterDisplayName` |
| `TargetUserId` / `TargetUser` | Guid? / User | Scope: **`== caller`** |
| `CreatedDate` | DateTime | Ordering (newest-first) + freshness display |

**Usability rule (existing, reused):** an invite is usable iff `Status == Pending && ExpiresDate > now`. "Expired" is derived, never stored.

### TeamMembership

Read only to determine "teamless" (zero memberships) on the frontend (`MembershipService`), and mutated **only** through the existing `AcceptAsync` path when an invite is accepted. No change here.

## New DTO

### MyInvitationDto (`backend/Dtos/Teams`)

The payload of `GET /api/v1/profiles/me/invitations`, one per usable targeted invite addressed to the caller.

| Field | Type | Notes |
|---|---|---|
| `Token` | string | Capability to accept/decline via existing token endpoints |
| `TeamName` | string | For the card |
| `TeamSlug` | string | Navigation target after accept |
| `TeamType` | `TeamType` (`CityTeam`/`Mixteam`) | Serialized as name (existing enum-name convention) |
| `City` | string? | May be null |
| `MemberCount` | int | From `Team.Memberships.Count` |
| `InviterDisplayName` | string | From `CreatedBy.Profile.DisplayName`, fallback "A teammate" |
| `CreatedDate` | DateTime | Newest-first ordering + display |
| `ExpiresDate` | DateTime | Freshness / "expires in N days" display |

Returned inside the shared `PagedResult<MyInvitationDto>(Items, TotalCount, Skip, Take)` envelope.

## Frontend model

### MyInvitation (`frontend/apps/web/src/app/core/models/team.models.ts`)

Mirror of `MyInvitationDto`:

```ts
export interface MyInvitation {
  token: string;
  teamName: string;
  teamSlug: string;
  teamType: TeamType;
  city: string | null;
  memberCount: number;
  inviterDisplayName: string;
  createdDate: string;   // ISO
  expiresDate: string;   // ISO
}
```

Consumed via `PagedResult<MyInvitation>` (existing envelope type).

## State & transitions (behavioral, not persisted)

The `/my-team` view resolves to one of three states off `MembershipService`:

```text
teams.length === 0  →  Empty-state home
                        ├─ pending invites (0..n)  → [Accept] → accept(token) → refresh memberships → navigate /t/{slug}
                        │                            [Decline] → decline(token) → remove row
                        ├─ Find a team   → /browse/teams
                        └─ Create a team → /teams/new
teams.length === 1  →  (nav resolves straight to /t/{slug}; direct visit shows the chooser as today)
teams.length  >  1  →  Existing "Your teams" chooser (unchanged; no invites section in v1)
```

Accepting an invite is the only state change that alters persisted data (via the existing accept endpoint); it flips `teams.length` from 0 to ≥1 after the membership refresh, moving the player out of the empty state.
