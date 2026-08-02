using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bielu.AspNetCore.AsyncApi.Scalar.Broker;

/// <summary>
/// Decides whether a caller may reach the broker proxy endpoints.
/// </summary>
/// <remarks>
/// The bridge grants publish access to your broker to anyone who can reach it, so it refuses to
/// serve an unprotected deployment rather than trusting that nobody found the URL. "Protected" means
/// the endpoint carries authorization metadata (from <c>RequireAuthorization</c>), the app is running
/// in Development, or the operator opted out explicitly via
/// <see cref="ScalarBrokerBridgeOptions.AllowAnonymous" />.
/// </remarks>
internal sealed class BrokerBridgeAccessGuard(
    ScalarBrokerBridgeOptions options,
    IHostEnvironment environment,
    ILogger<BrokerBridgeAccessGuard> logger)
{
    private int _developmentWarningLogged;

    /// <summary>
    /// Whether the request may proceed to the bridge.
    /// </summary>
    public bool IsAllowed(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (options.AllowAnonymous)
        {
            return true;
        }

        // RequireAuthorization puts IAuthorizeData on the endpoint, and the authorization middleware
        // has already enforced it by the time the handler runs - reaching here means it passed.
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>() is not null)
        {
            return true;
        }

        if (environment.IsDevelopment())
        {
            // Once, not per request: this fires on every console interaction otherwise.
            if (Interlocked.Exchange(ref _developmentWarningLogged, 1) == 0)
            {
                logger.LogWarning(
                    "The Scalar broker console proxy is reachable without authorization. This is allowed because the app is running in the Development environment. Before deploying elsewhere, call MapScalarBrokerAssets().RequireAuthorization(...) - the proxy can publish to your broker.");
            }

            return true;
        }

        logger.LogError(
            "Refused a request to the Scalar broker console proxy: the endpoints carry no authorization metadata and the app is not running in Development. Call MapScalarBrokerAssets().RequireAuthorization(...), or set AllowAnonymous on AddScalarBrokerBridge if the endpoints are protected by something outside ASP.NET Core.");

        return false;
    }
}
