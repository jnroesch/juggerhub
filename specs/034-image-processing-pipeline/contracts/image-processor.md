# Contract: `IImageProcessor` (internal service seam)

Feature: `034-image-processing-pipeline`. This is the reusable seam (FR-011) that profiles use today and teams/galleries (#99) will reuse. Namespace: `JuggerHub.Services.Media`.

## Interface

```csharp
public interface IImageProcessor
{
    /// <summary>
    /// Validate, guard, normalize, and re-encode an uploaded image to WebP per the given
    /// profile. Never throws for bad input — decode/format failures are returned as a
    /// non-Success status with a non-technical Reason. Pure/stateless; safe as a singleton.
    /// </summary>
    ImageProcessingResult Process(byte[] input, ImageProcessingProfile profile);
}
```

## Behavioral contract

| # | Given | Then |
|---|---|---|
| C1 | Zero-byte input | `Status = Empty`, no bytes |
| C2 | Input encoded size > `MaxInputBytes` | `Status = InputTooLarge` |
| C3 | Decoded/identified format ∉ `AllowedContentTypes` | `Status = UnsupportedType` |
| C4 | Header dimensions with `w*h > MaxDecodePixels` | `Status = DimensionsTooLarge`, **before** any pixel allocation |
| C5 | Corrupt / truncated / undecodable bytes | `Status = Unreadable` (no throw) |
| C6 | Valid image larger than `MaxDimension` | Output's largest side ≤ `MaxDimension` (Fit) or a `MaxDimension`-sided square (SquareCrop) |
| C7 | Valid image smaller than `MaxDimension` | **Not upscaled**; Fit preserves dimensions, SquareCrop crops to a centered square ≤ shorter side |
| C8 | Any valid image | Output is WebP (`ContentType = image/webp`); no EXIF/IPTC/XMP/ICC metadata present |
| C9 | Image with EXIF orientation flag | Output pixels are display-upright; no orientation flag relied upon |
| C10 | Animated input (animated WebP / APNG) | Output is a single static frame |
| C11 | Source with alpha/transparency | Transparency preserved in output |
| C12 | Encoded WebP > `MaxOutputBytes` | `Status = OutputTooLarge` (reject, do not return oversized bytes) |
| C13 | Success | `Bytes` non-null WebP, `Width`/`Height` set, `Reason` null |

**Invariants**: never throws on input-quality problems (C5 especially); no secrets or internal detail in `Reason`; deterministic for the same input+profile+options.

## Consumer contract (`ProfileService.SetAvatarAsync`)

1. Reject empty/oversized before or via the processor (keeps existing `Empty`/`TooLarge` semantics).
2. Call `Process(bytes, options.Avatar)`.
3. On non-Success → map `ImageProcessingStatus` → `AvatarSetStatus` (see data-model §4) and return `AvatarSetResult.Fail(status, result.Reason)` **without** touching stored data (FR-009).
4. On Success → store `result.Bytes` + `result.ContentType` into `ProfileAvatar` exactly as today (INSERT or UPDATE), and **discard the original upload bytes** (FR-015).
