# Phase 0 Research: Server-Side Image Processing Pipeline

Feature: `034-image-processing-pipeline` · Spec: [spec.md](./spec.md)

All spec inputs were resolved during `/speckit-clarify`; there are no open `NEEDS CLARIFICATION` markers. This document records the **technical** decisions the plan depends on.

---

## D1 — Imaging library

**Decision**: **SixLabors.ImageSharp** (pinned to its current major version), used behind a project-owned `IImageProcessor` interface.

**Rationale**:
- **Pure-managed, no native dependency** → identical behavior on local compose, Dev, and Prod with nothing to install in the Docker image and no per-arch binary. This directly serves constitution Principle V (environment parity) — the deciding factor.
- First-class API for every step this feature needs: `Image.Identify` (header-only dimension read for the bomb guard), `AutoOrient()`, metadata profile clearing, `ResizeOptions` with `Max`/`Crop` modes, `WebpEncoder` with a quality setting, animated decode with frame access, alpha preserved through WebP.
- Actively maintained, .NET 10 compatible.

**Alternatives considered**:
- **SkiaSharp** (BSD/MIT): no licensing threshold, but ships a **native binary per architecture** — a real parity/packaging cost in the Alpine/AKS image, and lower-level EXIF/orientation handling. Kept as the documented fallback if the licensing note below rules out ImageSharp; it slots behind the same `IImageProcessor` seam.
- **Magick.NET** (ImageMagick): most capable, but a large native footprint and heavier than needed for resize/strip/re-encode. Rejected as overkill.
- **System.Drawing.Common**: not cross-platform supported on non-Windows since .NET 6. Rejected.

**Licensing (owner decision, non-blocking)**: ImageSharp uses the **Six Labors Split License** — free for OSS and for organizations under the annual gross-revenue threshold; commercial license required above it. Because the library sits behind `IImageProcessor`, switching to SkiaSharp later touches only `ImageSharpImageProcessor.cs`. Surfaced to the owner at plan hand-off.

---

## D2 — Decompression-bomb / pixel guard (FR-004, mandatory, *before* rasterization)

**Decision**: Read image dimensions from the header only via `Image.Identify(...)` (which does **not** allocate the pixel buffer), and reject when `width * height > MaxDecodePixels` **before** calling `Image.Load`. Also cap the accepted encoded input size (D7) and rely on ImageSharp's `MemoryAllocator` allocation ceiling as defense-in-depth.

**Rationale**: `Identify` is cheap and returns `ImageInfo` (dimensions, format) without decoding, which is exactly the "guard before full rasterization" the spec mandates. A default of ~**40 megapixels** bounds worst-case decode memory to a safe envelope while comfortably accepting any real phone photo. Value is configurable (safe default).

**Alternatives**: Decode-then-check (rejected — the bomb has already exploded in memory); trusting a byte-size cap alone (rejected — a few-KB file can encode an enormous pixel grid).

---

## D3 — Orientation + metadata stripping (FR-005)

**Decision**: After load, `image.Mutate(x => x.AutoOrient())` to bake in EXIF orientation, then null out **all** metadata profiles (`Metadata.ExifProfile`, `IptcProfile`, `XmpProfile`, `IccProfile`) before encode. The `WebpEncoder` does not re-embed EXIF.

**Rationale**: Baking orientation first guarantees the stored pixels are display-upright and no orientation flag is relied upon afterward. Clearing every profile removes GPS/camera/color metadata (privacy — FR-005, SC-002/SC-003).

**Note**: Dropping the ICC profile can slightly shift colors for wide-gamut sources; acceptable for avatar/gallery use and consistent with the spec's explicit "strip ICC". Documented so it is a decision, not an accident.

---

## D4 — Resize modes + no-upscale (FR-006)

**Decision**: Per-context resize mode.
- **Fit** (future gallery): `ResizeOptions { Mode = ResizeMode.Max, Size = (maxDim, maxDim) }`, applied **only if** `max(width, height) > maxDim` (guarding against `Max` upscaling a smaller source).
- **Square-crop** (avatar): `ResizeOptions { Mode = ResizeMode.Crop, Position = AnchorPositionMode.Center, Size = (side, side) }` where `side = min(maxDim, min(width, height))` so a source smaller than `maxDim` is cropped to a centered square but **never upscaled**.

**Rationale**: Encodes the Clarifications decision (avatar = square-crop, gallery = fit) with an explicit no-upscale rule (FR-006). Center anchor is the least-surprising crop for faces/subjects.

**Alternatives**: Always-fit (rejected — clarified avatars want square); stretch-to-fill (rejected — distorts, violates edge-case rule).

---

## D5 — Output format + quality (FR-007)

**Decision**: Always encode to **WebP** via `WebpEncoder { Quality = <configured> }`, default quality **80**. The stored `ContentType` becomes `image/webp`.

**Rationale**: WebP gives strong compression with alpha support (FR-016), matching the spec's normalized-output contract. Quality 80 is the standard quality/size sweet spot; configurable per profile.

**Consequence (test drift)**: the existing integration test `Avatar_upload_accepts_a_valid_png` asserts the served type is `image/png`; after re-encode it is `image/webp`. That assertion must change (captured in the plan and tasks).

---

## D6 — Animated inputs (FR-017)

**Decision**: Accept animated WebP / (A)PNG; flatten to a single still by keeping the first frame (`while (image.Frames.Count > 1) image.Frames.RemoveFrame(1);`) before resize/encode. Output is always a static WebP.

**Rationale**: Matches the Clarifications answer (accept + first frame). Keeps uploads forgiving without introducing animation handling.

**Note**: If a given APNG cannot be multi-frame-decoded by the PNG decoder, the base frame is used — still a valid still result.

---

## D7 — Input cap vs output ceiling (FR-008, FR-012)

**Decision**: Two independent, separately-configured limits:
- **Input acceptance cap** — kept generous. The endpoint keeps `[RequestSizeLimit(8 MB)]` (hard 413 at the framework), and `ProfileOptions.MaxAvatarBytes` is **reframed as the input cap** and raised to align (~8 MB), returning a clear `TooLarge` reason.
- **Stored-output ceiling** — `ImageProcessingOptions` `MaxOutputBytes` (default ~512 KB). After encode, if the WebP still exceeds the ceiling, **reject** with a clear reason rather than store an oversized blob (FR-008).

**Rationale**: Lets the platform accept a big phone photo while guaranteeing a small stored result. Because we downscale to a modest max dimension at quality 80, the WebP is normally far under the ceiling, so rejection is a rare safety net. Fixed quality (not iterative re-encode) keeps the pipeline simple and matches the spec's "fixed configurable quality."

**Alternatives**: Iterative quality reduction to always fit under the ceiling (rejected for v1 — more complexity than warranted; noted as a possible future refinement).

---

## D8 — Distinct rejection reasons (FR-003)

**Decision**: Extend `AvatarSetStatus` with the missing categories so failures are distinct and testable: add `Unreadable` (corrupt/undecodable) and `DimensionsTooLarge` (pixel guard) alongside existing `InvalidType`, `TooLarge`, `Empty`, `ProfileNotFound`. Each carries a non-technical `Reason` string. The controller already maps any non-success (except `ProfileNotFound` → 404) to `400 + Reason`, so **no controller branching change** is required.

**Rationale**: Better UX (spec Clarifications) with minimal surface change; the enum members also give tests and telemetry stable categories.

---

## D9 — Service shape, DI lifetime, reusability (FR-011)

**Decision**: `IImageProcessor.Process(ReadOnlySpan<byte>/byte[] input, ImageProcessingProfile profile) → ImageProcessingResult`. Register as a **singleton** (stateless; ImageSharp `Configuration` is thread-safe). `ImageProcessingOptions` bound via `Configure<>` from an `ImageProcessing` config section with safe defaults. The processor is owner-agnostic; `ProfileService` selects the **Avatar** profile.

**Rationale**: A stateless singleton avoids per-request allocation of the processor; the profile argument (not a hard-coded avatar config) is what makes it reusable by #99's gallery context. Options-with-defaults satisfies Principle V (FR-013).

---

## D10 — Test strategy (no binary fixtures)

**Decision**: Unit tests construct inputs **in memory with ImageSharp** — e.g. build an `Image<Rgba32>`, attach an `ExifProfile` with a GPS tag, encode to JPEG bytes, feed it in, then assert the output has no `ExifProfile`, is WebP, and is within bounds. Bomb test uses a tiny image whose header declares huge dimensions (or a real large-dimension encode) to trip the `Identify` guard. Integration tests extend `ProfileTests` against the real stack.

**Rationale**: Synthetic fixtures keep the repo free of committed binary blobs, make intent explicit, and are deterministic. Integration coverage proves the end-to-end swap (upload → processed → served WebP).

---

## Resolved unknowns summary

| Topic | Resolution |
|---|---|
| Library | ImageSharp (managed), behind `IImageProcessor`; SkiaSharp fallback |
| Bomb guard | `Image.Identify` header read + `MaxDecodePixels` (~40 MP) before decode |
| Metadata/orientation | `AutoOrient()` then clear EXIF/IPTC/XMP/ICC |
| Resize | Fit (`Max`) / square-crop (`Crop` center); never upscale |
| Output | WebP, quality 80 (configurable); `ContentType = image/webp` |
| Animated | Accept, keep first frame, static output |
| Caps | Input ~8 MB (kept generous) vs output ceiling ~512 KB (reject if over) |
| Reasons | Extend `AvatarSetStatus` (+`Unreadable`, +`DimensionsTooLarge`) |
| DI | Singleton `IImageProcessor`; `ImageProcessingOptions` with safe defaults |
| Tests | In-memory synthetic images; extend integration `ProfileTests` |
| Migration | **None** — schema unchanged |
