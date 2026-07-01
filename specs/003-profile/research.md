# Phase 0 — Research & Decisions: Player Profile & Public Share Link

Resolves the items the spec deferred to planning (handle format, avatar storage, free-text limits) and records the design decisions that shape Phase 1.

## §1 — Handle (username/slug) format & rules

**Decision**: Handle is a lowercase, URL-safe slug: `^[a-z0-9]+(?:-[a-z0-9]+)*$`, length **3–30**, no leading/trailing/consecutive hyphens. Input is normalized (trim, lowercase) before validation. A **reserved-word set** (route/system words: `admin`, `api`, `u`, `login`, `logout`, `register`, `sign-in`, `settings`, `account`, `profile`, `me`, `auth`, `dashboard`, `static`, `assets`, `well-known`, `null`, `undefined`) is rejected. Uniqueness is enforced by a **unique index** on `PlayerProfile.Handle` (race-safe) plus a pre-check for a friendly message.

**Rationale**: Restricting to `[a-z0-9-]` keeps the URL clean, avoids case-folding ambiguity in `/u/<handle>`, and sidesteps Unicode confusable/homoglyph impersonation entirely (no need for IDNA/skeleton normalization at this stage). Hyphen-as-single-separator mirrors the PayPal.me / GitHub aesthetic the user liked. The unique index is the true guarantee; the pre-check is only for UX.

**Alternatives considered**: Allowing underscores or uppercase (rejected — two handles differing only by case would collide visually and complicate canonical URLs); allowing Unicode with skeleton-based confusable detection (rejected — heavy for MVP, real impersonation risk); deriving the handle from display name (rejected — user explicitly wants the user to choose it at registration).

**Immutability**: No update path exists. `Handle` has a private/init setter set once during profile creation; no service method or endpoint mutates it. Covered by an integration test asserting there is no route/verb that changes it.

## §2 — Registration extension & enumeration neutrality

**Decision**: `RegisterRequest` gains a required `Handle`. Validation order in `RegisterAsync`: (1) password policy (unchanged, email-independent), (2) **handle format + reserved** check, (3) create the `User` **and** the `PlayerProfile` in one transaction with the unique-handle index enforcing atomic uniqueness. Email existence stays **neutral** (never revealed). Handle collision **is** reported specifically ("that handle is taken").

**Rationale**: Handles are public identifiers (they appear in shareable URLs and public pages), so a "handle taken" response is not an account-enumeration oracle the way "email taken" would be — revealing it is expected and necessary UX. Email neutrality from 002 is preserved because the handle check happens on a value that is public by design and cannot be correlated to an email without already knowing it. Wrapping user+profile creation in a transaction keeps the invariant "every account has exactly one profile."

**Availability endpoint**: `GET /api/v1/auth/handle-available?handle=` (`[AllowAnonymous]`) returns `{ handle, normalized, available, reason? }` for live UX. Rate-conscious but not a security boundary. Reserved/malformed handles return `available:false` with a `reason`.

**Alternatives considered**: A separate post-registration "choose handle" step (rejected — user wants it in registration); making handle collision neutral like email (rejected — impossible to keep public profiles addressable while hiding handle existence, and needlessly hostile UX).

## §3 — Profile fields & limits

**Decision**: `DisplayName` 1–50 chars (required once profile is edited; defaults to the handle for a brand-new profile so the public page is never blank). `Description` 0–280 chars (single short bio, Twitter-ish). `Hometown` 0–80 chars. All trimmed; length enforced server-side via data annotations + service guard; client mirrors for UX. Empty profile is valid and viewable.

**Rationale**: Social-profile norms; 280 keeps the bio short and card-friendly per the wireframe. Defaulting `DisplayName` to the handle guarantees a non-empty public identity from the moment of registration (satisfies "empty/default profile is viewable").

**Alternatives considered**: Long-form rich-text bio (rejected — out of scope, layout is a card); requiring all fields at registration (rejected — friction; profile is progressively completed).

## §4 — Profile picture storage

**Decision**: Store the avatar as **`bytea` in a dedicated `ProfileAvatars` table** (1:1 optional with `PlayerProfile`, holding `ContentType` + `Bytes`), served by `GET /api/v1/profiles/{handle}/avatar`. Upload via `PUT /api/v1/profiles/me/avatar` (multipart) with **server-side validation**: allowed content-types (`image/png`, `image/jpeg`, `image/webp`), **magic-byte sniff** (don't trust the declared type), and a size cap (`Profile:MaxAvatarBytes`, default ~2 MB). A default placeholder avatar is rendered client-side when none exists.

**Rationale**: **Environment parity is the deciding factor** — the constitution mandates local/Dev/Prod differ only by config, and forbids adding infrastructure lightly (no Key Vault). Postgres `bytea` works **identically** in every environment with zero new services, secrets, or connection strings, and Azure App Service local disk is ephemeral so disk storage is a non-starter. Avatars are small and low-traffic; keeping the bytes in a **separate table** means profile/list projections never pull the blob. This is a deliberate MVP decision with a clean migration path.

**Migration path (documented, not built)**: When scale warrants, move bytes to object storage (Azure Blob) behind the same `IProfileService.GetAvatar`/`SetAvatar` seam and the same public URL — callers and the frontend are unaffected because they only ever touch the `/avatar` endpoint, never the storage mechanism.

**Alternatives considered**: Local disk volume (rejected — ephemeral on App Service, breaks parity); Azure Blob now (rejected — new infra + secrets + parity complexity for an MVP; premature); base64 in the profile JSON (rejected — bloats every profile read, no content-type integrity).

## §5 — Pompfe catalog & selection

**Decision**: A fixed `Pompfe` **enum** — `Stab, Langpompfe, Schild, QTip, Kette, DoppelKurz, Laeufer` — is the single source of truth (stored as its int value). Selections are rows in `ProfilePompfen` (`ProfileId`, `Pompfe`) with a **unique (ProfileId, Pompfe)** constraint. The frontend `pompfen.catalog.ts` carries display order + DE/EN labels ("Stab / Staff", …, "Läufer / Runner"). Läufer is a position but lives in the same selector per the wireframe.

**Rationale**: The catalog is small and fixed, so an enum is simpler and safer than a lookup table (no seed/migration churn, compile-time exhaustiveness). A join table with a unique constraint models the many-selections-per-profile set cleanly and lets a profile have zero selections. Labels live on the client because they are presentation.

**Alternatives considered**: A `Pompfe` lookup entity (rejected — over-engineered for a fixed 7-item catalog); storing selections as a delimited string on the profile (rejected — not queryable, no integrity).

## §6 — Recent activity & minimal events model

**Decision**: `Event : BaseEntity` (`Name`, `Date` (date), `Location`). `EventParticipation : BaseEntity` (`ProfileId`, `EventId`, `TeamLabel` string). Activity = participations for a profile joined to their event, **ordered by `Event.Date` desc**, projected to `ActivityItemDto { eventName, date, location, teamLabel }`, returned via the shared `PaginationRequest`/`PagedResult<T>` with a **default cap** (e.g. take 20, hard max 100). The public profile embeds the **most recent few** (e.g. 4) inline; the `{handle}/activity` endpoint pages the rest.

**Rationale**: This is the smallest model that makes activity genuine (per the user's decision) while deferring real event management. `TeamLabel` is a lightweight string because **no team model exists yet** — it satisfies "with <Team>" display and will be swapped for a real `TeamId` FK when a teams feature lands (the DTO shape stays the same). Pagination + cap satisfy the constitution's no-unbounded-lists rule.

**Alternatives considered**: A real `Team` entity + membership now (rejected by the user — out of scope this round); deriving activity without persistence (rejected — user wants it real/seedable); embedding the full activity list in the profile payload (rejected — unbounded, violates pagination rule).

## §7 — Public vs owner response shaping (never trust the client)

**Decision**: Two distinct DTOs. `OwnerProfileDto` (authenticated `/profiles/me`) may carry owner-relevant fields; `PublicProfileDto` (`/profiles/{handle}`) carries **only** `displayName, handle, hometown, description, hasAvatar, selectedPompfen[], recentActivity[]` — **no email, no id-that-leaks-account, no account/security fields**. The public projection is written explicitly (Mapster config + a projected query), so sensitive fields are *absent from the query*, not filtered after loading.

**Rationale**: Stripping at the DTO/query boundary (rather than hiding in the UI) is the constitution's "never trust the client" applied to data — a public caller physically cannot receive email/account data. Separate DTOs prevent accidental field leakage when the entity grows.

**Alternatives considered**: One DTO with nullable/omitted fields per caller (rejected — easy to leak by omission bug); relying on the SPA to not render email (rejected — the data would still be on the wire; violates SC-002).

## §8 — Routing & the short URL

**Decision**: Public page route `/u/:handle` is a **full-screen, anonymous** Angular route *outside* the shell (like the auth screens). The owner profile is `/profile` *inside* the shell behind `authGuard`. The "copy link" affordance composes the canonical origin + `/u/<handle>`. An unknown handle renders a friendly not-found state (backend returns 404 with generic ProblemDetails; SPA shows the empty state).

**Rationale**: `/u/<handle>` is short, matches the PayPal.me aesthetic, and keeps the public page free of app chrome/auth. Reserving `u` (and the other route words) in the handle reserved-set prevents collisions between handles and app routes.

**Alternatives considered**: Bare `/:handle` at the root (rejected — collides with every current/future top-level route, forces a catch-all that's fragile); `/profile/:handle` (rejected — longer, less shareable, doesn't match the requested aesthetic).

## Summary of resolved unknowns

| Unknown (from spec Assumptions) | Resolution |
|---|---|
| Handle format & bounds | `[a-z0-9]` + single hyphens, 3–30, reserved-word set, unique index (§1) |
| Registration neutrality vs handle uniqueness | Email stays neutral; handle-taken is reported (handles are public) (§2) |
| Free-text limits | DisplayName ≤50, Description ≤280, Hometown ≤80 (§3) |
| Avatar storage mechanism | Postgres `bytea` in a separate `ProfileAvatars` table, served via endpoint; parity-first MVP (§4) |
| Pompfe modeling | Fixed enum + join table with unique (ProfileId, Pompfe) (§5) |
| Activity/events shape | Minimal `Event` + `EventParticipation` w/ `TeamLabel`; paged + capped, newest-first (§6) |
| Public data safety | Dedicated `PublicProfileDto` projected without sensitive fields (§7) |
| Short URL routing | Anonymous full-screen `/u/:handle` outside the shell (§8) |
