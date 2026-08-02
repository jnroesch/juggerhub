# Phase 0 Research: Transactional Email Templates & Notification Preference Gating

**Feature**: 039-transactional-email-templates | **Date**: 2026-08-01

All unknowns were resolved by reading the existing implementation. No external research was
required — this feature adds nothing new to the stack; it brings four outliers onto
mechanisms features 010, 011, and 031 already established.

---

## D1. Where the escaping boundary belongs

**Decision**: Encode by default inside `EmailTemplateService.ReplaceVariables`, with an
explicit `RawHtml` wrapper for the few values that are intentionally markup. Subjects are
composed outside the template layer and stay unescaped.

**Rationale**: `ReplaceVariables` is the single choke point every templated value passes
through (`EmailTemplateService.cs:291-333`). Encoding there is the only placement that
cannot be forgotten when a new template is added — which is exactly the failure mode that
produced this feature. Encoding at each `Generate…Async` call site instead would be
explicit, but it re-creates the per-call-site discipline that already failed once: the
existing `team-news.html` drops a member-authored `{{NEWS_EXCERPT}}` in raw today, and
nobody noticed for a whole feature cycle.

Subjects must be excluded. They are plain-text headers, never parsed as markup, so encoding
a team named `Ravens & Co` would put a literal `Ravens &amp; Co` in the recipient's inbox.

**Audit of existing variables that must be marked `RawHtml`** (i.e. values that today
deliberately contain markup):

| Variable | Source | Verdict |
|---|---|---|
| `PLAN_FEATURES` | `GenerateSubscriptionWelcomeEmailAsync` — joins with `<br/>` | Needs `RawHtml`; method is unused boilerplate |
| `STATUS_STYLE` | `GenerateUnusualLoginNotificationEmailAsync` — a CSS declaration list | Needs `RawHtml`; method is unused boilerplate and its template file does not exist |
| `ACTOR_LINE`, `AUTHOR_LINE` | `team-role-changed` / `team-news` | **No** wrapper — code-composed sentences embedding a user-supplied name. Encoding is correct and desirable. |
| All URLs (`*_URL`) | `AddSharedUrls` and callers | **No** wrapper — encoding is correct inside `href`; `&` → `&amp;` is required there, not harmful. |

No other variable carries intentional markup. This closes the open risk the spec quality
checklist carried into planning.

**Alternatives considered**:
- *Encode at each `Generate…Async` call site*: rejected, as above — it is the status quo
  that failed.
- *Switch to a real template engine (Razor/Scriban) with auto-escaping*: correct in the
  abstract but disproportionate. It would rewrite all eleven existing templates and the
  loader/cache, for a feature whose job is to stop four emails being special.

### D1a. Which encoder (revised during implementation)

**Decision**: `HtmlEncoder.Create(new TextEncoderSettings(UnicodeRanges.All))`, **not**
`WebUtility.HtmlEncode`.

**Why this changed**: the first implementation used `WebUtility.HtmlEncode`, and the integration
suite caught the consequence immediately — it escapes *every* non-ASCII character, so the German
footer rendered as `Du erh&#228;ltst diese E-Mail` and the Spanish bodies were similarly littered
with numeric entities. A mail client displays those identically, so nothing was visibly broken,
but the source of every German and Spanish email became unreadable for zero security benefit.

The security requirement is to neutralize markup — `< > & " '`. Escaping `ä` is not part of that.
`HtmlEncoder.Create` with `UnicodeRanges.All` is the framework-supported way to express exactly
that boundary, so this is a configuration choice rather than a hand-rolled escaper (which would
have been the wrong answer).

**Consequence worth knowing**: apostrophes are still escaped, to `&#x27;`. So the English footer
reason "You're getting this because…" does not appear literally in the raw body. It renders
correctly; assertions just have to match on a fragment without the apostrophe. Two tests were
written against the literal string and failed on exactly this — recorded here because the next
person writing an email assertion will hit it too.

---

## D2. Whether the in-app channel also needs gating in these producers

**Decision**: No. Only the Email channel needs work.

**Rationale**: `NotificationService` already gates the in-app channel centrally for every
producer — `CreateAsync` checks `IsEnabledAsync(..., NotificationChannel.InApp)` and
`CreateManyAsync` filters through `GetEnabledRecipientsAsync(..., NotificationChannel.InApp)`
(`NotificationService.cs:48`, `:93`). Every existing fan-out therefore already honours the
in-app toggle without asking.

Two consequences worth stating, because they shrink the work:

1. The party/market producers need **only** an email-side filter added; their in-app calls
   are already correct.
2. The new event-cancellation in-app fan-out inherits gating **for free** the moment
   `EventCancelled` is mapped in `NotificationCategories.For` — no new gating code.

This also confirms FR-016 (channels independent) is satisfied by construction rather than
needing new logic.

---

## D3. How to thread the recipient's culture through a fan-out

**Decision**: Add `PreferredLanguage` to the anonymous-type projections the fan-outs already
run. Do not call `IRecipientCultureResolver.ResolveByEmailAsync` per recipient.

**Rationale**: Each fan-out already reads the recipient set with a projection that includes
`Email` and a display name — `PartyService.cs:136-139`, `PartyNewsService.cs:97-100`,
`EventService.cs:370-387`, `MarketRequestService.cs:202`. Adding `u.PreferredLanguage` is one
extra column on a query that already runs. `ResolveByEmailAsync` issues one `Users` query per
recipient, which is an N+1 inside a loop and would violate Principle III's projection rule for
no benefit.

`RecipientCultureResolver.Resolve(User)` cannot be reused directly because these projections
deliberately do not materialize `User` entities (Principle II/III: only required columns).
The resolution rule itself — `SupportedLanguages.ResolveOrDefault(preferred ?? request)` — is
a pure function and is applied to the projected string.

**Alternatives considered**:
- *Per-recipient `ResolveByEmailAsync`*: rejected, N+1.
- *Group recipients by language and render each body once*: a real optimization (the template
  render is per-recipient today) but premature — rendering is string replacement over a cached
  template, not the bottleneck. Recorded as a non-goal.

---

## D4. Making event cancellation a first-class notification

**Decision**: Append `NotificationType.EventCancelled = 8` and
`NotificationCategory.Events = 3`, map one to the other in `NotificationCategories.For`, and
fan out via `CreateManyAsync` in `EventService.NotifyCancellationAsync`.

**Rationale**: Both enums are explicitly documented as append-extensible
(`NotificationEnums.cs`), and preferences are sparse — "a row exists only for a cell the user
has explicitly set" (`NotificationPreference.cs`). A new category therefore needs **no data
migration**: absence means enabled, so every existing user starts with Events on for both
channels. This satisfies FR-021 with zero migration work.

`NotificationCategories.For` has a `_ => NotificationCategory.TeamNews` default arm. Adding
`EventCancelled` without a case would silently file cancellations under "Team news" — a
mis-gating bug that compiles and passes existing tests. The mapping case is mandatory, and
the fallback arm is the reason it is easy to forget.

**Recipient set**: the existing cancellation email already computes the correct set —
individual sign-ups plus admins of signed-up teams, de-duplicated by email
(`EventService.cs:369-392`). The projection must additionally carry `UserId` so the same set
can drive `CreateManyAsync`. De-duplication moves to `UserId` rather than email, which is
strictly more correct (one user, one notification, regardless of address casing).

---

## D5. Subject localization with embedded names

**Decision**: Add a `Get(string key, string culture, params object[] args)` overload to
`IEmailLocalizer` that applies `string.Format` to the resolved template.

**Rationale**: All four subjects embed a team and/or event name
(`"{team} wants you at {event} — JuggerHub"`). `EmailLocalizer` currently returns a fixed
string. A format overload keeps the existing in-code dictionary approach — chosen
deliberately in feature 031 as "a low-risk alternative to `.resx`/`IStringLocalizer`" — while
supporting parameters. Word order differs across the three languages, so positional
`{0}`/`{1}` placeholders are required rather than concatenation.

`string.Format` runs with the invariant culture: the app runs in globalization-invariant mode
(noted in `RecipientCultureResolver`), and the arguments are strings, so no culture-sensitive
formatting occurs.

---

## D6. Template file layout and build inclusion

**Decision**: Twelve new files — `{event-cancelled,party-request,party-news,market-invite}.html`
under each of `EmailTemplates/{en,de,es}/`.

**Rationale**: `LoadTemplateAsync` resolves `EmailTemplates/{culture}/{name}` and falls back
to `en/` per file (`EmailTemplateService.cs:260-289`). Authoring all three languages (FR-009a)
means the fallback never engages for these four.

The `.csproj` already globs `EmailTemplates/**/*.html` with `CopyToOutputDirectory` — new
files are picked up with **no project-file change**.

**Fallback hazard (FR-026a)**: fallback is per *file*, not per *placeholder*. A `de` template
that omits `{{PARTY_URL}}` yields a German email with no call-to-action and no error — the
same class of hazard feature 036 guarded against with an identical-key-set test. Mitigation is
a test asserting the three variants of each template contain the same placeholder set.

---

## D7. Footer legal links

**Decision**: Add `PRIVACY_URL` and `IMPRINT_URL` to `AddSharedUrls`, and link them from
`footer.html` in all three languages.

**Rationale**: `AddSharedUrls` is where shared chrome links are already derived from
`EmailOptions.FrontendBaseUrl` (`EmailTemplateService.cs:210-225`), so the new links inherit
the "an email can never point at a different host than the link beside it" property and
FR-023 is satisfied by construction. Routes `/privacy` and `/imprint` are confirmed present
and unguarded (`app.routes.ts:230-238`).

All three `footer.html` files already exist, so this is an edit in each, not a new file.

---

## D8. Frontend surface for the new notification type

**Decision**: Extend the existing type union, payload types, narrowing helper, and the row
component's `link`/`title`/`supporting` computeds; add i18n keys in en/de/es. Add
`'Events'` to `NotificationCategoryId`.

**Rationale**: `notification-row.component.html` has a `@default` icon arm, so an unhandled
type degrades to a generic bell rather than breaking — but `title`/`supporting` would fall
through to `alerts.row.fallbackTitle` and an empty line, which is a visibly broken row. The
computeds must be extended.

The preferences screen renders categories from the server-supplied list with no hardcoded
rows, so adding a category needs **only** the `NotificationCategoryId` union member
(`notification-preferences.models.ts:7`) — no template or component change. Server-owned
labels (FR-020) are what make this cheap.

**Link target**: `/events/{eventId}` — the cancelled event page remains viewable, which the
current cancellation email copy already promises.

---

## Non-goals confirmed during research

- Not translating the pre-existing `invitation.html` / `team-news.html` bodies (#84).
- Not deleting the unused boilerplate generators, though D1 confirms two of them are the only
  consumers of `RawHtml`. A follow-up may remove both the methods and the wrapper's only uses.
- Not batching template rendering by language (D3).
- Not adding an in-app notification for the other three emails — they already have one.
