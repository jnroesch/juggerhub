using JuggerHub.Common;
using JuggerHub.Services.Media;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace JuggerHub.Api.IntegrationTests.Media;

/// <summary>
/// Unit tests for the server-side image processing pipeline (feature 034 / #98). Pure — no
/// API host or database; inputs are synthesized in-memory with ImageSharp (research D10), so
/// these run without Docker/Testcontainers. Covers contracts/image-processor.md cases C1–C13.
/// </summary>
public sealed class ImageProcessorTests
{
    private static ImageSharpImageProcessor NewProcessor(ImageProcessingOptions? options = null) =>
        new(Options.Create(options ?? new ImageProcessingOptions()));

    private static ImageProcessingProfile Fit(int maxDim = 512, int maxOut = 512 * 1024) =>
        new() { ResizeMode = ImageResizeMode.Fit, MaxDimension = maxDim, Quality = 80, MaxOutputBytes = maxOut };

    private static ImageProcessingProfile SquareCrop(int maxDim = 512) =>
        new() { ResizeMode = ImageResizeMode.SquareCrop, MaxDimension = maxDim, Quality = 80, MaxOutputBytes = 512 * 1024 };

    // --- US1: normalization ---------------------------------------------------

    [Fact]
    public void Large_image_is_downscaled_to_bounds_and_reencoded_smaller()
    {
        using var img = Gradient(800, 600);
        var png = Encode(img, new PngEncoder());

        var result = NewProcessor().Process(png, Fit(maxDim: 256));

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        Assert.Equal("image/webp", result.ContentType);
        Assert.True(Math.Max(result.Width, result.Height) <= 256, "largest side must be within the max dimension");
        Assert.True(result.Bytes!.Length * 2 < png.Length, "downscaled WebP should be materially smaller than the source PNG");
        Assert.Equal("image/webp", DetectedType(result.Bytes!));
    }

    [Fact]
    public void Small_image_is_not_upscaled_in_fit_mode()
    {
        using var img = Solid(100, 80, new Rgba32(10, 120, 200));
        var result = NewProcessor().Process(Encode(img, new PngEncoder()), Fit(maxDim: 512));

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        Assert.Equal(100, result.Width);
        Assert.Equal(80, result.Height);
    }

    [Fact]
    public void Square_crop_produces_a_centered_square_without_upscaling()
    {
        using var img = Solid(100, 80, new Rgba32(10, 120, 200));
        var result = NewProcessor().Process(Encode(img, new PngEncoder()), SquareCrop(maxDim: 512));

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        // Never upscaled: square side = min(maxDim, shorter side) = 80.
        Assert.Equal(80, result.Width);
        Assert.Equal(80, result.Height);
    }

    [Fact]
    public void Transparency_is_preserved()
    {
        using var img = Solid(16, 16, new Rgba32(0, 0, 0, 0)); // fully transparent
        var result = NewProcessor().Process(Encode(img, new PngEncoder()), Fit());

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        using var outImg = Image.Load<Rgba32>(result.Bytes!);
        Assert.True(outImg[0, 0].A < 255, "alpha channel should survive re-encoding");
    }

    [Fact]
    public void Animated_input_is_flattened_to_a_single_frame()
    {
        using var img = Solid(12, 12, new Rgba32(200, 0, 0));
        using var second = Solid(12, 12, new Rgba32(0, 200, 0));
        img.Frames.AddFrame(second.Frames.RootFrame); // now a 2-frame animation
        var animated = Encode(img, new WebpEncoder());

        var result = NewProcessor().Process(animated, Fit());

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        using var outImg = Image.Load(result.Bytes!);
        Assert.Equal(1, outImg.Frames.Count);
    }

    // --- US2: metadata & orientation ------------------------------------------

    [Fact]
    public void Exif_gps_and_other_metadata_are_stripped()
    {
        using var img = Solid(8, 8, new Rgba32(10, 20, 30));
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.GPSLatitudeRef, "N");
        exif.SetValue(ExifTag.Make, "TestCam");
        img.Metadata.ExifProfile = exif;
        var jpeg = Encode(img, new JpegEncoder());

        // Sanity: the input really carries EXIF.
        Assert.NotNull(Image.Identify(jpeg).Metadata.ExifProfile);

        var result = NewProcessor().Process(jpeg, Fit());

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        using var outImg = Image.Load(result.Bytes!);
        Assert.Null(outImg.Metadata.ExifProfile);
    }

    [Fact]
    public void Exif_orientation_is_baked_into_pixels()
    {
        using var img = Solid(2, 1, new Rgba32(255, 255, 255)); // 2 wide, 1 tall
        var exif = new ExifProfile();
        exif.SetValue(ExifTag.Orientation, (ushort)6); // rotate 90° CW → should become 1 wide, 2 tall
        img.Metadata.ExifProfile = exif;

        var result = NewProcessor().Process(Encode(img, new JpegEncoder()), Fit(maxDim: 4096));

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        Assert.Equal(1, result.Width);
        Assert.Equal(2, result.Height);
    }

    // --- US3: abuse resistance ------------------------------------------------

    [Fact]
    public void Empty_input_is_rejected()
    {
        var result = NewProcessor().Process([], Fit());
        Assert.Equal(ImageProcessingStatus.Empty, result.Status);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void Oversized_input_is_rejected_before_processing()
    {
        using var img = Solid(50, 50, new Rgba32(1, 2, 3));
        var png = Encode(img, new PngEncoder());
        var options = new ImageProcessingOptions { MaxInputBytes = 10 };

        var result = NewProcessor(options).Process(png, Fit());

        Assert.Equal(ImageProcessingStatus.InputTooLarge, result.Status);
    }

    [Fact]
    public void Oversized_dimensions_are_rejected_before_decode()
    {
        using var img = Solid(50, 50, new Rgba32(1, 2, 3)); // 2500 px
        var png = Encode(img, new PngEncoder());
        var options = new ImageProcessingOptions { MaxDecodePixels = 100 };

        var result = NewProcessor(options).Process(png, Fit());

        Assert.Equal(ImageProcessingStatus.DimensionsTooLarge, result.Status);
    }

    [Fact]
    public void Unsupported_but_recognized_format_is_rejected()
    {
        using var img = Solid(8, 8, new Rgba32(9, 9, 9));
        var bmp = Encode(img, new BmpEncoder()); // recognized, but not in the allow-list

        var result = NewProcessor().Process(bmp, Fit());

        Assert.Equal(ImageProcessingStatus.UnsupportedType, result.Status);
    }

    [Fact]
    public void Garbage_bytes_are_reported_unreadable_without_throwing()
    {
        var result = NewProcessor().Process("this is not an image"u8.ToArray(), Fit());
        Assert.Equal(ImageProcessingStatus.Unreadable, result.Status);
    }

    [Fact]
    public void Truncated_image_is_reported_unreadable()
    {
        using var img = Gradient(64, 64);
        var png = Encode(img, new PngEncoder());
        var truncated = png[..40]; // header may parse, pixel data is gone

        var result = NewProcessor().Process(truncated, Fit());

        Assert.Equal(ImageProcessingStatus.Unreadable, result.Status);
    }

    [Fact]
    public void Output_over_the_ceiling_is_rejected()
    {
        using var img = Gradient(400, 400);
        var png = Encode(img, new PngEncoder());

        // Force the ceiling below any realistic encode so the safety net trips.
        var result = NewProcessor().Process(png, Fit(maxDim: 400, maxOut: 10));

        Assert.Equal(ImageProcessingStatus.OutputTooLarge, result.Status);
    }

    // --- Feature 046 / #99: the showcase profile ------------------------------

    [Fact]
    public void Showcase_profile_fits_a_panorama_without_squaring_it()
    {
        // The whole point of the showcase profile: a 3:1 panorama must come back a 3:1 panorama.
        // Reusing the avatar's SquareCrop here would return 1280x1280 and throw away the subject.
        using var img = Gradient(3000, 1000);
        var jpeg = Encode(img, new JpegEncoder());

        var options = new ImageProcessingOptions();
        var result = NewProcessor(options).Process(jpeg, options.Showcase);

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        Assert.Equal("image/webp", result.ContentType);
        Assert.Equal(1280, result.Width);
        Assert.True(Math.Max(result.Width, result.Height) <= 1280, "largest side must be within the showcase bound");
        Assert.NotEqual(result.Width, result.Height);

        // Aspect ratio preserved to within a rounding pixel.
        var sourceRatio = 3000d / 1000d;
        var outputRatio = result.Width / (double)result.Height;
        Assert.True(Math.Abs(sourceRatio - outputRatio) < 0.01, $"aspect ratio drifted: {outputRatio}");

        Assert.True(result.Bytes!.Length <= options.Showcase.MaxOutputBytes, "stored showcase image must be within 1 MB");
    }

    [Fact]
    public void Showcase_profile_never_upscales_a_small_picture()
    {
        using var img = Gradient(640, 480);
        var options = new ImageProcessingOptions();

        var result = NewProcessor(options).Process(Encode(img, new PngEncoder()), options.Showcase);

        Assert.Equal(ImageProcessingStatus.Success, result.Status);
        Assert.Equal(640, result.Width);
        Assert.Equal(480, result.Height);
    }

    [Fact]
    public void Showcase_profile_differs_from_the_avatar_profile()
    {
        // Guards the decision rather than the arithmetic: if someone "simplifies" the showcase
        // profile back onto the avatar's, this fails and points at spec FR-014.
        var options = new ImageProcessingOptions();

        Assert.Equal(ImageResizeMode.Fit, options.Showcase.ResizeMode);
        Assert.Equal(ImageResizeMode.SquareCrop, options.Avatar.ResizeMode);
        Assert.True(options.Showcase.MaxDimension > options.Avatar.MaxDimension);
    }

    // --- helpers --------------------------------------------------------------

    private static Image<Rgba32> Solid(int w, int h, Rgba32 color) => new(w, h, color);

    /// <summary>
    /// A photo-like image: smooth, continuous tone. Deliberately NOT a high-frequency pattern
    /// (e.g. <c>x ^ y</c>) — PNG compresses such synthetic noise to a few KB that no lossy codec
    /// can beat, which would make any "re-encode is smaller" assertion meaningless.
    /// </summary>
    private static Image<Rgba32> Gradient(int w, int h)
    {
        var img = new Image<Rgba32>(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                img[x, y] = new Rgba32(
                    (byte)(128 + (127 * Math.Sin(x * 0.01))),
                    (byte)(128 + (127 * Math.Sin(y * 0.013))),
                    (byte)(128 + (127 * Math.Sin((x + y) * 0.007))));
            }
        }

        return img;
    }

    private static byte[] Encode(Image image, IImageEncoder encoder)
    {
        using var ms = new MemoryStream();
        image.Save(ms, encoder);
        return ms.ToArray();
    }

    private static string? DetectedType(byte[] bytes) =>
        Image.Identify(bytes).Metadata.DecodedImageFormat?.DefaultMimeType;
}
