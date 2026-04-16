using IoTSpy.Api.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IoTSpy.Api.Authorization;

/// <summary>
/// Enforces that an API-key-authenticated request carries the specified scope claim.
/// JWT-authenticated requests are exempt (they have unrestricted access).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireScopeAttribute(string scope) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Not authenticated — let the [Authorize] attribute handle it.
        if (user.Identity?.IsAuthenticated != true)
            return;

        // JWT-authenticated users carry no api_key_id claim → unrestricted.
        if (!user.HasClaim(c => c.Type == ApiKeyAuthenticationHandler.ApiKeyIdClaimType))
            return;

        // API-key-authenticated: must have the required scope.
        if (!user.HasClaim(ApiKeyAuthenticationHandler.ScopeClaimType, scope))
            context.Result = new ForbidResult();
    }
}
