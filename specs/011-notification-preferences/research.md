# Research & Decisions: Notification Preferences

## 1. Sparse per-cell storage (absence = default on)

**Decision**: Store one `NotificationPreference` row per explicitly-set (user, category, channel)
cell; a missing row means the opt-out default (enabled). Unique `(UserId, Category, Channel)`.

**Rationale**: Opt-out defaults (FR-003) + extensibility without migrating existing users (FR-002).
Adding a category or channel later just widens the space of possible cells; every current user
keeps working with zero rows. Sparse rows also make "everything on by default" free — no seeding.

**Alternatives rejected**: (a) One wide row per user with a boolean column per (category, channel):
simple to read but every new category/channel is a schema migration touching all users. (b) JSON
blob of prefs per user: harder to index/query for the fan-out filter and weaker typing.

## 2. Where enforcement lives

**Decision**: **In-app** enforcement is centralized in `NotificationService`
(`CreateAsync`/`CreateManyAsync`) — it maps `NotificationType → NotificationCategory` and skips
creation when the recipient's In-app cell is off (so no row, no unread bump). **Email** enforcement
lives in each producer, immediately before it sends that category's email.

**Rationale**: In-app creation already funnels through one service, so gating there covers all
current and future producers uniformly and keeps the unread count honest (FR-007). Emails are sent
from producers (each category has its own template/recipient shape), so the Email check belongs
where the send happens (FR-008). Both paths use the same `IsEnabledAsync`.

## 3. Fail-safe resolution

**Decision**: `IsEnabledAsync` returns the default (true = deliver) on any lookup error, and the
call sites never let a preference problem throw into or roll back the originating action.

**Rationale**: FR-009 / SC-005 — an invite/role-change/news-post must succeed even if the preference
store hiccups, and a transient error should not silently drop a notification. Delivering on error is
the safe default for a non-security channel; security email is exempt entirely (§5).

## 4. Batched recipient filter for fan-out

**Decision**: For team-news fan-out, resolve both channels in bulk:
`GetEnabledRecipientsAsync(candidateIds, TeamNews, InApp/Email)` returns candidates minus those with
an explicit *disabled* row. In-app filtering happens in `NotificationService.CreateManyAsync`; email
filtering happens in `TeamNewsService` before the per-recipient send loop.

**Rationale**: One query per channel instead of N lookups; defaults (no row) remain included.

## 5. Security & sign-in is always-on and never governed

**Decision**: Auth/transactional email (email verification, password reset, password-change,
login/security — feature 002) is exempt from preferences and always sent. It appears in the settings
UI as a read-only "Always on" group with no toggles, sourced from the same matrix DTO.

**Rationale**: Wireframe ("Security & sign-in is always on"), FR-005/FR-010/SC-004, and basic account
safety — a user must not be able to turn off password-reset mail.

## 6. New category emails reuse the existing template stack

**Decision**: Add `team-role-changed.html` and `team-news.html` under `EmailTemplates/`, each
extending the shared `header.html`/`footer.html`/`base-styles.html`, and
`GenerateRoleChangedEmailAsync` / `GenerateTeamNewsEmailAsync` on `EmailTemplateService`. Sent via
the existing `IEmailSender` (Mailpit local, Resend Dev/Prod). The invite email is unchanged but now
gated by the Invites & roster Email preference.

**Rationale**: Constitution "Transactional Email" — base templates reused, HTML + inline CSS, no new
infrastructure. Makes the Email column honest for the two categories that had no email path (US2),
which the owner required (no dead toggles).

## 7. API shape — matrix read + per-cell upsert

**Decision**: `GET /api/v1/notification-preferences` returns the effective matrix (togglable
categories with per-channel booleans + the always-on group, labels server-owned). `PUT
/api/v1/notification-preferences/{category}/{channel}` with `{ enabled }` upserts one cell and
returns 204. Auto-save fires one PUT per toggle.

**Rationale**: Matches the wireframe's auto-save ("no save button") with the smallest write unit, and
keeps labels/descriptions in one server-owned place shared by the desktop matrix and mobile stack.

## 8. Settings screen placement

**Decision**: Route `/settings/notifications` (authGuard), reached from the avatar menu and the
account page. Desktop renders a category × channel matrix; mobile renders stacked category cards with
In-app / Email chips — the two layouts bind the same signals and the same PUT.

**Rationale**: Wireframe ("Settings · desktop matrix + mobile stacked", reached "from the top-right
on desktop"). Keeps notification settings discoverable next to account settings.
