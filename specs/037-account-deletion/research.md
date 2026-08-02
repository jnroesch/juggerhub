# Phase 0 Research: Self-Service Account Deletion

**Feature**: 037-account-deletion | **Date**: 2026-08-01

The spec's three open questions were resolved with the owner before planning (see spec → *Clarifications*). This document resolves the *technical* unknowns those answers created, all of them verified against the source rather than assumed.

The headline finding reframes the whole feature:

> **A hard delete of the account row is impossible, and the "neutral placeholder" the spec asks for already exists and already works.** The right shape is *delete the profile, neutralise the account row* — not *delete the account*.

---

## R1 — Can the account row be deleted at all?

**Decision**: **No. The `User` row must survive, anonymised.** Erasure deletes the `PlayerProfile` and neutralises the identity columns on `User`.

**Rationale**: ~20 foreign keys into `User` are `DeleteBehavior.Restrict` **by explicit design**, each with a comment saying why. A `DELETE FROM AspNetUsers` throws before it touches anything. The restricting references, from [AppDbContext.cs](backend/Data/AppDbContext.cs):

| Reference | Why it restricts (per the code) |
|---|---|
| `AdminActionRecord.Actor` / `.Target` | "History must never vanish with an account row." |
| `ChatMessage.Sender` | "a departing account must not silently delete its side of other people's conversations" |
| `UserBlock.Blocker` / `.Blocked` | "a block is a safety record and must never be dropped as a side effect of touching a user row" |
| `BadgeAward.GrantedBy`, `AchievementAward.GrantedBy` | "Preserve who granted" |
| `TeamNewsPost.Author`, `EventNewsPost.Author`, `PartyNewsPost.Author` | authored content in a shared space |
| `TeamInvitation.CreatedBy`/`.TargetUser`, `EventAdminInvitation.*`, `PartyAdminInvitation.*` | invitations both directions |
| `TeamJoinRequest.DecidedBy` | a decision recorded about someone else |
| `Party.CreatedBy`, `Training.CreatedBy`, `Conversation.Requester` | ownership of a shared object |

Every one of these is a case where the spec *also* says the record must survive (FR-021, FR-022, FR-024). The database is already enforcing the spec's own retention policy. Working with it costs nothing; working around it would mean loosening constraints that exist to protect other members' data.

**Alternatives considered**:

- *Hard delete + convert the `Restrict` FKs to `SetNull`.* Rejected. It would require a migration touching ~20 relationships, would make `ChatMessage.SenderId` nullable in a way that collides with its existing "null means system message" meaning, and would trade a guarantee that holds by construction for one that holds by review.
- *Hard delete + pre-null every reference in application code.* Rejected. Same migration for the non-nullable columns, plus a hand-maintained list of ~20 call sites that silently rots the next time someone adds an FK to `User` — exactly the "holds by memory" failure the codebase's own comments warn against.

**Consequence for FR-014**: "erase or irreversibly anonymise" is satisfied by the anonymise branch. The columns to neutralise on `User` are `Email`, `NormalizedEmail`, `UserName`, `NormalizedUserName`, `PasswordHash`, `SecurityStamp`, `ConcurrencyStamp`, `PhoneNumber`, `PhoneNumberHash`-adjacent fields, `TwoFactorEnabled`, `EmailConfirmed`, and `PreferredLanguage`.

---

## R2 — The neutral placeholder already exists

**Decision**: **Reuse the existing mechanism unchanged. Do not build a second one.**

**Rationale**: [ChatConversationService.cs:1154](backend/Services/Chat/ChatConversationService.cs#L1154) already defines it:

```csharp
internal const string PlaceholderName = "A former player";
```

and [ChatMessageService.cs:394-400](backend/Services/Chat/ChatMessageService.cs#L394-L400) already explains the exact behaviour the spec asks for:

> *"A soft-deleted or banned account's profile is hidden by a global query filter (feature 013), so DisplayName projects to null here rather than the row being absent. Their past messages must still read coherently, so they get a neutral placeholder instead of a blank or a crash — history is preserved, not rewritten."*

The critical detail is **what the placeholder keys off**. It is not keyed on ban status — it is keyed on `Sender.Profile.DisplayName` **projecting to null**. Today that happens because the ban query filter hides the profile. After an erasure it happens because *the profile row is gone*, and the `LEFT JOIN` produces the same null.

**This means FR-023, FR-024 and FR-026 are largely satisfied by deleting the profile row.** The placeholder path is already exercised, already tested, and already applied consistently across chat surfaces ([ChatBlockService.cs:40](backend/Services/Chat/ChatBlockService.cs#L40), [ChatConversationService.cs:470,698,780,1188](backend/Services/Chat/ChatConversationService.cs#L470)).

**Gap found**: `PlaceholderName` is a hardcoded English string on a backend service, and it is `internal` to the Chat namespace. Two consequences:

1. It is **not localised**, which sits awkwardly against feature 031 and FR-008's three-language requirement.
2. Non-chat surfaces (news post authors, rosters) have no shared access to it.

**Resolution**: promote the placeholder to a shared constant and localise it via the existing backend localisation service. Scope note — this is a *small* refactor with app-wide reach; it is called out in the plan's Complexity Tracking rather than smuggled in.

---

## R3 — Adding `AccountStatus.Deleted` is a trap

**Decision**: Add `AccountStatus.Deleted = 3`, **and audit every existing `!= Banned` predicate in the same change.**

**Rationale**: This is the single most dangerous part of the feature. There are seven predicates written as `!= AccountStatus.Banned`, and **a new enum value silently passes all of them**:

| Location | Predicate | Effect after adding `Deleted` |
|---|---|---|
| [AppDbContext.cs:137](backend/Data/AppDbContext.cs#L137) | `PlayerProfile` query filter | Moot — profile row is deleted |
| [AppDbContext.cs:179](backend/Data/AppDbContext.cs#L179) | `ProfilePompfe` filter | Moot — cascades with profile |
| [AppDbContext.cs:201](backend/Data/AppDbContext.cs#L201) | `ProfileAvatar` filter | Moot — cascades with profile |
| [AppDbContext.cs:345](backend/Data/AppDbContext.cs#L345) | `EventParticipation` filter | Moot — cascades with profile |
| [ChatConversationService.cs:53](backend/Services/Chat/ChatConversationService.cs#L53) | DM participant resolution | **Fails open** |
| [ChatConversationService.cs:161](backend/Services/Chat/ChatConversationService.cs#L161) | "can I DM this person?" | **Fails open** |
| [ChatConversationService.cs:822](backend/Services/Chat/ChatConversationService.cs#L822) | participant name resolution | **Fails open** |

The four query filters are incidentally safe because the profile is gone. The three chat checks query `_db.Users` **directly** and would treat a deleted account as contactable. In practice reaching one requires a handle that no longer resolves — so it is unreachable *by accident*, not *by construction*. Constitution Principle I requires the latter.

**Resolution**: replace the three chat predicates with an explicit positive test — `u.Status == Active || u.Status == Suspended` — which states "visible and contactable" rather than "not one specific bad state". Suspended accounts remain visible, matching feature 013's stated semantics ("everything else is untouched").

**Alternatives considered**:

- *A nullable `DeletedAt` on `User` instead of an enum value.* Rejected — it has the identical fail-open problem against `!= Banned` while additionally creating two competing sources of truth for account state.
- *Reuse `Banned` for deleted accounts.* Rejected outright. It would put erased accounts in the moderation denylist, in the admin ban list, and in the "unban" flow — and FR-030 explicitly forbids conflating the two meanings.

---

## R4 — Freeing the email without breaking Identity

**Decision**: Null `Email`/`NormalizedEmail`; set `UserName`/`NormalizedUserName` to a non-identifying unique token.

**Rationale**: `options.User.RequireUniqueEmail = true` (constitution, Principle IV). Identity's schema puts a **unique index on `NormalizedUserName`** and a non-unique index on `NormalizedEmail`. Postgres permits multiple `NULL`s in a unique index, so nulling the email is safe for any number of deletions and is what actually frees the address for re-registration (FR-031).

`UserName` cannot be null (unique index), so it becomes a value carrying no information — derived from randomness, **not** from the old handle or a hash of the email, since either would be a re-identification vector prohibited by FR-026.

**Verified against the source, not assumed.** [AuthService.cs:79-92](backend/Services/Auth/AuthService.cs#L79-L92) is where both outcomes come from — one code path, no new branch needed:

```csharp
var existing = await _userManager.FindByEmailAsync(email);
if (existing is not null)
{
    // "the retained banned row is exactly what blocks this email from re-registering"
    if (!existing.EmailConfirmed && existing.Status == AccountStatus.Active)
        await SendVerificationSafelyAsync(existing, ct);
    return RegisterResult.Accepted();   // neutral — and NO account is created
}
```

- **Ban** → the row is retained *with its email*, the lookup finds it, registration returns a neutral acceptance and creates nothing. The ban sticks.
- **Deletion** → the address is released, the lookup finds nothing, registration proceeds normally.

[AuthService.cs:232](backend/Services/Auth/AuthService.cs#L232) (`Status != Active`) is likewise unreachable for a deleted account because that lookup is by email too.

### ⚠ Defect found in the first draft of this design

[AuthService.cs:97](backend/Services/Auth/AuthService.cs#L97) creates the account as:

```csharp
var user = new User { UserName = email, Email = email };
```

**`UserName` is the email address**, and Identity puts a **unique index on `NormalizedUserName`**. Nulling only `Email`/`NormalizedEmail` therefore leaves the *username* colliding, and `CreateAsync` fails with a duplicate-username error.

That failure lands on [AuthService.cs:115-122](backend/Services/Auth/AuthService.cs#L115-L122), which logs the codes and returns `RegisterResult.Accepted()` — the deliberate anti-enumeration neutral response. The returning member would be **told registration succeeded when no account was created**, with nothing in the UI to indicate otherwise.

Randomising `UserName`/`NormalizedUserName` is therefore not tidiness — it is what makes FR-031 actually work. Generalised as **FR-034**: releasing the address means releasing *every* uniqueness-constrained identifier, not just the one named "email". This must be tested by completing a real re-registration, not by asserting the email column is null.

### Note on pre-emptive deletion

An **Active** member who anticipates a ban can delete first and re-register, since FR-005 only refuses accounts *already* suspended or banned. This is accepted rather than mitigated: the email denylist is a speed bump, not a control — a banned user can already register from any other address today — so barring this one path would buy nothing while requiring retention that contradicts the erasure. Recorded so it is a decision rather than an oversight.

---

## R5 — Re-authentication and session termination

**Decision**: Reuse `SignInManager.CheckPasswordSignInAsync` and `IRefreshTokenService.RevokeAllForUserAsync`. Build nothing new.

**Rationale**: Both already exist and are already used for exactly this class of operation.

- Re-auth (FR-003): [AuthService.cs:186](backend/Services/Auth/AuthService.cs#L186) uses `CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)`. Reusing it means the deletion confirmation inherits lockout, which matters because FR-034 makes re-auth load-bearing.
- Session kill (FR-016): [RefreshTokenService.cs:99](backend/Services/Auth/RefreshTokenService.cs#L99) `RevokeAllForUserAsync`, already called on password reset and on ban ([AdminUserService.cs:215](backend/Services/Admin/AdminUserService.cs#L215)).

**Caveat carried into the plan**: revoke *marks* tokens revoked; it does not delete the rows. FR-016 requires the retained per-session `CreatedByIp` to be **removed**, not merely revoked. Deletion therefore needs `ExecuteDeleteAsync` on `RefreshToken`, which is permitted — that FK is `Cascade`, not `Restrict`.

---

## R6 — Blocking conditions: what guard actually exists

**Decision**: Reuse the team last-admin rule; **define** the event and party rules here, because they do not exist.

**Rationale**: The team guard is real and precise — [TeamService.cs:396-404](backend/Services/Teams/TeamService.cs#L396-L404) counts admins and returns `MemberOpStatus.LastAdmin` with the message *"Make someone else an admin before you step down or leave."* [TeamMembership.cs:6](backend/Entities/TeamMembership.cs#L6) documents it as an invariant.

No equivalent was found for events or parties. FR-010 requires the outcome be *defined* rather than discovered, so the plan defines it: **blocking, symmetric with teams**, since `Party.CreatedBy` and `Conversation.Requester` are `Restrict` and would otherwise surface as a raw constraint violation mid-deletion — precisely the partial-failure FR-035 forbids.

**Note on FR-011** (report all blocking items at once): the existing guard is written for one team at a time. Deletion needs a *precondition query* that gathers every blocker in one pass. This is new code, not a reuse.

---

## R7 — Atomicity under the resilience principle

**Decision**: One `IExecutionStrategy.ExecuteAsync` wrapping one transaction, with **all** mutation inside the delegate.

**Rationale**: Constitution Principle VII is explicit: with `EnableRetryOnFailure` active, a user-initiated transaction must run through the execution strategy as a single retriable unit, with all state mutation inside the retried delegate. FR-035 (atomic) and FR-036 (repeat-safe) map onto this directly.

**Ordering hazard**: the profile photo lives outside the transaction. A blob delete cannot be rolled back. The safe order is **commit the database transaction first, reclaim the object second** — the inverse leaves an unreachable-but-deleted image if the transaction aborts. A failure to reclaim after commit must not report success (FR-015); it becomes an operator-actionable record (FR-039).

---

## R8 — Where the profile photo lives right now

> **⚠ CORRECTED 2026-08-01 during implementation. The original finding below was wrong.**

**Decision**: The reclaim step is **real work, not a seam**. `ProfileAvatar` is a *descriptor*; deleting the row does **not** delete the image.

**What was wrong.** The original research recorded feature 035 (media storage abstraction) as "planned but not merged" and concluded `ProfileAvatar` still held bytes in Postgres, so deleting the profile would erase the image for free. **035 is merged** — `b801df9`, PR #104. The entity carries `ObjectKey` + `SizeBytes` and the bytes live in Azure Blob Storage behind `IMediaStore`. This was caught while writing T012, when a test that seeded a `ProfileAvatar` failed to compile against a `Bytes` property that no longer exists.

**What it changes:**

- Deleting `PlayerProfile` cascades the `ProfileAvatar` **descriptor row** and leaves the blob object **orphaned**.
- FR-015 ("MUST NOT report deletion as successful if the object could not be reclaimed") therefore has teeth from day one. T026 is a real `IMediaStore.DeleteAsync(objectKey)` call, not a no-op.
- The ordering constraint in R7 becomes load-bearing rather than theoretical: **read the `ObjectKey` before the cascade removes the row**, commit, then delete the object. A blob delete cannot be rolled back.
- 035 ships a `MediaReconciliationService` — an operator-triggered orphan sweep with a grace period. It is a **backstop, not the mechanism**: leaning on it would mean the member's photo survives until someone remembers to run a sweep, which is not what "erased" means.

**Interface** ([IMediaStore.cs](backend/Services/Media/IMediaStore.cs)): `PutAsync`, `OpenReadAsync`, `DeleteAsync(key)`, `ExistsAsync(key)`. Deletion needs `DeleteAsync`, and `ExistsAsync` if T026 verifies rather than trusts.

**Lesson for the plan's risk 5**, which said "if 035 lands first, verify the ordering survives the merge": it had already landed. Check `git log` for a dependency's merge state rather than carrying a memory of it.

---

## R9 — The disclosure must name a consequence nobody expects

**Decision**: The pre-confirmation disclosure explicitly states that **messages and posts remain, attributed to no one**, and that text the member typed themselves may still identify them.

**Rationale**: This is FR-025 and FR-027, and it is the least intuitive consequence of the owner's Q2 answer. A member clicking "delete my account" overwhelmingly expects their messages to vanish. They will not. Discovering that afterwards is the failure mode most likely to produce a complaint that is *correct*.

This also constrains FR-041: the privacy policy must state it too, in German as the authoritative text. Per the standing rule that legal text is organised by category of data and avoids stating what the product does *not* do, this is phrased as a durable commitment about retained content, not as a feature-shaped negative.

---

## Resolved unknowns summary

| # | Unknown | Resolution |
|---|---|---|
| R1 | Can the account row be deleted? | No — delete profile, anonymise `User` |
| R2 | How is the placeholder built? | Already exists; keys on null profile, not on ban |
| R3 | Is a new `AccountStatus` safe? | Only with an audit of all seven `!= Banned` predicates |
| R4 | How is the email freed? | Null email/normalized email; randomise `UserName` |
| R5 | Re-auth and session kill? | Both already exist; tokens need delete, not just revoke |
| R6 | What blocking guards exist? | Teams yes; events/parties defined here |
| R7 | How is atomicity achieved? | Execution strategy + transaction; blob reclaim after commit |
| R8 | Where is the photo? | In Postgres today, cascades free; 035 seam kept |
| R9 | What must the disclosure say? | Retained messages, and self-identifying text within them |

**No unresolved NEEDS CLARIFICATION remain.**
