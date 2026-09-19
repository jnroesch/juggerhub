# Contract: push endpoints and the delivered payload (055)

Three new endpoints, one changed DTO field, one payload shape that crosses to the device. Every
endpoint is authenticated like every other surface (feature 026); none is anonymous.

## `GET /api/v1/push/public-key`

Gives the client the application server key it needs to subscribe.

| Aspect | Contract |
|---|---|
| Auth | Signed in |
| `200` | `{ "publicKey": "<base64url uncompressed P-256 point>" }` |
| Notes | Per environment, never baked into the bundle (research R9). Not a secret; the private half never leaves the server |

## `POST /api/v1/push/subscriptions`

Registers the browser the caller is using.

```json
{
  "endpoint": "https://fcm.googleapis.com/fcm/send/…",
  "p256dh": "<base64url>",
  "auth": "<base64url>",
  "deviceLabel": "Chrome on Android"
}
```

| Aspect | Contract |
|---|---|
| Auth | Signed in. The owner is **the caller's own id from the token**, never a field in the body |
| `204` | Registered, or already registered — idempotent on `endpoint` |
| `400` | Missing or malformed fields, or an endpoint that is not an absolute `https` URL |
| Reassignment | An endpoint already stored for a **different** account is moved to the caller. That is the shared-device case, and the unique index is what makes it a move rather than a duplicate |
| Rate limit | Subject to the existing per-user policy; a `429` from us is never retried by the client |

## `DELETE /api/v1/push/subscriptions`

Removes the browser the caller is using. Body carries the endpoint, because the server cannot infer
which device is asking.

```json
{ "endpoint": "https://fcm.googleapis.com/fcm/send/…" }
```

| Aspect | Contract |
|---|---|
| Auth | Signed in |
| `204` | Removed, or nothing matched — idempotent, and it never reports whether a row existed |
| Scope | Deletes only a row belonging to the caller. An endpoint belonging to someone else is a no-op, not an error, and is never acknowledged as existing |

## Changed: the preference matrix

`GET /api/v1/notification-preferences` — `categories[].channels` gains `push`:

```json
{ "inApp": true, "email": true, "push": true }
```

`PUT /api/v1/notification-preferences/{category}/{channel}` — unchanged. `{channel}` now also
accepts `Push`; the route already binds the enum by name and rejects unknown values with `400`
(`NotificationPreferencesController.cs:42-51`). **No new endpoint.**

## The delivered payload

What the server encrypts and hands to the push service, and what the service worker receives. This
is the whole of it — FR-009 says nothing further travels.

```json
{
  "title": "Hamburg Hammers",
  "body": "Anna invited you to join",
  "url": "/t/hamburg-hammers",
  "tag": "invite:0199f3c2-…"
}
```

| Field | Rule |
|---|---|
| `title` | Localized to the **recipient's** stored language, never the actor's |
| `body` | Names what happened and its subject (owner decision). Never a news post's full text, never a chat message — chat is #309 |
| `url` | An **app-relative path**, always beginning `/`. Never absolute, never from user input. The worker refuses anything else |
| `tag` | The producer's dedupe key, or `type:subjectId`. Makes a second arrival replace the first rather than stack (research R5) |

The push message additionally carries `Topic` equal to `tag`, so a push service replaces a message
it still holds undelivered, and a `TimeToLive` short enough that a notification about a training
tomorrow does not arrive next week.

## Behaviour on failure

| Push service says | Meaning | What we do |
|---|---|---|
| `2xx` | Accepted | Touch `LastSuccessAt` |
| `404`, `410` | Subscription is gone | **Delete the row. Never retry.** |
| `429` | The **provider** is throttling us | Retry with backoff honouring `Retry-After`, via the shared pipeline. This is the opposite of our own limiter's `429`, which is never retried against |
| `5xx`, `408`, timeouts, connection failures | Transient | Shared pipeline: bounded attempts, jittered backoff, breaker |
| anything else | Rejection | Fail fast, log status code and subscription id. **Never the response body** (Principle VII) |

In every failure case the action that produced the notification still succeeds and the acting player
sees nothing (FR-013).

## Not in this contract

- No endpoint that lists a player's devices (FR-027).
- No endpoint that sends a test notification.
- No chat push (FR-026).
