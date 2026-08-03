# Phase 1 Data Model: Terms of Use with Community Rules

One new entity. No column is added to `AspNetUsers`, and no existing table changes shape.

---

## `TermsAcceptance`

Durable evidence that one account agreed to one version of the Terms of Use at one moment
(FR-020 – FR-025). Modelled directly on `AdminActionRecord`, which solves the same problem: a
record about an account that must outlive every state that account can reach.

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` | UUIDv7 from `BaseEntity` (Principle III) |
| `CreatedDate` | `DateTime` | **This is the acceptance moment.** Set by `AuditFieldsInterceptor`; no separate `AcceptedAt` column, following `AdminActionRecord` which documents `CreatedDate` as its "when" |
| `ModifiedDate` | `DateTime` | From `BaseEntity`. Never changes in practice — the row is write-once |
| `UserId` | `Guid` | FK → `AspNetUsers`, **`Restrict`** |
| `User` | `User` | Navigation |
| `Version` | `string(32)` | The document version agreed to. **The server's own `TermsOptions.CurrentVersion`**, never the client-submitted string (research R1) |
| `DisplayLanguage` | `string(8)` | BCP-47 base tag the document was shown in (`en`/`de`/`es`), validated against the supported allowlist server-side |

### Configuration

```
builder.Entity<TermsAcceptance>(entity =>
{
    entity.Property(a => a.Version).HasMaxLength(32).IsRequired();
    entity.Property(a => a.DisplayLanguage).HasMaxLength(8).IsRequired();

    // FR-025: "which version is this account bound by", newest first.
    entity.HasIndex(a => new { a.UserId, a.CreatedDate });

    // An account cannot accept the same version twice.
    entity.HasIndex(a => new { a.UserId, a.Version }).IsUnique();

    // Evidence must never vanish with an account row — same reasoning as AdminActionRecord.
    entity.HasOne(a => a.User)
        .WithMany(u => u.TermsAcceptances)
        .HasForeignKey(a => a.UserId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

### Why `Restrict`, and the one thing that must not happen

`Restrict` is not defensive habit here. `AccountDeletionService.EraseOwnedDataAsync` is a list of
`ExecuteDeleteAsync` calls over every table keyed by `UserId`, and this table is keyed by
`UserId`. It reads like it belongs on that list. **It does not.**

Adding it would delete the consent evidence for exactly the accounts most likely to dispute
something later. `Restrict` makes that mistake fail loudly at the database instead of succeeding
quietly. The entity carries an XML-doc warning to the same effect, and
[quickstart.md](./quickstart.md) scenario 6 tests the survival property directly.

### Relationship to `User`

`User` gains one navigation:

```
public ICollection<TermsAcceptance> TermsAcceptances { get; set; } = [];
```

A collection rather than a single reference, even though exactly one row exists per account
today. FR-021 requires that a future version change and re-acceptance need no restructuring; a
`1:1` would have to be migrated, a `1:N` does not.

---

## Lifecycle

```
                registration accepted
                        │
                        ▼
   ┌─────────────────────────────────────────┐
   │  created inside UserManager.CreateAsync │  ← same SaveChanges as User + PlayerProfile
   │  (navigation property, research R2)     │
   └─────────────────────────────────────────┘
                        │
                        ▼
                   ┌─────────┐
                   │ written │  write-once; never updated, never deleted by application code
                   └─────────┘
                        │
        ┌───────────────┼────────────────┬──────────────────────┐
        ▼               ▼                ▼                      ▼
   Suspended          Banned          Deleted            (future) new version
   unchanged         unchanged     row survives,          second row added;
                                  points at a row         earlier row keeps
                                  identifying nobody      naming the old version
```

**Registration that fails** — password rejected, handle taken, the `DbUpdateException` race, or a
non-succeeding `IdentityResult` — creates **nothing**. The acceptance is attached to the `User`
graph, so it is persisted by the same call that persists the account or by nothing at all
(FR-019, FR-022).

**Suspension and ban** (feature 013) never touch this table. A ban is a retained soft-delete; the
evidence of what the banned account agreed to is the whole reason to keep it.

**Self-erasure** (feature 037) leaves the row untouched. `NeutraliseAccountAsync` overwrites the
identifying columns on the surviving `User` row, so the acceptance continues to evidence that an
agreement was made while identifying nobody (FR-024). Re-registration with the released email
produces a **new** `User` and a **new** acceptance row; the old one is not reused or rewritten.

---

## Read shape (FR-025)

One projection, no endpoint added in this feature beyond what the admin user detail already
returns. `AsNoTracking`, explicit `.Select`, keyed by user — no pagination rule engaged because
it is a single-account lookup, not a list.

```
public sealed record TermsAcceptanceDto(string Version, DateTime AcceptedAt, string DisplayLanguage);
```

`AcceptedAt` is projected from `CreatedDate` so the DTO reads as evidence rather than as an audit
timestamp. No user identifier crosses the boundary — the caller already knows which account it
asked about (Principle II).

---

## Configuration: `TermsOptions`

Not an entity, but the authoritative value the entity records.

| Setting | Default | Notes |
|---|---|---|
| `Terms:CurrentVersion` | `"2026-08-03"` | Date-form version string. Bumped **only** when the document text changes; the parity guard fails the build if the catalogues disagree |

Identical in shape across local/Dev/Prod (Principle V), with a safe built-in default so a missing
configuration section can never mean "no version" or "any version". A version change is a code
change — the catalogue text and this value move together, which is what the guard enforces.
