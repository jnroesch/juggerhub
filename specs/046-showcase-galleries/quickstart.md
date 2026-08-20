# Quickstart & Validation: Showcase image galleries

**Feature**: 046-showcase-galleries | **Date**: 2026-08-20

Proves the feature end-to-end locally. This is a **validation guide**, not implementation — code
belongs in `tasks.md` and the implementation phase. Shapes are in
[data-model.md](./data-model.md) and [contracts/showcase-endpoints.md](./contracts/showcase-endpoints.md).

## Prerequisites

- Docker + docker-compose, `.env` populated from `.env.sample`. **No new environment variable is
  added** — the media store and the image processor are already configured (features 034/035).
- **Exactly one migration** for this feature, creating two new tables. If `dotnet ef migrations list`
  shows one that alters an existing table, stop and re-read [data-model.md](./data-model.md).
- Accounts needed:
  - **A** — a member with a **public** profile.
  - **B** — a member with a **private** profile.
  - **C** — an **admin** of a team; **D** — an ordinary **member** of that same team;
    **E** — a signed-in member who is **not** in that team.
  - A platform admin (to ban an account for the gating checks).
- Test images to have ready: a normal JPEG photo, a wide panorama, a tall portrait shot, a PDF
  renamed to `.jpg`, a >8 MB image, and a >40 MP image.

```powershell
docker compose up -d          # backend, frontend, database, azurite, mailpit
```

### Sanity check before any UI exists

```powershell
# As A — expect 200 and [] on a fresh gallery
curl "http://localhost:5000/api/v1/profiles/<A-handle>/showcase"

# Upload one — expect 201 and a ShowcaseImageDto
curl -X POST "http://localhost:5000/api/v1/profiles/me/showcase" `
     -H "Authorization: Bearer <A-token>" -F "file=@photo.jpg"

# Fetch the bytes — expect 200 image/webp, Cache-Control: private, no-cache, and an ETag
curl -i "http://localhost:5000/api/v1/profiles/<A-handle>/showcase/<id>/image"
```

---

## Validate the user stories

### US1 — A player showcases their best moments (P1)

Sign in as **A**, open your own profile.

1. **Add.** Add a picture. It appears without a reload, in the gallery, at the end.
   → *FR-001, SC-001*
2. **Fill it.** Add four more. All five show. The add control now says the gallery is full and is
   disabled. → *FR-001*
3. **Sixth is refused server-side.** With five stored, `POST …/me/showcase` again **from curl**
   (bypassing the disabled button) → `409` naming the limit; the gallery still holds five, in the
   same order. This is the check that matters — the disabled button is UX, not the boundary.
   → *FR-002, Principle I*
4. **Reorder.** Move the third picture to the front with the move-up control. Reload → the new order
   holds. Open the profile as **E** → same order. → *FR-006, FR-010*
5. **Caption.** Add a caption to one picture, clear it from another. Both persist; the cleared one
   renders with no caption and is not broken. → *FR-005, FR-009*
6. **Remove.** Delete the middle picture. It disappears; the remaining four are contiguous
   (positions 0–3 — check the listing, not just the screen). → *FR-011*
7. **The avatar is untouched.** A's identity picture is unchanged throughout, in the header, in
   chat, and in browse lists. → *FR-004*
8. **Empty for a viewer.** As **E**, open the profile of a member with no pictures → no gallery
   frame, no broken image, no empty card. As that member themselves → an invitation to add one.
   → *FR-026*

### US2 — A team shows what the team is like (P1)

1. **Admin adds.** As **C**, add three pictures to the team page. They appear. → *FR-008*
2. **Member can look, not touch.** As **D**, open the team page → the gallery renders; there is no
   add, reorder, caption, or remove control anywhere. Then, from curl with D's token,
   `POST /api/v1/teams/<slug>/showcase` → `403`. → *FR-008, US2 scenario 3*
3. **Signed-in outsider.** As **E**, open the team page → the same gallery, same order, no controls.
   → *FR-020*
4. **Independent caps.** With A's own gallery already at five, C fills the team's to five. Both
   succeed. → *FR-003, US2 scenario 6*
5. **Team deletion.** Create a throwaway team, add two pictures, delete the team. The rows are gone,
   and — the part that needs checking — **the objects are gone from the container too**:
   ```powershell
   # Azurite listing before and after; the two team-showcase/… objects must disappear
   az storage blob list --container-name media --prefix team-showcase/ --connection-string "<azurite>"
   ```
   → *FR-012, SC-010, research R7*

### US3 — Viewers can look at a picture properly (P2)

1. **Enlarge.** Click a thumbnail → the picture opens enlarged over the page with a visible close
   control. → *FR-027*
2. **Page through.** Next/previous move through the gallery in its order and stop at the ends
   without wrapping into emptiness. → *FR-027*
3. **Keyboard only.** Tab to a thumbnail, Enter to open, arrow keys to move, Escape to close —
   focus returns to the thumbnail you opened from. → *FR-027, SC-007*
4. **375 px.** In device emulation at 375 px wide, both the gallery and the enlarged view are usable
   with **no horizontal page scroll** and no clipped controls. → *FR-025, SC-007*
5. **Odd aspect ratios.** The panorama and the portrait shot: thumbnails stay a uniform grid; the
   enlarged view shows each whole picture rather than cropping the subject out. → *edge case*

### US4 — The showcase does not open a privacy hole (P1)

Run every one of these; each maps to a requirement that a passing UI cannot demonstrate.

1. **Private profile, signed out.** With **B** (private) holding pictures, in a private browser
   window: `GET /profiles/<B-handle>/showcase` → `404`; `GET …/showcase/<id>/image` → `404`.
   → *FR-018, SC-003*
2. **Private profile, signed in.** Same two calls as **E** → `200` for both. → *FR-018*
3. **Public profile, signed out.** Same two calls against **A** → `200` for both. → *FR-018*
4. **Ban.** Ban **A**. Immediately re-run the anonymous listing and image calls → both `404`, with
   no restart and no cache clear. Unban afterwards. → *FR-019, FR-021, SC-003*
5. **Public → private flips instantly.** Make **A** private. The very next anonymous image request
   → `404`. → *FR-021*
6. **Team gallery is never anonymous.** `GET /teams/<slug>/showcase` signed out → `401`.
   → *FR-020*
7. **Nothing discloses the store.** Inspect every response body **and header** from the calls above:
   no object key, no container name, no storage URL, no SAS. The `ETag` must be a 32-char hex hash,
   not a key. → *FR-022, SC-004*
8. **The container refuses direct reads.** Take an object key from the database and request it
   straight from Azurite anonymously → refused. → *US4 scenario 6*
9. **Not your gallery.** As **E**, `DELETE /profiles/me/showcase/<A's image id>` → `404`; A's
   gallery is unchanged. → *FR-007*

### US5 — A bad upload fails clearly and changes nothing (P2)

With a gallery holding two pictures, attempt each of these and confirm **after every one** that the
gallery still holds exactly those two, in the same order:

| Upload | Expected |
|---|---|
| PDF renamed `.jpg` | `400`, message about the file type — the decision is made on content, not on the claimed type |
| 12 MB photo | `400`/`413`, message about size |
| 45 MP image | `400`, message about size — refused **before** decode |
| Truncated JPEG | `400`, "we couldn't read that picture" |
| Zero-byte file | `400` |
| Sixth into a full gallery | `409`, names the five-picture limit — *distinguishable from every row above* |

→ *FR-015, FR-016, SC-006*

**Store outage.** Stop Azurite (`docker compose stop azurite`) and attempt an upload → the member is
told it could not be completed and can retry; **no row is written** (check the listing). Restart
Azurite and retry → succeeds. → *FR-015, US5 scenario 4*

**Missing object.** Delete one object directly from the container, leaving its row. Reload the
gallery → that one entry degrades to the no-picture placeholder and **the other four still render**.
→ *FR-024*

**Loading and error states.** Throttle the network to "slow 3G" and reload → a single muted loading
line (never a spinner, per DESIGN.md). Block the listing request → an error state with "Try again"
that actually retries. Neither may be an empty state. → *FR-026, DESIGN.md*

---

## Cross-cutting checks

### The reconciliation sweep must not eat live galleries ⚠

The single highest-consequence regression in this feature (research R6).

1. Store showcase pictures on **A** (public) and **B** (private), and on the team.
2. Ban **A** — their rows are now hidden by the query filter.
3. Set `MediaStorage:OrphanGraceMinutes` low and trigger the admin sweep.
4. **Every showcase object must survive**, A's included. If any disappeared, the sweep is missing
   `IgnoreQueryFilters()` or one of the two new tables.
5. Then upload, delete the *row* directly in the database, and sweep again → that object **is**
   reclaimed. The sweep still works; it just knows about the galleries now.

### Account deletion

Give **B** five showcase pictures, then delete B's account through the normal self-service flow.
Afterwards: the rows are gone, and the five objects are gone from the container. → *FR-012, SC-010*

### i18n parity

```powershell
cd frontend; npx nx test web        # the key-parity guard (feature 042) fails on any missing key
```

Then set the language to German and to Spanish and walk US1 and US5 — every new string is
translated, including the failure reasons. → *FR-030, SC-009*

### Request volume

With DevTools' network panel on a profile that has five pictures: **one** listing request and five
image requests on load. On a profile with none: the listing request and **zero** image requests.
→ *SC-008*

---

## Automated verification

```powershell
dotnet test backend/JuggerHub.slnx                  # incl. the new Profile/Teams/Media suites
cd frontend; npx nx lint web; npx nx test web; npx nx build web
```

Both must be green before the feature is considered done. Never report a run that was not made.
