# Contract: Injected Tracker Snippet

**Feature**: `033-umami-analytics`

The exact content injected into `index.html` before `</head>`, carried in the `JH_ANALYTICS_HEAD` environment variable.

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
    s.src = "JH_ANALYTICS_SCRIPT_PATH";
    s.setAttribute("data-website-id", "JH_ANALYTICS_WEBSITE_ID");
    s.setAttribute("data-do-not-track", "true");
    s.setAttribute("data-exclude-search", "true");
    document.head.appendChild(s);
  })();
</script>
```

Empty when analytics is off. `sub_filter` then replaces `</head>` with `</head>` — a no-op.

### Double quotes are mandatory, not stylistic

**No `'` may appear anywhere in this value.** It is carried into `sub_filter '</head>' '<value></head>';`, whose argument is *single-quoted*, so one apostrophe terminates the string early and nginx refuses to start — taking the application down at the moment analytics is enabled, not when it is written.

This was found by rendering the config rather than by reading it (T013): the single-quoted form fails with `nginx: [emerg] unexpected "1"`, pointing at the DNT comparison rather than at the quoting. JavaScript treats `"` and `'` as equivalent; nginx does not.

---

## Why a guard rather than the attribute alone

`data-do-not-track="true"` is documented as respecting **Do Not Track only**. FR-007 requires Do Not Track **and** Global Privacy Control, and Umami has no attribute for GPC ([research.md](../research.md) §8).

Checking before injection — rather than letting the tracker load and decide — means a GPC or DNT user generates **no request at all**, not merely an ignored one. That is a stronger guarantee than the attribute gives, and it is the honest reading of "MUST NOT record anything".

`data-do-not-track` is kept as well: defence in depth costs nothing, and it keeps the behaviour correct if the guard is ever refactored away.

---

## Required properties

| Property | Requirement | Why |
|---|---|---|
| `async` + `defer` | Mandatory | FR-012: must not delay first render. |
| Never blocks | Mandatory | FR-011: a failed or slow load must be invisible. `appendChild` of an async script cannot block. |
| No retry | Mandatory | FR-013 / Principle VII: a dropped beacon is dropped. Retrying per-pageview measurement is amplification. |
| No `data-host-url` | Deliberate | Umami defaults to sending data where the script is hosted — our own origin. Setting it would duplicate a value that can then drift. |
| `data-exclude-search="true"` | **Mandatory** | Owner decision. Query strings are **not** recorded. Without it Umami stores `url_query` beside `url_path`, so `/sign-in` was recorded carrying `returnUrl`, which holds deep links such as `/players/<handle>`. FR-008 decided page *paths* are verbatim; query strings were never part of that decision and are not inherited from it. The tracker blanks `URL.search` **before sending**, so the value never leaves the browser — stronger than stripping it server-side. |
| No `identify()`, no `data-tag` | Mandatory | FR-005: nothing may link an event to a member. |
| Inline, not bundled | Deliberate | Keeps analytics out of Angular source and guarantees the guard runs before any tracker request. |

---

## SPA route changes

FR-001 requires in-app navigations to be recorded as distinct page views. Umami's tracker auto-tracks by default (`data-auto-track` defaults to on), but **the documentation does not state whether it hooks `history.pushState`**, and Angular's router navigates without a document load.

Treated as **unverified** — [research.md](../research.md) open item 2 — and confirmed against the real application in quickstart scenario 3. If auto-tracking does not follow the Angular router, the fallback is an explicit `umami.track()` call on `NavigationEnd`, which *would* mean touching Angular source and should be sized as such. Not assumed either way.

---

## Interaction with a future CSP

The snippet is an **inline script**. The application has no `Content-Security-Policy` header today, so nothing blocks it. If one is introduced later it must permit this inline block (via nonce or hash) and `connect-src` to the origin. Recorded in the spec as a forward dependency, not work in this feature.

---

## Values

| Placeholder | Source | Secret? |
|---|---|---|
| `JH_ANALYTICS_SCRIPT_PATH` | `/jh-insights.js`, matching `TRACKER_SCRIPT_NAME` | No |
| `JH_ANALYTICS_WEBSITE_ID` | Created by hand in the dashboard, per environment | **No** — it ships in page source. Belongs in `envs/*.tfvars`, not GitHub secrets. |

Website IDs do not exist until someone creates the website in the Umami dashboard, so there is an ordered bootstrap: deploy Umami → create the website → record the ID → apply the frontend configuration. Covered in quickstart.
