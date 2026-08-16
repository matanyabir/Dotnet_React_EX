namespace MX.Api.Authentication;

/// <summary>
/// Where the access token lives on a browser client.
///
/// The token used to be handed to the frontend in the login body, which meant it
/// had to be kept somewhere script could reach — and anything script can reach,
/// injected script can reach too. Putting it in an <c>HttpOnly</c> cookie takes
/// it out of that reach entirely: the browser attaches it to requests, and no
/// code on the page can read it back out, so a single XSS no longer hands over a
/// working admin session.
///
/// The trade is that cookies travel automatically, which is what CSRF exploits.
/// Three things answer that, and all three have to hold:
///
/// 1. <c>SameSite=Lax</c> — the browser will not attach this cookie to a
///    cross-site POST/PUT/DELETE at all, which is every state-changing route here.
/// 2. Editing requires <c>Content-Type: application/json</c>, which a form-based
///    forgery cannot set without a preflight the CORS policy will refuse.
/// 3. The CORS policy names its origins; credentialed requests from anywhere else
///    are rejected before a handler sees them.
/// </summary>
internal static class SessionCookie
{
    /// <summary>
    /// The <c>__Host-</c> prefix is enforced by the browser: it will only store
    /// the cookie if it is Secure, path <c>/</c>, and carries no Domain — so a
    /// sibling subdomain cannot plant a session cookie on this host. Dropped over
    /// plain HTTP, where the prefix's conditions cannot be met.
    /// </summary>
    private const string SecureName = "__Host-mx_session";
    private const string InsecureName = "mx_session";

    /// <summary>Both spellings, so a token set over either scheme is still found.</summary>
    public static readonly string[] Names = [SecureName, InsecureName];

    public static void Append(HttpContext context, string token, DateTimeOffset expiresAt)
    {
        var secure = context.Request.IsHttps;

        context.Response.Cookies.Append(Name(secure), token, Options(secure, expiresAt));
    }

    /// <summary>
    /// Clears the cookie under both names. A delete is only honoured when its
    /// attributes match the ones the cookie was set with, so this mirrors
    /// <see cref="Options"/> rather than relying on the name alone.
    /// </summary>
    public static void Delete(HttpContext context)
    {
        var secure = context.Request.IsHttps;

        foreach (var name in Names)
        {
            context.Response.Cookies.Delete(name, Options(secure, expiresAt: null));
        }
    }

    /// <summary>Reads the token out of whichever cookie is present, if either is.</summary>
    public static string? Read(HttpRequest request)
    {
        foreach (var name in Names)
        {
            if (request.Cookies.TryGetValue(name, out var token) && !string.IsNullOrEmpty(token))
            {
                return token;
            }
        }

        return null;
    }

    private static string Name(bool secure) => secure ? SecureName : InsecureName;

    private static CookieOptions Options(bool secure, DateTimeOffset? expiresAt) => new()
    {
        // The whole point: unreadable from JavaScript.
        HttpOnly = true,

        // Set only over HTTPS. Forcing it on a plain-HTTP dev server would have
        // the browser drop the cookie silently and log-ins would appear to fail.
        Secure = secure,

        SameSite = SameSiteMode.Lax,
        Path = "/",

        // Expires with the token it carries, so a browser left open overnight
        // does not keep sending a credential the API will only reject.
        Expires = expiresAt
    };
}
