# UI Review Checklist: Lazy Direct-Message Creation

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is done.
**Created**: 2026-07-21
**Feature**: [spec.md](../spec.md)

**Scope**: One new view — the **compose** screen (`ChatComposeComponent`, `/chat/compose/:handle`):
a recipient header, a calm empty-thread affordance, and the message composer. It mirrors
the existing conversation view's header + composer. No other UI changes (the entry-point
edits are routing only).

## Color & tokens

- [x] CHK001 Semantic tokens only (`bg-surface-card`, `border-border-muted`, `text-heading`, `text-muted`, `bg-brand`, `text-on-accent`, `text-danger-fg`) — no raw scale steps.
- [x] CHK002 Exactly one coral `brand-primary` CTA in the view (the send button); everything else is neutral — matching the conversation composer.

## Typography & voice

- [x] CHK008 Sentence case throughout ("New message", "Loading…", the hint, "Message …" placeholder).
- [x] CHK010 Voice is warm and addresses the reader ("This is the start of your conversation with …. Say hello …"); no emoji.

## Layout & spacing

- [x] CHK011 Interactive controls meet the 44px target (`min-h-11`/`min-w-11` on the back link, composer input, and send button — same as the conversation view).
- [x] CHK012 Spacing composes from tokens (`px-md`, `py-sm`, `gap-sm`, `py-lg`); the layout is a flex column that fills the chat pane and reflows on mobile/desktop (FR-012).

## Shape, elevation & motion

- [x] CHK015 `rounded-md` controls; `rounded-pill` avatar placeholder — radius matches element type.
- [x] CHK019/021 The send button transitions on `duration-fast`, darkens + gains a coral glow on hover, and nudges/scales on press (reused verbatim from the conversation composer).

## Accessibility

- [~] CHK025 Contrast: the coral send button is white-on-`coral-4` — the standing app-wide primary-button contrast pattern (owner's brand decision), identical to the existing conversation send button. Not resolved here.
- [x] CHK026 Status is never colour-only — the send failure is a bordered danger panel **with text** and `role="alert"`; the "can't message" and loading states are text.
- [x] CHK027 Keyboard-reachable with visible focus (`focus-visible:ring-2 ring-focus`) on the back link, input, and send; the input is labelled (`sr-only` "Message"); the send button has an `aria-label`.

## Empty, loading & error states

- [x] CHK028 The empty thread is a calm, low-pressure hint ("Say hello — the chat is created when you send your first message") rather than a blank void.
- [x] CHK029 Loading ("Loading…"), unavailable ("You can't message this player right now."), and send-error states all exist and are styled to the system.

## Feature-specific UI

- [x] CHK030 The compose view names the recipient (display name + `@handle`) and clearly reads as "New message", so the user knows nothing is sent/created until they submit.
- [x] CHK031 On first send the URL is replaced with the real `/chat/:id` (no spent compose URL left in history); leaving before sending shows no thread anywhere.

## Notes

- One `[~]` item is the standing app-wide primary-button contrast decision, followed here
  for consistency with the existing conversation composer — flagged for the owner, not
  resolved. No new tokens or visual style introduced.
