using System.Text;

namespace MX.Infrastructure.Storage;

/// <summary>
/// Identifies an image format from its leading bytes.
///
/// The upload's declared <c>Content-Type</c> and filename extension are both
/// supplied by whoever is uploading, so neither proves anything: a shell script
/// renamed to <c>.png</c> and posted as <c>image/png</c> satisfies both checks.
/// The bytes are the one part of an upload the client cannot lie about, so the
/// format is decided here and the extension is derived from the result.
/// </summary>
internal readonly record struct ImageFormat(string Extension, string ContentType)
{
    public static ImageFormat Jpeg { get; } = new(".jpg", "image/jpeg");
    public static ImageFormat Png { get; } = new(".png", "image/png");
    public static ImageFormat Gif { get; } = new(".gif", "image/gif");
    public static ImageFormat Webp { get; } = new(".webp", "image/webp");
}

internal static class ImageSignature
{
    /// <summary>Longest signature we need to inspect (WebP's RIFF header).</summary>
    public const int MaxSignatureLength = 12;

    private static ReadOnlySpan<byte> JpegMagic => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> PngMagic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Covers both "GIF87a" and "GIF89a".</summary>
    private static ReadOnlySpan<byte> GifMagic => "GIF8"u8;

    private static ReadOnlySpan<byte> RiffMagic => "RIFF"u8;
    private static ReadOnlySpan<byte> WebpMagic => "WEBP"u8;

    /// <summary>
    /// Recognises the four formats browsers actually produce for a photo of a
    /// broken appliance. Anything else is rejected rather than guessed at.
    /// </summary>
    public static bool TryDetect(ReadOnlySpan<byte> content, out ImageFormat format)
    {
        if (content.StartsWith(JpegMagic))
        {
            format = ImageFormat.Jpeg;
            return true;
        }

        if (content.StartsWith(PngMagic))
        {
            format = ImageFormat.Png;
            return true;
        }

        if (content.StartsWith(GifMagic))
        {
            format = ImageFormat.Gif;
            return true;
        }

        // WebP is a RIFF container: "RIFF", a 4-byte length, then "WEBP".
        if (content.Length >= MaxSignatureLength &&
            content.StartsWith(RiffMagic) &&
            content[8..12].SequenceEqual(WebpMagic))
        {
            format = ImageFormat.Webp;
            return true;
        }

        format = default;
        return false;
    }

    /// <summary>Human-readable list for validation messages.</summary>
    public static string SupportedFormats => string.Join(", ", ["JPEG", "PNG", "GIF", "WebP"]);

    /// <summary>Only used to make test fixtures; keeps the magic numbers in one place.</summary>
    public static byte[] SampleBytesFor(string extension) => extension switch
    {
        ".png" => [.. PngMagic, .. Encoding.ASCII.GetBytes("fake png body")],
        ".jpg" => [.. JpegMagic, .. Encoding.ASCII.GetBytes("fake jpeg body")],
        ".gif" => [.. GifMagic, .. Encoding.ASCII.GetBytes("9a fake gif body")],
        _ => throw new ArgumentOutOfRangeException(nameof(extension), extension, "No sample for this format.")
    };
}
