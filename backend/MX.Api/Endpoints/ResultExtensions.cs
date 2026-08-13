using MX.Application.Common;

namespace MX.Api.Endpoints;

/// <summary>
/// Translates a service <see cref="Result{TValue}"/> into an HTTP response.
///
/// Every endpoint funnels through here, so the mapping from "not found" to 404
/// and "invalid" to 400 is stated once. Failures come back as RFC 9457
/// ProblemDetails, which is what ASP.NET produces for unhandled errors too — so
/// a client sees one error shape regardless of where the failure came from.
/// </summary>
internal static class ResultExtensions
{
    public static IResult ToHttpResult<TValue>(
        this Result<TValue> result,
        Func<TValue, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess is null ? Results.Ok(result.Value) : onSuccess(result.Value!);
        }

        return result.ErrorType switch
        {
            ResultErrorType.NotFound => Results.Problem(
                title: "Not found",
                detail: result.Errors.FirstOrDefault(),
                statusCode: StatusCodes.Status404NotFound),

            ResultErrorType.Validation => Results.ValidationProblem(
                errors: new Dictionary<string, string[]> { ["request"] = [.. result.Errors] },
                title: "The request could not be accepted"),

            // No other failure kind exists; reaching here means one was added to
            // the enum without deciding what it means over HTTP.
            _ => Results.Problem(
                title: "Unexpected error",
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
