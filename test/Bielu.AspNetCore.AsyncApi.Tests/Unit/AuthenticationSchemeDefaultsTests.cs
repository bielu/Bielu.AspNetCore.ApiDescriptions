using Bielu.AspNetCore.AsyncApi.Services;
using ByteBard.AsyncAPI.Models;
using Shouldly;
using Xunit;

namespace Bielu.AspNetCore.AsyncApi.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="AuthenticationSchemeDefaults.DefaultMap"/> and <see cref="DetectedAuthenticationScheme"/>.
/// The handler types below stand in for the real ones, since mapping is by simple type name.
/// </summary>
public class AuthenticationSchemeDefaultsTests
{
    private sealed class JwtBearerHandler;

    private sealed class CookieAuthenticationHandler;

    private sealed class NegotiateHandler;

    private sealed class OpenIdConnectHandler;

    [Fact]
    public void DefaultMap_JwtBearerHandler_MapsToHttpBearerJwt()
    {
        // Act
        var scheme = AuthenticationSchemeDefaults.DefaultMap(new DetectedAuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)));

        // Assert
        scheme.ShouldNotBeNull();
        scheme.Type.ShouldBe(SecuritySchemeType.Http);
        scheme.Scheme.ShouldBe("bearer");
        scheme.BearerFormat.ShouldBe("JWT");
        scheme.Description.ShouldContain("'Bearer'");
    }

    [Fact]
    public void DefaultMap_CookieHandler_MapsToCookieApiKey()
    {
        // Act
        var scheme = AuthenticationSchemeDefaults.DefaultMap(new DetectedAuthenticationScheme("Cookies", "Cookie auth", typeof(CookieAuthenticationHandler)));

        // Assert
        scheme.ShouldNotBeNull();
        scheme.Type.ShouldBe(SecuritySchemeType.HttpApiKey);
        scheme.In.ShouldBe(ParameterLocation.Cookie);
        scheme.Name.ShouldBe(".AspNetCore.Cookies");
    }

    [Fact]
    public void DefaultMap_NegotiateHandler_MapsToHttpNegotiate()
    {
        // Act
        var scheme = AuthenticationSchemeDefaults.DefaultMap(new DetectedAuthenticationScheme("Negotiate", null, typeof(NegotiateHandler)));

        // Assert
        scheme.ShouldNotBeNull();
        scheme.Type.ShouldBe(SecuritySchemeType.Http);
        scheme.Scheme.ShouldBe("negotiate");
    }

    [Fact]
    public void DefaultMap_UninferableHandler_ReturnsNull()
    {
        // Act
        var scheme = AuthenticationSchemeDefaults.DefaultMap(new DetectedAuthenticationScheme("oidc", null, typeof(OpenIdConnectHandler)));

        // Assert
        scheme.ShouldBeNull();
    }

    [Fact]
    public void DetectedAuthenticationScheme_ExposesValuesAndMatchesHandlerByName()
    {
        // Act
        var detected = new DetectedAuthenticationScheme("Bearer", "JWT", typeof(JwtBearerHandler));

        // Assert
        detected.Name.ShouldBe("Bearer");
        detected.DisplayName.ShouldBe("JWT");
        detected.HandlerType.ShouldBe(typeof(JwtBearerHandler));
        detected.HandlerIs(nameof(JwtBearerHandler)).ShouldBeTrue();
        detected.HandlerIs("jwtbearerhandler").ShouldBeFalse();
    }
}
