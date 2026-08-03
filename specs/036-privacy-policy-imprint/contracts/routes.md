# Contract: Routes & Reachability (036)

No HTTP API contract exists for this feature — it adds no backend endpoint (plan.md Summary). The
contracts that matter are the **client routes** and the **reachability guarantee**, both of which
are externally observable and both of which are asserted in tests.

---

## 1. Routes

Added to `frontend/apps/web/src/app/app.routes.ts`, at the **top level**, siblings of the auth
screens — outside `ShellComponent`.

| Path | Component | Guard | Loading | Backend calls |
|---|---|---|---|---|
| `/privacy` | `PrivacyComponent` | **none** | lazy (`loadComponent`) | **none** |
| `/imprint` | `ImprintComponent` | **none** | lazy (`loadComponent`) | **none** |

Both routes provide the `legal` Transloco scope via `providers: [provideTranslocoScope('legal')]`
on the route definition, so the catalog is fetched when the route activates and not before.

### Hard guarantees

| # | Guarantee | Why it is a contract, not an implementation detail |
|---|---|---|
| RC-1 | Neither route carries `authGuard`, `onboardingGuard`, or any other guard | The feature's entire purpose is reachability without a session; a guard added by reflex silently destroys it |
| RC-2 | Neither route issues **any** `/api/**` request — not for content, not for session probing | SC-008. A 401-triggered refresh could redirect the reader away from a page they are legally entitled to see |
| RC-3 | Navigating to either path with cookies cleared leaves the URL unchanged — no `sign-in` redirect, no `returnUrl` | The inverse of what `authenticated-only.spec.ts` asserts for gated paths |
| RC-4 | Both paths are stable and directly linkable from outside the app | FR-003 — a supervisory authority or an email may link straight to them |
| RC-5 | Each page links to the other | FR-016 |

### Interaction with feature 026

None. 026 is enforced server-side by the global `FallbackPolicy`; these pages make no server call,
so there is nothing to allowlist. The OpenAPI/`AuthController` allowlist gotcha recorded by features
021/026 concerns *endpoints* and does not apply.

### Interaction with feature 033

`/privacy` and `/imprint` are recorded verbatim by analytics like every other path (033 FR-008).
They carry no identifier, so this is unremarkable — noted only so it is not mistaken for a leak.

---

## 2. Reachability

`FR-002` — reachability differs by audience (owner decision, 2026-08-01). One shared component,
three placements:

| State | Placement | Clicks |
|---|---|---|
| Signed out, in the shell (`/u/:handle`, landing) | `jh-app-footer` at the end of the shell's flow column | 1 |
| Auth / invite-accept screens (outside the shell) | `jh-legal-links` inline at the bottom of the card column | 1 |
| **Signed in**, desktop and mobile | **the account page only** — the shell footer does not render for members | 3 (avatar menu → Account → link) |

> **Changed 2026-08-01.** The footer originally rendered in both states. It now sits inside
> `@if (anonymous())`. A signed-out visitor is the reader a privacy policy exists for and keeps
> one-click access from anywhere; a member has already made that decision, and a document read
> once does not earn space on every screen. The `pb-[76px]` reasoning in §2.2 still applies,
> because the footer must clear the mobile bottom bar on the anonymous in-shell route.

> **Changed 2026-08-03.** `/onboarding` no longer carries the inline cluster (owner decision).
> Onboarding runs after registration, where 041 shows and records acceptance of the Terms of Use
> and where the privacy and imprint links sit next to the submit button — the reader has already
> been given them. The eight remaining off-shell placements are the ones reachable *before* or
> *without* that step: the five auth screens and the three invite-accept screens.

### 2.1 `jh-legal-links` (shared)

`frontend/apps/web/src/app/shared/ui/legal-links/`. Renders the privacy link, the imprint link, and
a `©` line. Presentation-only: no injected service, no state, no API call.

| Input | Type | Purpose |
|---|---|---|
| `variant` | `'footer' \| 'inline'` | `footer` for the app footer; `inline` for the compact form used on off-shell screens |

Carries `data-testid="legal-links"`, with `data-testid="legal-link-privacy"` and
`data-testid="legal-link-imprint"` on the anchors.

### 2.2 `jh-app-footer` (shell)

`frontend/apps/web/src/app/layout/app-footer/`, mounted in `shell.component.html` **after**
`<main>`, inside the existing `flex min-h-screen flex-col` column:

```html
<main class="min-w-0 flex-1 pb-[76px] md:pb-0"> … </main>
@if (anonymous()) { <jh-app-footer /> } @else { <jh-bottom-nav /> }
```

Placement is load-bearing. `<main>` already reserves `pb-[76px]` for the fixed mobile bottom bar, so
a footer that follows it sits above the bar and is never occluded.

The footer is **anonymous-only** (2026-08-01). Note `anonymous()` means *probed and null*, not
*not yet probed* — an undefined session keeps the full nav to avoid a flash on load, and the footer
follows the same rule rather than flashing in and out. All three states are pinned in
`shell.component.spec.ts`.

Scrolling to reach it costs no clicks; following the link costs one.

### 2.2a Signed-in placement — the account page

`frontend/apps/web/src/app/features/account/account.component.html`, below the notification-settings
link. This is the **only** in-app route to the legal pages for a member, so
`account.component.spec.ts` pins both links: if they were ever dropped from here, a signed-in member
would have no way to reach the privacy policy without typing the URL.

### 2.3 Inline placements (outside the shell)

Nine screens render full-screen outside `ShellComponent` and are therefore not covered by the
footer. Each gains `<jh-legal-links variant="inline" />` at the bottom of its content column:

| Screen | Route | Why it matters here |
|---|---|---|
| Register | `/register` | **The screen where an email address is actually handed over.** The single most important placement in the feature |
| Sign in | `/sign-in` | The default landing for an unauthenticated deep link |
| Forgot password | `/forgot-password` | |
| Reset password | `/reset-password` | |
| Verify email | `/verify-email` | |
| Onboarding | `/onboarding` | Where profile and location data are first supplied |
| Team invite accept | `/join/:slug/:token` | Anonymous preview |
| Event co-admin invite | `/event-invite/:token` | Anonymous preview |
| Party co-admin invite | `/party-invite/:token` | Anonymous preview |

The admin shell (`/admin/**`) is deliberately excluded: it is reachable only by platform admins,
who reach the footer through the rest of the app.

---

## 3. Page contract

Both pages are built on the shared `LegalPageComponent`, which owns the long-form treatment defined
in DESIGN.md (research R7) and renders:

1. `h1` — document title
2. A `caption`-step meta line — the "last updated" date, locale-formatted
3. On `en`/`es` only — the authoritative-language notice (FR-019)
4. On `/privacy` only — an anchored table of contents (`h2` targets)
5. The sections themselves, `h2` per section and `h3` per subsection
6. The cross-link to the other document (RC-5)

| # | Behaviour | Requirement |
|---|---|---|
| PC-1 | Content column capped at `container-sm` (640px ≈ 70–75 characters) | FR-017 |
| PC-2 | No horizontal scroll at 320px | FR-018, SC-007 |
| PC-3 | Heading hierarchy is unbroken (`h1` → `h2` → `h3`, no skips) so a screen reader can traverse section by section | FR-018, SC-007 |
| PC-4 | Section `id`s are stable so deep links to a section keep working | Edge case: deep-linked section |
| PC-4a | Table-of-contents entries use `[routerLink]="[]"` + `[fragment]`, **never a bare `href="#id"`** | See the note below — a bare fragment href sent readers to sign-in |
| PC-5 | Links inside prose are underlined, unlike navigation links elsewhere in the app | research R7 — colour alone is a weak affordance in a wall of text |
| PC-6 | Changing language re-renders in place without navigating away | 031 FR-004, inherited |
| PC-7 | **If the `legal` scope fails to load, a visible error state is shown — never an empty document** | Constitution VII. A blank privacy policy reads as a policy that says nothing, which is worse than an honest error |
| PC-8 | No `[innerHTML]` binding anywhere in either page | Constitution I |

### PC-4a — why a bare fragment link broke the page

Shipped as a bug and worth recording, because the fix looks like a stylistic preference and is not.

The app sets `<base href="/">`. Per the HTML spec a **fragment-only URL resolves against the
document's base URL**, not the current one — so `href="#privacy-controller"` on `/privacy` resolved
to `/#privacy-controller`. That is the root route, which is `authGuard`ed, so every table-of-contents
click threw the reader onto the sign-in screen **from a page whose whole purpose is being readable
without an account**. It is also the most-clicked control on the page.

The fix is `[routerLink]="[]" [fragment]="…"`, which stays on the active route and sets only the
fragment, plus `withInMemoryScrolling({ anchorScrolling: 'enabled' })` in `app.config.ts` so the
router actually scrolls rather than just putting the fragment in the URL.

The regression test navigates through the **real router** rather than creating the component
directly. That matters: a directly-created component has no active route, so `routerLink="[]"`
resolves to `/` there too and the test would pass against the broken code.

---

## 4. Test assertions this contract implies

| Assertion | Level | Covers |
|---|---|---|
| `/privacy` and `/imprint` render with cookies cleared; URL unchanged; no `returnUrl` | e2e | RC-1, RC-3, SC-008 |
| No `/api/**` request is issued while either page loads | e2e (route interception) | RC-2 |
| Both reachable in ≤2 clicks from a signed-out screen and a signed-in one, at desktop and mobile widths | e2e | FR-002, SC-001 |
| No horizontal overflow at 320px | e2e | PC-2, SC-007 |
| Heading hierarchy has no skipped level | unit | PC-3 |
| `en`/`es` render the authoritative-language notice; `de` does not | unit | FR-019, DM-3 |
| Scope load failure renders the error state, not an empty page | unit | PC-7 |
| Each page links to the other | unit | RC-5, FR-016 |
