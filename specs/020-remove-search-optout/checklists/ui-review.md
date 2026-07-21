# UI Review Checklist: Remove the Player-Search Opt-Out

**Purpose**: Verify implemented UI complies with [DESIGN.md](../../../DESIGN.md) before the feature is done.
**Created**: 2026-07-21
**Feature**: [spec.md](../spec.md)

**Scope note**: This feature is a **UI removal**, not new UI. It deletes the
"Discovery / Appear in search" section (toggle + status text) from the owner-profile
edit view and updates one static note on the Players browse page. No new components,
colors, tokens, typography, motion, or icons are introduced — so the standing
DESIGN.md compliance items below are **N/A (nothing added)**. The review focuses on
the two removal-specific checks.

## Standing DESIGN.md items

- [x] N/A — CHK001–CHK029: no new UI is introduced (removal only). No tokens, colors, type, shape, motion, icons, or states added or changed. Existing surrounding components are untouched.

## Feature-specific UI

- [x] CHK030 The Discovery section (Appear-in-search toggle + "You appear / are hidden…" status text) is fully removed from the owner-profile edit view — verified [profile-owner.component.html](../../../frontend/apps/web/src/app/features/profile/profile-owner/profile-owner.component.html); no toggle, `role="switch"`, or `data-testid="profile-appear-in-search"` remains.
- [x] CHK031 No orphaned divider or empty section remains: the two `<hr>` dividers that had bracketed the Discovery section collapsed to a single `<hr class="my-lg border-border" />` between the "Plays" and "Badges & achievements" sections — section rhythm (`my-lg`) preserved.
- [x] CHK032 The Players browse note reflects the new behavior ("Every player on JuggerHub is listed here.") instead of the retired opt-in caveat — [browse-players.component.html](../../../frontend/apps/web/src/app/features/browse/browse-players/browse-players.component.html); copy is sentence-case and addresses the reader in the DESIGN.md voice.

## Notes

- Removal verified against the diff; the owner-profile edit view keeps its existing
  card, spacing, and divider system unchanged apart from the deleted section.
- No DESIGN.md conflicts encountered.
