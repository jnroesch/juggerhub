# Phase 1 Data Model: Server-Side Image Processing Pipeline

Feature: `034-image-processing-pipeline` · Spec: [spec.md](./spec.md) · Research: [research.md](./research.md)

> **No database schema change.** The `ProfileAvatar` entity (`bytea Bytes`, `ContentType`) and its migration are untouched — only the *content* written to it changes (a normalized WebP instead of raw upload bytes). The "entities" below are in-memory configuration and result shapes, not persisted tables.

---

## 1. `ImageProcessingProfile` (per-context settings)

The configurable knobs for one upload context (spec: *Processing Profile / Constraints*). Named profiles let avatars and the future gallery (#99) share one processor with different limits (FR-011).

| Field | Type | Default (Avatar) | Meaning / FR |
|---|---|---|---|
| `ResizeMode` | enum `{ Fit, SquareCrop }` | `SquareCrop` | FR-006, Clarifications — avatar crops to square; gallery will use `Fit` |
| `MaxDimension` | int (px) | `512` | Largest output side; never upscales (FR-006) |
| `Quality` | int (1–100) | `80` | WebP encode quality (FR-007) |
| `MaxOutputBytes` | int | `512 * 1024` | Stored-output ceiling; over → reject (FR-008) |

## 2. `ImageProcessingOptions` (bound from config, safe defaults)

Bound from an optional `ImageProcessing` config section (`Configure<ImageProcessingOptions>`). All values have safe built-in defaults so the feature runs with **zero configuration** (FR-013, Principle V).

| Field | Type | Default | Meaning / FR |
|---|---|---|---|
| `MaxInputBytes` | int | `8 * 1024 * 1024` | Input acceptance cap; kept generous (FR-012) |
| `MaxDecodePixels` | long | `40_000_000` (~40 MP) | Decompression-bomb guard, checked pre-decode (FR-004) |
| `AllowedContentTypes` | string[] | `["image/png","image/jpeg","image/webp"]` | Input allow-list (FR-002) |
| `Avatar` | `ImageProcessingProfile` | see §1 | The avatar context profile |
| *(Gallery)* | `ImageProcessingProfile` | — | **Added by #99**, not this feature; shape already supports it |

**Const section name**: `ImageProcessing`. No secrets (Principle V / Secret Management).

## 3. `ImageProcessingResult` + `ImageProcessingStatus` (processor output)

Returned by `IImageProcessor.Process(...)`. Internal type — never serialized to the client directly.

```
enum ImageProcessingStatus { Success, Empty, UnsupportedType, InputTooLarge, DimensionsTooLarge, Unreadable, OutputTooLarge }

record ImageProcessingResult(
    ImageProcessingStatus Status,
    byte[]? Bytes,          // set only on Success — the normalized WebP
    string? ContentType,    // "image/webp" on Success
    int Width, int Height,  // output dimensions on Success
    string? Reason)         // non-technical message on failure (FR-003)
```

| Status | Cause | FR |
|---|---|---|
| `Success` | Processed OK | FR-001 |
| `Empty` | Zero-byte input | Edge case |
| `UnsupportedType` | Sniffed/decoded type not in allow-list | FR-002/003 |
| `InputTooLarge` | Encoded input > `MaxInputBytes` | FR-012 |
| `DimensionsTooLarge` | `w*h > MaxDecodePixels` (pre-decode) | FR-004 |
| `Unreadable` | Corrupt/truncated/undecodable | FR-003 |
| `OutputTooLarge` | Encoded WebP > `MaxOutputBytes` | FR-008 |

## 4. `AvatarSetStatus` (existing enum — extended)

Add two members so avatar failures stay distinct and testable (FR-003, D8). The processor's failure statuses map onto these:

| `AvatarSetStatus` | From `ImageProcessingStatus` |
|---|---|
| `Success` | `Success` |
| `Empty` | `Empty` |
| `InvalidType` | `UnsupportedType` |
| `TooLarge` | `InputTooLarge` **and** `OutputTooLarge` (both size failures; `Reason` differentiates) |
| **`DimensionsTooLarge`** *(new)* | `DimensionsTooLarge` |
| **`Unreadable`** *(new)* | `Unreadable` |
| `ProfileNotFound` | (unchanged — profile lookup, not processing) |

Controller mapping is unchanged: `ProfileNotFound → 404`, every other non-success → `400` with `Reason` (so the two new members need no new controller branch).

## 5. Unchanged (for reference)

- **`ProfileAvatar`** entity, `ProfileAvatars` table, 1:1 mapping, ban/visibility query filter — untouched.
- **`GetAvatarAsync`** — unchanged (serves whatever bytes/type are stored; now WebP).
- **Endpoints** `PUT /profiles/me/avatar`, `GET /profiles/{handle}/avatar` — unchanged shape (FR-014).

## Processing sequence (per upload)

```
input bytes
  → empty check                         (Empty)
  → size check vs MaxInputBytes         (InputTooLarge)
  → Identify header: format + w,h
      → format ∉ allow-list             (UnsupportedType)
      → w*h > MaxDecodePixels           (DimensionsTooLarge)   ← guard BEFORE decode (FR-004)
  → Load (decode)                       (Unreadable on failure)
  → flatten animation to first frame                            (FR-017)
  → AutoOrient + clear EXIF/IPTC/XMP/ICC                        (FR-005)
  → resize per profile (Fit | SquareCrop), no upscale           (FR-006)
  → encode WebP @ Quality                                       (FR-007)
  → output > MaxOutputBytes → reject    (OutputTooLarge)        (FR-008)
  → Success(bytes, "image/webp", w, h)
```
