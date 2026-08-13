using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using MX.Application.Common;
using MX.Infrastructure.Storage;

namespace MX.Infrastructure.Tests;

/// <summary>
/// Upload handling, where the interesting cases are all adversarial: a file that
/// claims to be an image, a filename that tries to escape the directory, a body
/// large enough to hurt.
/// </summary>
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _directory;

    public LocalFileStorageTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"mx-uploads-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private LocalFileStorage Storage(long maxBytes = 5 * 1024 * 1024) =>
        new(_directory, maxBytes, NullLogger<LocalFileStorage>.Instance);

    private static MemoryStream Bytes(byte[] content) => new(content);

    private static MemoryStream Png() => Bytes(ImageSignature.SampleBytesFor(".png"));

    // ------------------------------------------------------------- happy path

    [Fact]
    public async Task Stores_a_png_and_returns_a_dataset_style_path()
    {
        var result = await Storage().SaveImageAsync(Png());

        Assert.True(result.IsSuccess);
        Assert.StartsWith("uploads/", result.Value!, StringComparison.Ordinal);
        Assert.EndsWith(".png", result.Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".gif")]
    public async Task Accepts_each_supported_format_and_names_the_file_after_it(string extension)
    {
        var result = await Storage().SaveImageAsync(Bytes(ImageSignature.SampleBytesFor(extension)));

        Assert.True(result.IsSuccess);
        Assert.EndsWith(extension, result.Value!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_stored_file_actually_exists_with_the_original_bytes()
    {
        var original = ImageSignature.SampleBytesFor(".png");

        var result = await Storage().SaveImageAsync(Bytes(original));

        var fileName = Path.GetFileName(result.Value!);
        var written = await File.ReadAllBytesAsync(Path.Combine(_directory, fileName));

        Assert.Equal(original, written);
    }

    [Fact]
    public async Task Two_uploads_of_the_same_image_do_not_collide()
    {
        var storage = Storage();

        var first = await storage.SaveImageAsync(Png());
        var second = await storage.SaveImageAsync(Png());

        Assert.NotEqual(first.Value, second.Value);
        Assert.Equal(2, Directory.GetFiles(_directory).Length);
    }

    [Fact]
    public async Task Creates_the_uploads_directory_on_first_use()
    {
        Assert.False(Directory.Exists(_directory));

        await Storage().SaveImageAsync(Png());

        Assert.True(Directory.Exists(_directory));
    }

    [Fact]
    public async Task The_returned_path_uses_forward_slashes_on_every_platform()
    {
        // It is a URL, not a file path — a backslash would break the <img> tag
        // on Windows and match neither the dataset nor the static file route.
        var result = await Storage().SaveImageAsync(Png());

        Assert.DoesNotContain('\\', result.Value!);
    }

    // ------------------------------------------------------------- rejections

    [Fact]
    public async Task Rejects_a_file_that_is_not_an_image()
    {
        var script = Bytes(Encoding.ASCII.GetBytes("#!/bin/sh\nrm -rf /\n"));

        var result = await Storage().SaveImageAsync(script);

        Assert.True(result.IsFailure);
        Assert.Equal(ResultErrorType.Validation, result.ErrorType);
    }

    [Fact]
    public async Task A_rejected_upload_writes_nothing_to_disk()
    {
        await Storage().SaveImageAsync(Bytes(Encoding.ASCII.GetBytes("not an image at all")));

        Assert.False(Directory.Exists(_directory) && Directory.GetFiles(_directory).Length > 0);
    }

    [Fact]
    public async Task Rejects_a_disguised_executable_despite_an_image_extension()
    {
        // The whole reason detection reads the bytes: renaming a script to
        // .png and declaring image/png satisfies every other kind of check.
        var disguised = Bytes([0x4D, 0x5A, .. Encoding.ASCII.GetBytes("MZ is a Windows executable")]);

        Assert.True((await Storage().SaveImageAsync(disguised)).IsFailure);
    }

    [Fact]
    public async Task Rejects_an_empty_upload()
    {
        var result = await Storage().SaveImageAsync(Bytes([]));

        Assert.True(result.IsFailure);
        Assert.Contains("empty", string.Join(' ', result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rejects_an_image_over_the_size_limit()
    {
        var oversized = ImageSignature.SampleBytesFor(".png").Concat(new byte[4096]).ToArray();

        var result = await Storage(maxBytes: 1024).SaveImageAsync(Bytes(oversized));

        Assert.True(result.IsFailure);
        Assert.Contains("smaller", string.Join(' ', result.Errors), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Accepts_an_image_exactly_at_the_size_limit()
    {
        // Guards the boundary: the read deliberately fetches one byte past the
        // limit to detect oversize, which is easy to turn into an off-by-one.
        var exact = ImageSignature.SampleBytesFor(".png");

        var result = await Storage(maxBytes: exact.Length).SaveImageAsync(Bytes(exact));

        Assert.True(result.IsSuccess);
    }

    // ------------------------------------------------------- signature checks

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }, ".jpg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, ".png")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, ".gif")]
    public void Detects_each_format_from_its_leading_bytes(byte[] header, string expected)
    {
        Assert.True(ImageSignature.TryDetect(header, out var format));
        Assert.Equal(expected, format.Extension);
    }

    [Fact]
    public void Detects_webp_through_its_riff_container()
    {
        // "RIFF" + 4 length bytes + "WEBP" — the format check has to look past
        // the first four bytes, unlike every other format here.
        byte[] webp = [.. "RIFF"u8, 0x00, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

        Assert.True(ImageSignature.TryDetect(webp, out var format));
        Assert.Equal(".webp", format.Extension);
    }

    [Fact]
    public void Does_not_mistake_a_non_webp_riff_file_for_an_image()
    {
        // A .wav is also RIFF. Only the container matching is not enough.
        byte[] wav = [.. "RIFF"u8, 0x00, 0x00, 0x00, 0x00, .. "WAVE"u8, .. "fmt "u8];

        Assert.False(ImageSignature.TryDetect(wav, out _));
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x89 })]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46 })] // "RIFF" and nothing else
    public void Handles_content_too_short_to_identify(byte[] truncated)
    {
        // Must return false, not read off the end of the buffer.
        Assert.False(ImageSignature.TryDetect(truncated, out _));
    }
}
