# Quickstart & Validation: Server-Side Image Processing Pipeline

Feature: `034-image-processing-pipeline` · Spec: [spec.md](./spec.md) · Plan: [plan.md](./plan.md)

How to verify the pipeline works end-to-end. Implementation details live in `tasks.md`; this is a run/validation guide.

## Prerequisites

- .NET 10 SDK; Docker running (integration tests use Testcontainers Postgres/Redis).
- Backend builds clean: `dotnet build backend` (repo uses `TreatWarningsAsErrors=true`).
- `SixLabors.ImageSharp` added to `backend/JuggerHub.Api.csproj` (see plan / research D1).

## Automated validation (primary)

Run the backend test suite:

```bash
dotnet test backend/tests/JuggerHub.Api.IntegrationTests
```

Expected coverage once tasks are implemented:

**Unit — `Media/ImageProcessorTests.cs`** (synthetic in-memory images, research D10). Maps to `contracts/image-processor.md` cases C1–C13:
- Large image (e.g. 3000×2000) → output WebP, largest side ≤ `MaxDimension`, bytes ≥ 90% smaller (SC-001).
- Small image → not upscaled (Fit preserves dims; SquareCrop centers) (C7).
- JPEG carrying an EXIF **GPS** tag → output has no `ExifProfile`/metadata (SC-002, C8).
- Image with EXIF orientation → output pixels upright (SC-003, C9).
- Animated WebP → single static frame (C10).
- Image with alpha → transparency preserved (C11).
- Header declaring huge dimensions → `DimensionsTooLarge` with no memory spike (SC-004, C4).
- Truncated/corrupt bytes → `Unreadable`, no throw (C5).
- Non-image bytes → `UnsupportedType` (C3).
- Over-`MaxInputBytes` → `InputTooLarge`; over-`MaxOutputBytes` after encode → `OutputTooLarge` (C2, C12).

**Integration — `Profile/ProfileTests.cs`** (real stack):
- `Avatar_upload_accepts_a_valid_png` → **served `Content-Type` is now `image/webp`** (updated assertion — the one existing test that changes).
- Upload a large PNG → GET avatar returns a materially smaller WebP.
- Upload JPEG with GPS EXIF → served bytes contain no EXIF/GPS.
- Rejected upload (corrupt) after a valid one already set → the prior avatar is unchanged (SC-005, FR-009).

## Manual smoke test (optional)

With the stack up (`docker compose up`) and an authenticated session:

```bash
# Upload a large phone photo as your avatar
curl -sS -X PUT https://localhost/api/v1/profiles/me/avatar \
  -H "Authorization: Bearer <token>" \
  -F "file=@big-phone-photo.jpg" -i          # expect 204

# Fetch it back; confirm it came back as WebP and small
curl -sS https://localhost/api/v1/profiles/<your-handle>/avatar -D - -o /tmp/av.webp
#   → Content-Type: image/webp ; /tmp/av.webp is a few tens of KB, not MBs

# Confirm no location metadata leaked (should print nothing / no GPS)
exiftool /tmp/av.webp | grep -i gps || echo "no GPS — good"
```

Rejection checks (each returns `400` with a distinct `detail`, prior avatar untouched):

```bash
curl -X PUT .../me/avatar -F "file=@notanimage.txt;type=image/png"   # → "Use a PNG, JPEG, or WebP image."
curl -X PUT .../me/avatar -F "file=@truncated.jpg"                    # → "That image could not be read."
curl -X PUT .../me/avatar -F "file=@decompression-bomb.png"          # → "Image resolution is too large."
```

## Success criteria mapping

| Spec SC | Validated by |
|---|---|
| SC-001 (≥90% smaller, ≤ max dim) | Unit large-image test; manual fetch size |
| SC-002 (no EXIF/GPS/ICC) | Unit GPS test; manual `exiftool` |
| SC-003 (orientation baked) | Unit orientation test |
| SC-004 (bomb rejected, no memory spike) | Unit dimensions-guard test |
| SC-005 (rejection leaves existing intact) | Integration prior-avatar-unchanged test |
| SC-006 (< 1 s per image) | Unit timing (informal) / observed |
| SC-007 (identical local/Dev/Prod) | Pure-managed lib + options with defaults (no env-specific behavior) |
