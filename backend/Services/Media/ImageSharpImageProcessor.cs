using JuggerHub.Common;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace JuggerHub.Services.Media;

/// <summary>
/// ImageSharp-backed <see cref="IImageProcessor"/> (feature 034 / #98). Pure-managed, so
/// behavior is identical across local/Dev/Prod. Stateless — registered as a singleton.
/// </summary>
public sealed class ImageSharpImageProcessor : IImageProcessor
{
    private readonly ImageProcessingOptions _options;

    public ImageSharpImageProcessor(IOptions<ImageProcessingOptions> options) => _options = options.Value;

    /// <inheritdoc />
    public ImageProcessingResult Process(byte[] input, ImageProcessingProfile profile)
    {
        // 1. Empty.
        if (input.Length == 0)
        {
            return ImageProcessingResult.Fail(ImageProcessingStatus.Empty, "No image was provided.");
        }

        // 2. Generous input-size cap (defence-in-depth behind the endpoint's [RequestSizeLimit]).
        if (input.Length > _options.MaxInputBytes)
        {
            return ImageProcessingResult.Fail(
                ImageProcessingStatus.InputTooLarge,
                $"Image is too large (max {_options.MaxInputBytes / (1024 * 1024)} MB).");
        }

        // 3. Header-only guard BEFORE decoding pixels: identify format + dimensions.
        //    Image.Identify reads only the header, so a decompression bomb never allocates here.
        ImageInfo info;
        try
        {
            info = Image.Identify(input);
        }
        catch (ImageFormatException)
        {
            return ImageProcessingResult.Fail(ImageProcessingStatus.Unreadable, "That image could not be read.");
        }

        // Detected type must be in the allow-list (declared content type is never trusted).
        var detectedType = info.Metadata.DecodedImageFormat?.DefaultMimeType;
        if (detectedType is null || Array.IndexOf(_options.AllowedContentTypes, detectedType) < 0)
        {
            return ImageProcessingResult.Fail(ImageProcessingStatus.UnsupportedType, "Use a PNG, JPEG, or WebP image.");
        }

        // Decompression-bomb guard — reject before any pixel buffer is allocated.
        if ((long)info.Size.Width * info.Size.Height > _options.MaxDecodePixels)
        {
            return ImageProcessingResult.Fail(ImageProcessingStatus.DimensionsTooLarge, "Image resolution is too large.");
        }

        // 4. Decode + normalize. Any decode failure (truncated/corrupt) → Unreadable, never thrown out.
        try
        {
            using var image = Image.Load(input);

            // 4a. Flatten animation to the first frame (static output).
            while (image.Frames.Count > 1)
            {
                image.Frames.RemoveFrame(1);
            }

            // 4b. Bake EXIF orientation into the pixels, then strip ALL metadata (privacy).
            image.Mutate(x => x.AutoOrient());
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;
            image.Metadata.IccProfile = null;

            // 4c. Resize per profile; never upscale.
            ApplyResize(image, profile);

            // 5. Re-encode to WebP.
            using var output = new MemoryStream();
            image.Save(output, new WebpEncoder { Quality = profile.Quality });
            var bytes = output.ToArray();

            // 6. Stored-output ceiling — reject rather than store an oversized blob.
            if (bytes.Length > profile.MaxOutputBytes)
            {
                return ImageProcessingResult.Fail(
                    ImageProcessingStatus.OutputTooLarge, "Processed image exceeds the size limit.");
            }

            return ImageProcessingResult.Ok(bytes, "image/webp", image.Width, image.Height);
        }
        catch (ImageFormatException)
        {
            return ImageProcessingResult.Fail(ImageProcessingStatus.Unreadable, "That image could not be read.");
        }
    }

    /// <summary>Resize per the profile's mode, never upscaling an image smaller than the target.</summary>
    private static void ApplyResize(Image image, ImageProcessingProfile profile)
    {
        var max = profile.MaxDimension;

        if (profile.ResizeMode == ImageResizeMode.SquareCrop)
        {
            // Square side = min(MaxDimension, shorter source side) so a small source is
            // centre-cropped to a square but never enlarged.
            var side = Math.Min(max, Math.Min(image.Width, image.Height));
            if (side <= 0)
            {
                return;
            }

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(side, side),
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
            }));
        }
        else // Fit
        {
            // Only downscale when the largest side exceeds the max (avoids ResizeMode.Max upscaling).
            if (Math.Max(image.Width, image.Height) <= max)
            {
                return;
            }

            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(max, max),
                Mode = ResizeMode.Max,
            }));
        }
    }
}
