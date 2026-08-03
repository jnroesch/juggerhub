# Contract: `/terms` route and link surfaces

Extends `specs/036-privacy-policy-imprint/contracts/routes.md`. Every rule there applies
unchanged; this records what a third document adds.

---

## RC-1 — The route

```
{
  path: 'terms',
  loadComponent: () => import('./features/legal/terms/terms.component').then((m) => m.TermsComponent),
}
```

Declared alongside `privacy` and `imprint`, under the same comment block, with the same three
properties:

| Property | Value | Why |
|---|---|---|
| **Guard** | **none** | A document someone must agree to *before* they have an account cannot sit behind a sign-in wall. This is the third documented exception to feature 026's authenticated-only rule. Do not add `authGuard` by reflex |
| **Shell** | **outside** | The shell's anonymous bar pushes sign-in and register — the wrong framing for a reader deciding whether to agree, which is the entire audience for this page |
| **Backend calls** | **none** | Same as 036. A 401-triggered refresh must never redirect a reader away from a document they are entitled to read. The text is a static asset |

The register page is where the version is read from the catalogue — **not** this page. `/terms`
renders; it does not participate in the acceptance flow.

---

## RC-2 — Cross-links between documents

`LegalPageComponent`'s binary `siblingLink` + `siblingLabelKey` inputs become:

```ts
readonly siblings = input.required<readonly { link: string; labelKey: string }[]>();
```

Each document declares the other two:

| Page | Siblings |
|---|---|
| `/terms` | `/privacy`, `/imprint` |
| `/privacy` | `/terms`, `/imprint` |
| `/imprint` | `/terms`, `/privacy` |

New catalogue cross-link labels: `crossLink.toTerms` (short) and `crossLink.toTermsLong` (the
in-document link text), matching the existing `toPrivacy`/`toPrivacyLong` pair.

The privacy and imprint pages are updated in the same change. This refactor is sequenced
**before** the terms page is added, so no page is ever left with a stale input shape.

---

## RC-3 — `jh-legal-links`

The component gains a third anchor, `routerLink="/terms"`, first in the cluster — it is the
document with contractual force, and the one a registering reader is being asked about.

```
Terms · Privacy · Imprint            (+ copyright in the footer variant)
```

New main-catalogue key `legal.terms`. The label lives in the **main** catalogue, not the lazy
legal scope — the footer renders on every screen and cannot wait for a scope that loads only on
legal routes. This is the existing rule for `legal.privacy` / `legal.imprint`; the new key
follows it.

**This is the whole of FR-010.** All 11 placements render this one component — the app footer
plus 10 off-shell screens — so a single change covers every surface at once:

```
layout/app-footer                       (every screen inside the shell)
features/account
features/auth/register                  ← matters most for this feature
features/auth/sign-in
features/auth/forgot-password
features/auth/reset-password
features/auth/verify-email
features/onboarding
features/events/event-invite-accept
features/parties/party-invite-accept
features/teams/invite-accept
```

No screen-by-screen edit is needed, and none should be added.

---

## RC-4 — Presentation

Rendered by the existing shared `LegalPageComponent`, so DESIGN.md's **Long-form content**
treatment is inherited rather than reimplemented: `container-sm` measure, `h1`→`h2` hierarchy
with no skipped levels, caption-step meta line, underlined in-prose links, no card and no shadow.

| Aspect | Value |
|---|---|
| Table of contents | **On.** The document is long and its sections are the ones people navigate to — "How to behave here" above all |
| Authoritative notice | Shown on `en` and `es`, not on `de` (existing `showAuthoritativeNotice` logic) |
| Meta line | Version **and** last-updated, both read from the document's own node rather than the shared `meta.lastUpdated` (research R4) |
| Anchors | `terms-<sectionKey>`, from the existing `anchor()` helper — stable, so deep links keep working |
| Error state | Existing visible error + retry. Never a blank or partial document |

No `[innerHTML]` anywhere. Paragraphs are catalogue array entries, never strings carrying markup,
so there is no sink to sanitise (Principle I). The acceptance-control text on `/register` follows
the same rule: it is composed from translated segments around a real `routerLink`, not from an
HTML string.
