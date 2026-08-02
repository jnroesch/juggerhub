# Contract: Recorder Configuration (`website.replay_config`)

**Feature**: `038-umami-session-recording`

This is where FR-005, FR-006, FR-006a, and the sampling assumption are actually enforced.
Not in the page, not in nginx, not in Terraform — in a JSONB column in Umami's database,
which is **editable from the dashboard by anyone who can sign in, with no release and no
trace in git**.

That property is why this is a contract rather than a settings note.

---

## The endpoint

```text
GET /jh-insights/api/websites/{websiteId}/recorder
```

Public (`skipAuth: true`), cached `public, max-age=60, stale-while-revalidate=300`,
fetched by the recorder with `credentials: "omit"` before it captures anything.

**Required response for this feature:**

```json
{
  "enabled": true,
  "replayEnabled": true,
  "heatmapEnabled": false,
  "sampleRate": 1,
  "heatmapSampleRate": 0.15,
  "maskLevel": "moderate",
  "maxDuration": 300000,
  "blockSelector": ""
}
```

`sampleRate` is the exception: it is whatever the dashboard says for that environment (`1`
in Dev today, deliberately lower in Prod). Every other value above is required.

Asserting this response is the verification for the masking and sampling requirements
(quickstart scenario 3). It is checked against the **running system**, not against the seed
file, because the seed file is not what the browser obeys.

---

## Seeded values

Extends `scripts/umami-seed-website.sql` (033), which already creates the website row.

| Column / key | Value | Requirement |
|---|---|---|
| `website.recorder_enabled` | `true` | Master switch; `false` ⇒ `{"enabled": false}` and the recorder stops immediately |
| `replayEnabled` | `true` | FR-001 |
| `heatmapEnabled` | **`false`** | Out of scope — a separate capture mechanism writing to `heatmap_event`, with its own disclosure obligation |
| `sampleRate` | dashboard-set | **Umami default is `0.15`.** Terraform never writes it; a new website records 15% until someone changes it in website settings |
| `maskLevel` | `"moderate"` | FR-006 / FR-006a — the owner's decision |
| `maxDuration` | `300000` | 5 minutes; bounds both storage and the long-session edge case |
| `blockSelector` | `""` | Unused. The FR-006a escape hatch |

`getRecorderConfig` discards unknown keys and silently falls back to `moderate` for an
invalid `maskLevel` — so a typo in this seed does not fail loudly, it just leaves the
default in place. **Verify the endpoint, don't trust the seed.** That matters most for
`sampleRate`, which nothing in this repository writes: if nobody set it in the dashboard,
the environment is recording 15% of sessions and looks fine.

---

## `maskLevel` — the only two values that exist

`'strict' | 'moderate'`, resolved at record time to rrweb options:

| Level | rrweb options | Input values | Rendered text |
|---|---|---|---|
| `moderate` | `{ maskAllInputs: true }` | masked | **captured** |
| `strict` | `{ maskAllInputs: true, maskTextSelector: "*" }` | masked | masked |

**`maskAllInputs: true` at both levels.** FR-005 (passwords) and FR-006 (every input value,
masked in the browser before transmission) cannot be switched off through this surface at
all — a stronger guarantee than the spec assumed it was buying, and worth knowing when
someone later asks whether a config change could have leaked a password. It could not.

**`moderate` is the owner's decision (FR-006a)**, and its consequence is that chat message
history rendered on screen is captured. The two reversals, should that be reconsidered:

- `"maskLevel": "strict"` — masks all rendered text everywhere.
- `"blockSelector": "<selector>"` — excludes matching regions at record time, so they never
  enter the payload at all.

Both are one field in one JSONB column. Neither needs a release, a migration, or a code
change. That is the mitigation the spec relies on when it accepts the exposure.

---

## What the deploy owns, and what the dashboard owns

**Every one of these settings has a control in Umami's website settings**, and an operator
has to be able to reach any of them during an incident without waiting for a deploy. So
Terraform **seeds a new website and then gets out of the way**.

The rule: **a deploy may narrow what is captured, or leave it alone. It may never widen it.**

| Key | Written | Why |
|---|---|---|
| `replayEnabled` / `recorder_enabled` | **creation only** | The dashboard is the kill switch. Replay turned off at 02:00 must not come back with the next deploy. |
| `sampleRate` | **never written** | A dashboard slider. Terraform has no say: a fresh website starts on Umami's `0.15` and whoever sets the environment up chooses the real rate there. |
| `maskLevel` | **creation only** | Only two values exist and `moderate` is the *weaker* one, so an operator can only ever tighten it. Re-asserting could only undo a tightening. |
| `blockSelector` | **creation only** | A selector added in a hurry to exclude a screen must not vanish on the next deploy. |
| `maxDuration` | **creation only** | Same reasoning: lowering it narrows capture. |
| `heatmapEnabled` | **every deploy**, forced `false` | The **one** setting whose drift would *widen* capture. Heatmaps write to `heatmap_event`, are disclosed nowhere in the privacy policy, and are not swept by the retention job — so a stray toggle accumulates undisclosed data that never expires. Enabling them needs a policy change first. |

**Operational consequences, worth knowing before the next apply:**

- "Creation only" means `replay_config IS NULL`. Dev's row is already configured, so the
  seed is already a no-op there. Prod's has never been configured, so the next deploy there
  seeds `replayEnabled: true` with `moderate` masking — **and Prod will record 15% of
  sessions until someone sets the rate in the dashboard.**
- The **only** recording value Terraform still owns is the retention period, because Umami
  has no setting for it and the privacy policy publishes the number.

Verification does not rely on any of this: quickstart asserts the **live endpoint**, so a
setting that drifted in a direction nobody intended fails a check rather than going
unnoticed. The response is also the acceptance evidence for SC-003 — a masking claim in the
policy is only as true as this JSON.

No attempt is made to lock the dashboard out of the column. That would fight the product,
and the owner needs to be able to switch masking to `strict`, or recording off entirely, in
a hurry and without a deploy.

---

## Environment differences

Per constitution V, the same resources everywhere; only values differ.

| | local | Dev | Prod |
|---|---|---|---|
| recording | on with the `analytics` profile (opt-in) | on | on |
| `sampleRate` | dashboard | dashboard (`1` today) | dashboard — **set it after the first deploy** |
| `maskLevel` and everything else | identical | identical | identical |

**Masking is identical in all three environments.** A weaker setting anywhere would mean
the thing verified is not the thing shipped — and Dev carries real member data.

Sampling is the one deliberate difference, and it is set per environment in the dashboard
rather than in deployed configuration: in Prod the volume is real, and recording a fraction
is data minimisation as much as cost control. Nothing enforces that automatically, so it is
a step in the Prod rollout, not a default.
