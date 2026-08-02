# Contract: Injected Recorder Snippet

**Feature**: `038-umami-session-recording`

Extends [033's tracker-snippet contract](../../033-umami-analytics/contracts/tracker-snippet.md).
The recorder is appended **inside the existing guard**, in the same `JH_ANALYTICS_HEAD`
value — not as a second snippet, and never as the raw script tag the dashboard produces.

---

## Shape

```html
<script>
  (function () {
    var n = navigator;
    if (n.globalPrivacyControl || n.doNotTrack === "1" || window.doNotTrack === "1") return;
    var s = document.createElement("script");
    s.async = true;
    s.defer = true;
    s.src = "/jh-insights.js";
    s.setAttribute("data-website-id", "JH_ANALYTICS_WEBSITE_ID");
    s.setAttribute("data-do-not-track", "true");
    s.setAttribute("data-exclude-search", "true");
    document.head.appendChild(s);
    /* recording (038) — appended only when enabled for this environment */
    var r = document.createElement("script");
    r.async = true;
    r.src = "/jh-insights/r.js";
    r.setAttribute("data-website-id", "JH_ANALYTICS_WEBSITE_ID");
    document.head.appendChild(r);
  })();
</script>
```

The recording half is present only when recording is enabled for the environment; when it
is off, the value is byte-for-byte 033's snippet. When analytics itself is off the whole
value is empty and the `sub_filter` is a no-op — unchanged.

---

## Why inside the existing guard, not a second script tag

This is the difference between "the recorder ignores an objecting visitor" and "an
objecting visitor never contacts us".

| | Raw tag as supplied | Inside the guard |
|---|---|---|
| GPC / DNT visitor | Loads 190 KB of recorder, fetches the config endpoint, then stops | **No request at all** |
| Origin | `analytics-dev.juggerhub.com` — blocklist-matched, cross-origin | Same-origin, unmatched |
| Environment | Dev host + Dev website id baked into every build | Substituted at container start |

The recorder has **no DNT or GPC handling of its own** — the strings do not appear in it,
and unlike the tracker it has no `data-do-not-track` attribute to set. The guard is
therefore not defence in depth here; **it is the entire mechanism** for FR-009 and FR-010.
Moving the recorder outside it silently removes the only objection route the privacy policy
offers.

---

## Required properties

| Property | Requirement | Why |
|---|---|---|
| Inside the DNT/GPC guard | **Mandatory** | FR-009/FR-010. The recorder honours neither signal on its own. |
| `async` | Mandatory | FR-028: must not delay first render. |
| No `data-host-url` | **Mandatory** | The endpoint base must come from the script's own directory ([nginx-routes.md](./nginx-routes.md)). Setting it would both duplicate a value that can drift and defeat the path design. |
| Appended after the tracker | Mandatory | The recorder waits for `window.umami.getSession().cache` — it cannot start without the tracker's server-issued session. |
| No `defer` on the recorder | Deliberate | `defer` has no effect on a dynamically inserted script; 033 sets both on the tracker, and copying that here would imply a guarantee the property does not give. |
| Double quotes only | **Mandatory** | Inherited and unchanged: the value is carried into nginx's *single-quoted* `sub_filter` argument, so one apostrophe stops nginx from starting — taking the app down the moment analytics is enabled. |
| Same `data-website-id` as the tracker | Mandatory | One value, `JH_ANALYTICS_WEBSITE_ID`, used by the tracker, the recorder, and the nginx config location. |

---

## Ordering and the session dependency

The recorder polls for the tracker's session cache — 50 attempts at 100 ms, then it gives
up permanently. Both scripts are `async`, so their execution order is not guaranteed, and
that is fine: whichever lands first, the recorder waits up to 5 seconds for the tracker to
establish a session.

The failure mode worth knowing: if the **tracker** is blocked or fails while the recorder
loads, the recorder waits 5 seconds and silently stops. Recording is therefore strictly
dependent on measurement — which is also why FR-024's "turning analytics off also stops
recording" holds for free, without extra configuration.

---

## What the dashboard-generated tag would have done

Recorded because it is what was asked for, and the difference is not cosmetic:

```html
<script defer src="https://analytics-dev.juggerhub.com/recorder.js"
        data-website-id="b7e4d21c-0a53-4f18-9c62-3d5a81f0e447"></script>
```

- Outside the guard ⇒ GPC and DNT visitors recorded (breaks FR-009, and the policy's only
  objection promise).
- Cross-origin to a host containing `analytics` ⇒ blocked for a large share of visitors
  (033 FR-016), so the data would also be quietly incomplete.
- Dev hostname **and** Dev website id fixed at build time ⇒ a Prod build reporting into
  Dev's dataset (033 FR-018, FR-020).
- In Angular source or `index.html` ⇒ 033's decision to keep analytics out of the bundle
  reversed.

Same two inputs, four broken requirements.
