using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SignalRChat.Auth;

/// <summary>
/// Minimal API-key authentication handler for demonstration purposes. Reads the key from the
/// <c>api_key</c> query parameter (first) or the <c>X-API-Key</c> header (fallback), and accepts
/// the single hard-coded demo key <see cref="DemoApiKey"/>.
/// </summary>
/// <remarks>
/// This is intentionally simple — a production handler would validate against a secrets store or
/// a database rather than a constant.
/// </remarks>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The demo API key accepted by this handler. Use this value in Scalar's Authentication panel.</summary>
    public const string DemoApiKey = "signalr-demo-key";

    private const string QueryParamName = "api_key";
    private const string HeaderName = "X-API-Key";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = Request.Query[QueryParamName].ToString() is { Length: > 0 } q
            ? q
            : Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!string.Equals(key, DemoApiKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "api-user") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
