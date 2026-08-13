using MX.Application.Common;

namespace MX.Application.Abstractions;

/// <summary>
/// Stores an uploaded image and returns the path it can be served from.
///
/// Note what this deliberately does <em>not</em> take: the browser's filename or
/// its declared content type. Both are supplied by the caller and neither can be
/// trusted — the filename invites path traversal and the content type is a claim,
/// not a fact. Implementations decide the type from the bytes and name the file
/// themselves.
/// </summary>
public interface IFileStorage
{
    /// <returns>
    /// On success, a storage-relative path such as <c>uploads/a1b2c3.png</c>,
    /// matching the convention in the supplied dataset. On failure, a validation
    /// result the API turns into a 400 — a rejected upload is bad input, not an
    /// exceptional condition.
    /// </returns>
    Task<Result<string>> SaveImageAsync(Stream content, CancellationToken cancellationToken = default);
}
