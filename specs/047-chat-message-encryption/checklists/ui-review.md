# UI Review Checklist: Chat Message Encryption at Rest + Database Transport Hardening

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is considered done.
**Created**: 2026-09-09
**Feature**: [spec.md](../spec.md)

**Surface under review** — small, and worth naming precisely so this is not read as broader than it is:

1. One new bubble state in `chat-conversation.component.html` (~L112) — a message the server could
   not decrypt, rendered with the **existing** tombstone treatment and a different string.
2. One new copy string, `chat.conversation.messageUnavailable`, in en/de/es.
3. The rewritten messages paragraph in `public/i18n/legal/{de,en,es}.json` L129 — prose inside the
   already-shipped `LegalPageComponent`, no markup of its own.

**No new component, no new token, no new layout, no new colour, no new icon.** Items below that
have no surface in this diff are marked **n/a** rather than ticked — a tick would claim a check
that was never run.

## Color & tokens

- [x] CHK001 Semantic aliases only — the new branch reuses the deleted-message bubble's classes verbatim (`bg-surface-muted text-body` / `bg-brand-active text-on-accent`); no raw scale step is introduced
- [x] CHK002 Exactly one coral CTA per view — unchanged; the new state adds no action at all
- [x] CHK003 Lemon `brand-highlight` unused here
- [x] CHK004 No status colour is used: an unreadable message is deliberately **not** styled as an error. It is a neutral absence, like a tombstone, not a failure the reader can act on
- [x] CHK005 No new colour value introduced

## Typography, numbers & voice

- [x] CHK006 Faces unchanged — the placeholder inherits the bubble's body text
- [ ] CHK007 n/a — no numeric content
- [x] CHK008 Sentence case: "This message can't be displayed." / "Diese Nachricht kann nicht angezeigt werden." / "Este mensaje no se puede mostrar."
- [x] CHK009 Nothing below 12px — `text-body-md` on the bubble, unchanged
- [x] CHK010 Voice: states the fact plainly and does not blame the reader, apologise, or invite an action that does not exist. No emoji. The legal paragraph keeps the policy's existing second-person voice ("Deine Nachrichten…")

## Layout & spacing

- [ ] CHK011 n/a — the new state is text inside an existing bubble; no control is added. The delete affordance on one's own message keeps its existing ≥44px target
- [x] CHK012 Spacing unchanged (`px-sm py-xs` on the existing bubble)
- [x] CHK013 Container unchanged
- [x] CHK014 Section rhythm unchanged

## Shape & elevation

- [x] CHK015 `rounded-md` on the bubble, unchanged
- [ ] CHK016 n/a — no shadow added
- [ ] CHK017 n/a — no card added
- [ ] CHK018 n/a

## Motion & states

- [ ] CHK019 n/a — no transition added
- [x] CHK020 Focus unchanged; the placeholder is not focusable and does not need to be
- [ ] CHK021 n/a — no button added
- [x] CHK022 No animation

## Iconography

- [x] CHK023 No icon added. Considered and rejected: a lock or warning glyph would read as either a security boast or an error the reader could fix, and it is neither
- [x] CHK024 No emoji

## Accessibility

- [x] CHK025 Contrast — the placeholder reuses the deleted-message treatment, `italic opacity-80` on `bg-surface-muted text-body`. ⚠ **See the note below**: this inherits an existing, already-shipped concern rather than introducing one
- [x] CHK026 Not colour alone — the state is conveyed entirely by **text**, which is the strongest form of this rule
- [x] CHK027 Keyboard reachability unchanged

## Empty, loading & error states

- [x] CHK028 This *is* the empty-ish state, and it is deliberately low-pressure: it says what happened and nothing more. There is no "next step" to offer — the reader cannot recover the message, and inviting them to try would be dishonest
- [x] CHK029 The conversation's own loading and error states are untouched, which is the requirement: FR-009 exists precisely so that one unreadable message does **not** put the thread into its error state

## Feature-specific UI

- [x] CHK030 The unavailable bubble is visually identical to the deleted-message tombstone but carries a **different string**. This is the point of the item: "deleted" would be a false statement about what happened — nobody withdrew the message — and a blank bubble would tell the reader nothing at all. Asserted in `chat-conversation.component.spec.ts` (three specs: the placeholder renders, it does not say "deleted", and the messages either side stay readable)
- [x] CHK031 Only the affected message changes. The surrounding thread, its ordering, its sender labels and its read receipts are untouched — asserted both in the component spec and against the real API in `ChatMessageEncryptionTests`
- [x] CHK032 The inbox preview shows an empty last line for an unreadable message, identical to a deleted one. Recorded as a deliberate simplification in the plan, not an oversight: the row still carries its sender and timestamp, and opening the conversation is where the explanation belongs
- [x] CHK033 The legal paragraph reads as one document in all three languages, German authoritative. It states what is now true (text encrypted at rest, connection encrypted), states plainly that JuggerHub holds the key, and does **not** claim or imply end-to-end encryption
- [x] CHK034 The legal paragraph claims nothing about hops that lack protection — the realtime backplane (GH #219) is not covered by this feature and is not mentioned as if it were

## Notes

- **Inherited contrast concern (CHK025), not introduced here.** `opacity-80` on body text
  reduces effective contrast below the unmodified token pair. The new placeholder reuses the
  *existing* deleted-message treatment exactly, so this diff neither creates nor worsens the
  issue — diverging would have been the worse outcome, since two neutral bubble states that
  look different would read as two different kinds of event. This belongs with the standing
  app-wide DESIGN.md contrast question (the primary-button 3.14:1 conflict), which is the
  owner's call and out of scope here.
- No `.html` / `.css` / `.ts` separation concern: the change is two lines of template.
- No conflict with DESIGN.md was found that required resolving.
