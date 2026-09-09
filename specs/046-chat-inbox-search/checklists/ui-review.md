# UI Review Checklist: Chat Inbox Search by People and Conversation Names

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before a feature is considered done.
**Created**: 2026-09-08
**Feature**: [spec.md](../spec.md)

**How to use**: This is an *implementation-quality* gate, run **after** UI is built and
**before** verification — not a spec-quality gate like `requirements.md`. Checked against the
diff of `frontend/apps/web/src/app/features/chat/chat-inbox/chat-inbox.component.html`,
`chat-inbox.component.ts` and the three `public/i18n/*.json` catalogues.
[DESIGN.md](../../../DESIGN.md) is the source of truth: if a check ever conflicts with it,
DESIGN.md wins and the conflict is reported rather than silently resolved.

**Surface under review**: the search field's copy, a `role="status"` line while a search is in
flight, the "nothing matched" empty state, and the results — which are the existing inbox rows
rendered from one `@for`. The "In your messages" and "People" sections were removed. No new
component, colour, spacing step or icon.

## Color & tokens

- [x] CHK001 Components reference **semantic aliases** (`surface-card`, `text-body`, `brand-primary`, `border-default`…), never raw scale steps — new markup uses `text-muted`, `text-body-sm`, `surface-card`, `border-border-muted` only
- [x] CHK002 **Exactly one coral `brand-primary` CTA per view** — unchanged: the "Start a chat" `jhButton`; the removed people-row "Chat" text-link buttons were secondary
- [x] CHK003 Lemon `brand-highlight` is used only for small pops — not used
- [x] CHK004 Status tokens paired — the pre-existing load-error alert is unchanged; the new status line is informational (`text-muted`), not a status colour
- [x] CHK005 No new colors introduced ad hoc

## Typography, numbers & voice

- [x] CHK006 Headings/hero use **Hubot Sans**; body and UI text **Mona Sans** — no heading added; the two eyebrow `h2`s were removed
- [x] CHK007 Scores, stats, times, counts in the **mono** face — row times/badges unchanged (`font-mono`)
- [x] CHK008 **Sentence case everywhere** — "Find a chat by name…", "Search your chats by name", "Nothing matched “…”." (en/de/es alike)
- [x] CHK009 Nothing meaningful below 12px — status and empty lines are `text-body-sm`
- [x] CHK010 Copy addresses the reader as **"you"**; CTAs invite; no emoji — "Search your chats by name" / "Deine Chats…" / "tus chats…"; placeholder ends in an ellipsis like every other search field in the app

## Layout & spacing

- [x] CHK011 Touch targets ≥ 44px — the input keeps `min-h-11`; rows unchanged
- [x] CHK012 Spacing from the 4px scale — `mt-lg`, `py-xl`, `px-md`, `py-sm`
- [x] CHK013 Centered column, mobile-first — `max-w-container-md` unchanged
- [x] CHK014 Section rhythm — not applicable (no new section); page unchanged

## Shape & elevation

- [x] CHK015 No sharp corners — `rounded-md` input, `rounded-lg` list, `rounded-pill` avatars, all unchanged
- [x] CHK016 Shadows are the warm tokens — none added
- [x] CHK017 Cards: white, 1px muted border — the list keeps `bg-surface-card border-border-muted`; no card lift applies to a list
- [x] CHK018 Larger shadows reserved — none used

## Motion & states

- [x] CHK019 Transitions use token durations — rows keep `transition-colors duration-fast`
- [x] CHK020 Focus always visible — input `focus:border-focus focus:ring-2 focus:ring-focus`; rows `focus-visible:ring-2 focus-visible:ring-focus`
- [x] CHK021 Button hover/press — unchanged `jhButton`
- [x] CHK022 No infinite decorative loops in content — none added (see the pre-existing note below)

## Iconography

- [x] CHK023 Lucide line icons only — the search icon is unchanged
- [x] CHK024 No emoji as icons

## Accessibility

- [x] CHK025 Body text ≥ 4.5:1 — new text uses the same `text-muted`/`text-heading` tokens as the surrounding page
- [x] CHK026 Status never by colour alone — the in-flight state is a sentence; the empty state names the term
- [x] CHK027 Keyboard-reachable with labels/roles — the input keeps its `sr-only` label (now "Search your chats by name"); results are the same anchor rows; the in-flight line carries `role="status"` and is announced

## Empty, loading & error states

- [x] CHK028 Empty states offer a next step — the search's "nothing matched" state is deliberately plain: spec FR-008 requires "a plain empty state that names the term, never an error", and the inbox's own warm empty state (`chat-empty`, with "Start a chat") is untouched and now explicitly suppressed while a term is active so the two never stack
- [x] CHK029 Loading and error states exist and are styled — in-flight search is one muted `body-sm` line with `role="status"` (DESIGN.md: never a spinner or skeleton, never a layout shift); the rows stay visible beneath it; the load-error `jh-alert`-style paragraph is unchanged

## Feature-specific UI

- [x] CHK030 Results are the inbox rows themselves: one `@for` over `displayed()` renders both the plain inbox and a match set (FR-005); no second row template exists
- [x] CHK031 No "In your messages" / "People" heading, snippet or message excerpt remains anywhere in the inbox (FR-011); the component spec asserts it
- [x] CHK032 The inbox is not blanked while a term is entered: until the first page for a term arrives, `displayed()` falls back to the live inbox under the status line (DESIGN.md "keep what's there")
- [x] CHK033 Copy changed in all three catalogues at once (`searchSr`, `searchPlaceholder`; `inYourMessages`, `people`, `chat` removed) — the parity spec is part of the full test run
- [x] CHK034 A late response for an older term cannot overwrite a newer one (`searchSeq`), so fast typing never shows stale rows

## Notes

- **Pre-existing, out of scope, reported not resolved**: the inbox's *initial* load (before any
  search) still renders three `animate-pulse` skeleton blocks (`chat-inbox.component.html`,
  "Loading" section). DESIGN.md's loading rule ("one muted text line … never a spinner or
  skeleton") post-dates feature 019's inbox. This feature did not touch that block; converting
  it is a small follow-up for the inbox as a whole, not for search.
- No conflict with DESIGN.md was found in the surface this feature changed.
