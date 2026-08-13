using Microsoft.Extensions.Logging;
using MX.Application.Abstractions;
using MX.Application.Common;

namespace MX.Infrastructure.Storage;

/// <summary>
/// Saves uploaded images to a directory on disk, served back by the static file
/// middleware.
///
/// Three deliberate safety properties:
///
/// 1. The filename is generated, never taken from the upload. A client-supplied
///    name is the classic path-traversal vector ("../../appsettings.json"), and
///    generating one removes the attack rather than filtering for it.
/// 2. The format is decided from the file's leading bytes, not its declared
///    content type — see <see cref="ImageSignature"/>.
/// 3. The read is bounded, so an oversized upload is rejected without first
///    buffering it all into memory.
/// </summary>
public sealed class LocalFileStorage(
    string uploadsDirectory,
    long maxImageSizeBytes,
    ILogger<LocalFileStorage> logger) : IFileStorage
{
    /// <summary>Matches the <c>uploads/…</c> convention in the supplied dataset.</summary>
    private const string PublicPrefix = "uploads";

    public async Task<Result<string>> SaveImageAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();

        // One byte past the limit is enough to know it is too big.
        var read = await CopyAtMostAsync(content, buffer, maxImageSizeBytes + 1, cancellationToken)
            .ConfigureAwait(false);

        if (read == 0)
        {
            return Result<string>.Invalid("The uploaded image is empty.");
        }

        if (read > maxImageSizeBytes)
        {
            return Result<string>.Invalid(
                $"Images must be {maxImageSizeBytes / (1024 * 1024)} MB or smaller.");
        }

        buffer.Position = 0;
        var bytes = buffer.GetBuffer().AsMemory(0, (int)read);

        if (!ImageSignature.TryDetect(bytes.Span, out var format))
        {
            return Result<string>.Invalid(
                $"That file is not an image we recognise. Accepted formats: {ImageSignature.SupportedFormats}.");
        }

        Directory.CreateDirectory(uploadsDirectory);

        // A generated name: no traversal, no collisions, no reliance on anything
        // the client sent. The extension comes from the detected format.
        var fileName = $"{Guid.NewGuid():N}{format.Extension}";
        var fullPath = Path.Combine(uploadsDirectory, fileName);

        await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Stored an uploaded {Format} image as {FileName}.", format.ContentType, fileName);

        // Forward slashes regardless of platform — this is a URL path, not a file path.
        return Result<string>.Success($"{PublicPrefix}/{fileName}");
    }

    /// <summary>
    /// Copies at most <paramref name="limit"/> bytes, so a hostile or accidental
    /// giant upload cannot exhaust memory before the size check runs.
    /// </summary>
    private static async Task<long> CopyAtMostAsync(
        Stream source,
        Stream destination,
        long limit,
        CancellationToken cancellationToken)
    {
        var rented = new byte[81_920];
        long total = 0;

        while (total < limit)
        {
            var wanted = (int)Math.Min(rented.Length, limit - total);
            var read = await source.ReadAsync(rented.AsMemory(0, wanted), cancellationToken).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(rented.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            total += read;
        }

        return total;
    }
}
