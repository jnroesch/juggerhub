# Contract: Confirmation dialogs and the new Alerts rows

## `jh-confirm-dialog` (new, `shared/ui/confirm-dialog/`)

Presentational. The host renders it inside `@if` while a confirmation is pending and owns the call.

| Input | Type | Meaning |
|-------|------|---------|
| `heading` | string (translated) | dialog title, `aria-labelledby` |
| `body` | string (translated) | explanation, `aria-describedby` |
| `keepLabel` | string | the safe answer (focused on open) |
| `confirmLabel` | string | the destructive answer (`variant="danger"`) |
| `busyLabel` | string | the destructive answer's label while `busy` |
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
