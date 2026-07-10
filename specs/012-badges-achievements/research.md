# Research & Design Decisions: Badges & Achievements

Feature 012. Resolves the approach for each significant choice. No open `NEEDS CLARIFICATION` remain (the three scope decisions were resolved with the user; see spec FR-003/FR-011/FR-013).

---

## D1 — Two separate systems, modeled as parallel entity pairs

**Decision**: `BadgeDefinition` + `BadgeAward` and `AchievementDefinition` + `AchievementAward` are two independent entity families. Shared enums (`AwardSource`, `AwardStatus`, `SubjectType`) live in one `RecognitionEnums.cs`.

**Rationale**: The user chose "two separate systems" over a unified type. Separate families keep each catalog, admin surface, and display group cleanly distinct, and let achievements diverge (accomplishment context) without conditional logic on a shared row. This matches the constitution's "avoid unnecessary abstractions" better than a TPH hierarchy with a discriminator.

**Alternatives considered**:
- *Single `RecognitionDefinition` with a `Kind` enum* — rejected: contradicts the explicit decision and forces nullable/branching fields.
- *EF TPH inheritance from a shared base* — rejected: adds mapping complexity for little payoff at this scale; duplication here is small and readable.

**Cost**: minor duplication across the two pairs (roughly parallel EF config and service logic). Accepted deliberately.

---

## D2 — Polymorphic subject: `PlayerProfileId?` + `TeamId?` with a DB CHECK

**Decision**: Each award row carries two nullable FKs — `PlayerProfileId?` and `TeamId?` — with a database CHECK constraint that **exactly one** is set. Cascade-delete from each subject removes its awards.

**Rationale**: Directly mirrors the established `EventSignup` polymorphic pattern (`UserId?`/`TeamId?` + CHECK). Preserves referential integrity and gives free cleanup on subject deletion (spec edge case). Linking the player side to `PlayerProfile` (not `User`) keeps display queries profile-centric — the profile is the public identity that shows the award, and Team is its public analog.

**Alternatives considered**:
- *`SubjectType` enum + bare `SubjectId` Guid* — rejected: loses FK integrity and cascade cleanup; would need manual orphan handling.
- *Separate `PlayerBadgeAward` / `TeamBadgeAward` tables* — rejected: doubles tables and queries with no benefit.

---

## D3 — Definition applicability & grant-time validation

**Decision**: Each definition carries `AppliesToPlayers` and `AppliesToTeams` booleans (at least one true). Granting validates the target subject type against these and rejects mismatches (spec FR-005) with a clean 400/409, never a raw error.

**Rationale**: Booleans are simplest, queryable, and cover players / teams / both. Validation lives in the service (thin controller).

---

## D4 — Admin gate: a `PlatformAdmin` authorization policy backed by a config allowlist

**Decision**: Introduce a named ASP.NET Core authorization **policy** `PlatformAdmin`. Controllers use `[Authorize(Policy = "PlatformAdmin")]`. v1 backs it with a requirement + handler that resolves the caller's user id from the JWT `sub` claim, loads the user via `UserManager<User>`, and checks the (case-insensitive, normalized) email against a configured allowlist `Admin:Emails` (`AdminOptions`).

**Rationale**: The policy name is the stable seam. Issue #21 replaces only the handler/requirement (e.g., with a role check) — **controllers and behavior stay identical**. Email is the practical admin identifier (Identity enforces unique email); GUIDs would be impractical to configure. Enforcement is fully server-side. The allowlist flows through `.env` locally and GitHub Environments deployed, exactly like other config/secrets.

**Alternatives considered**:
- *Config of admin user GUIDs* — rejected: impractical to obtain/configure; churny.
- *Seed a real Identity role now* — rejected: that is issue #21's job (security-sensitive, needs its own spec); deliberately deferred.
- *Trust an `email` claim without a DB lookup* — rejected for now: email may not be a present/verified claim; a `UserManager` lookup on admin-only (rare) requests is acceptable and robust.

**Known interim limitation (documented)**: because the gate keys on email, an account holding a listed email is treated as admin. Admin emails are expected to belong to existing, controlled accounts; the real role (#21) removes this coupling. No privilege escalation path is opened for non-listed users.

---

## D5 — Icon storage: a separate 1:1 blob table per definition (like `ProfileAvatar`)

**Decision**: Each definition has an optional 1:1 icon entity (`BadgeIcon` / `AchievementIcon`) storing validated image `Bytes` + `ContentType` in Postgres `bytea`, served via a dedicated public endpoint. Catalog/list projections never pull the blob.

**Rationale**: Reuses the exact, already-accepted `ProfileAvatar` approach (parity-first MVP with a documented path to object storage — cf. issue #13). Keeping the blob in a side table keeps catalog listings and the embedded profile/team award lists lean.

**Alternatives considered**:
- *`IconUrl` string* — rejected for v1: pushes asset hosting elsewhere and gives no first-class curated art; can be added later.
- *Bytes column directly on the definition* — rejected: would bloat catalog projections.

**Upload validation**: magic-byte sniff + size cap, mirroring the avatar upload (png/jpeg/webp), enforced server-side.

---

## D6 — Display integration: embed bounded award lists in existing page payloads

**Decision**: Extend the existing public + owner profile responses and the team detail/page response with `badges` and `achievements` arrays (each item: name, description, icon reference, earned date, and — for achievements — optional context). Lists are bounded; a dedicated paginated "all awards" read endpoint is deferred until counts warrant it.

**Rationale**: Consistent with how the profile already embeds Pompfen, Teams, and recent activity in one fetch — no extra round trip on page load, and the display area shows a small set. Revoked awards are filtered out at the query (spec FR-009).

**Alternatives considered**:
- *Separate `GET /profiles/{handle}/badges` endpoints* — rejected for v1: extra round trips for a small list; revisit if a subject can earn many.

---

## D7 — Award lifecycle, duplicate prevention, durability

**Decision**: `Award` carries `Source` (`Manual` in v1; `Automatic` reserved), `GrantedByUserId`, `EarnedAt`, `Status` (`Active`/`Revoked`), `RevokedByUserId`, `RevokedAt`, `RevokedReason`. A **filtered unique index** on `(DefinitionId, PlayerProfileId)` and `(DefinitionId, TeamId)` **where `Status = Active`** prevents duplicate active awards (FR-006) while allowing a re-grant after revocation. Revoked rows are retained (FR-009/FR-012).

**Rationale**: Reserving `Source` is what makes automatic awarding (deferred US3) addable later "without reworking earned-award history" (FR-011). Filtered unique index enforces the invariant at the database, not just in code. Durability = removal only via explicit revoke.

---

## D8 — Achievement context

**Decision**: Achievement **awards** carry optional `ContextYear` (int?) and `ContextLabel` (string?) — e.g., year 2026, label "National Championship". Badge awards have no context.

**Rationale**: "won the championship in year Y" is per-grant context, so it belongs on the award, not the definition. Keeps badges strictly simpler than achievements, reinforcing the two-systems separation.

---

## D9 — Frontend admin surface & route guarding

**Decision**: A new `features/admin/` area with badge and achievement management screens (list catalog, create/edit/retire, grant to a subject by handle/slug, revoke). Routes guarded client-side for UX only (an admin guard hiding the nav/route); the **server policy is the security boundary**. Display components live with the profile and team-detail features, styled per DESIGN.md, reusing the badge slot visual from the existing stub.

**Rationale**: Matches the constitution (client checks are UX-only) and the existing feature-folder structure. The admin guard can later key off a real admin claim once #21 lands.
