namespace MX.Application.Common;

/// <summary>
/// Why an operation did not succeed. Deliberately small: it describes the shape
/// of the failure, not an HTTP status, so the application layer stays unaware of
/// the transport. The API translates these in exactly one place.
/// </summary>
public enum ResultErrorType
{
    None = 0,

    /// <summary>The caller asked for something that does not exist.</summary>
    NotFound,

    /// <summary>The caller sent something the domain will not accept.</summary>
    Validation,

    /// <summary>The caller could not be identified — bad credentials.</summary>
    Unauthorized
}

/// <summary>
/// The outcome of a service call.
///
/// Expected failures — a missing ticket, a malformed email address — are ordinary
/// return values here rather than exceptions. Exceptions stay reserved for the
/// genuinely unexpected, so a 404 does not cost a stack trace and cannot be
/// swallowed by an over-broad catch.
/// </summary>
public sealed class Result<TValue>
{
    private Result(TValue value)
    {
        IsSuccess = true;
        Value = value;
        ErrorType = ResultErrorType.None;
        Errors = [];
    }

    private Result(ResultErrorType errorType, IReadOnlyList<string> errors)
    {
        IsSuccess = false;
        Value = default;
        ErrorType = errorType;
        Errors = errors;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>The result, when <see cref="IsSuccess"/>. Otherwise default.</summary>
    public TValue? Value { get; }

    public ResultErrorType ErrorType { get; }

    /// <summary>Human-readable reasons, safe to show the caller. Empty on success.</summary>
    public IReadOnlyList<string> Errors { get; }

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> NotFound(string message) =>
        new(ResultErrorType.NotFound, [message]);

    public static Result<TValue> Invalid(params string[] errors) =>
        new(ResultErrorType.Validation, errors);

    public static Result<TValue> Invalid(IReadOnlyList<string> errors) =>
        new(ResultErrorType.Validation, errors);

    public static Result<TValue> Unauthorized(string message) =>
        new(ResultErrorType.Unauthorized, [message]);
}
