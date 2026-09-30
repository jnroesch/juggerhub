# Contract: Confirmation dialogs and the new Alerts rows

## `jh-confirm-dialog` (new, `shared/ui/confirm-dialog/`)

Presentational. The host renders it inside `@if` while a confirmation is pending and owns the call.

> **Amended by GH #392**: the two older confirmations now ask through this component too — the news
> post's delete dialog (features 057/059, inside `jh-news-post`) and the team page's join / withdraw
> confirmation (feature 009). The second destroys nothing, so the acting answer's look became an
> input, `confirmVariant` (default `danger`). Two things the browser walk found were fixed in the
> component, for every host: it has no box of its own (`display: contents` — in a news post's row the
> open dialog pushed the ⋯ button aside), and it takes the focus off the page as it is created, before
> it is in the page (asked for by a menu item that its menu then removes, the answers used to grow in
> from nothing — measured on this feature's roster *Remove* as well).

| Input | Type | Meaning |
|-------|------|---------|
| `heading` | string (translated) | dialog title, `aria-labelledby` |
| `body` | string (translated) | explanation, `aria-describedby` |
| `keepLabel` | string | the safe answer (focused on open) |
| `confirmLabel` | string | the answer that acts |
| `confirmVariant` | `ButtonVariant` | how the acting answer looks; `danger` unless the host says otherwise (GH #392) |
| `busyLabel` | string | the acting answer's label while `busy`; falls back to `confirmLabel` |
| `busy` | boolean | disables both answers and ignores Escape |
| `error` | string \| null (translated) | one `role="alert"` line under the body |

| Output | When |
|--------|------|
| `confirmed` | the destructive answer is pressed (not while busy) |
| `dismissed` | the safe answer or Escape (not while busy) |

Behaviour: fixed full-screen scrim; a bottom sheet below `sm`, a centred card from `sm`; the safe
answer receives focus after first render; Tab and Shift+Tab stay inside; answers wrap as a row
(`flex-wrap`), full labels, never truncated. `data-testid`s: `confirm-dialog`,
`confirm-dialog-keep`, `confirm-dialog-confirm`, `confirm-dialog-error`.

## Team page (`/t/{slug}`) — roster ⋯ → Remove

- Remove closes the menu and opens the dialog: heading *Remove {name} from {team}?*, body *They lose
  access to the team's members-only pages and chat. We'll let them know.*, answers *Keep {name}* /
  *Remove from team*.
- Dismiss → focus returns to that member's ⋯ button. Removed → dialog closes, roster reloads, focus
  to the roster heading.
- 404 / 403 → dialog closes, a page-level note (`role="status"`), page reloads. Other failures → the
  dialog's error line; confirming again is the retry.

## Party page (`/parties/{id}`)

- *In* tab Remove → heading *Take {name} out of the party?*, body ends *They won't be sent a message
  about it.* (the team dialog says the opposite, so an admin who knows one does not assume the
  other), answers *Keep {name}* / *Remove from party*.
- *Declined* tab Remove → heading *Clear {name}'s answer?*, body says they will count as not having
  answered, answers *Keep answer* / *Clear answer*.
- Disband → heading *Disband this party?*, body keeps today's meaning (cannot be undone), answers
  *Keep party* / *Disband*. `window.confirm` is no longer called anywhere on the page.
- Removal 404 → dialog closes, page-level note, reload. Other failures → the dialog's error line.

## Alerts rows

| Type | Icon | Title | Supporting | Link |
|------|------|-------|------------|------|
| `TeamMemberRemoved` | `user-minus` (info tone) | *You're no longer a member of {team}* | *The team's members-only pages and chat are closed to you now* | `/t/{slug}` |
| `TeamMemberDeparted` | `user-minus` (info tone) | *{name} left {team}* / *{name} was removed from {team}* | *No longer on the team's roster* | `/t/{slug}` |

`{name}` is `actorDisplayName ?? alerts.row.formerPlayer` (the placeholder 058 uses).

> **Changed during implementation**: the plan had no supporting line for either row. Every other row
> has one, and the row's spec treats an empty supporting line as a broken row (feature 039), so each
> got a factual line. Neither names a person, gives a reason or makes a suggestion (FR-011, FR-015).

## Accept sites — `429`

Alerts inline Accept, *My team*, onboarding team step, `/join/{slug}/{token}`, Home *Needs you*: on
`429` the invitation stays actionable and the site shows `teams.inviteLimited`. Never retried.
