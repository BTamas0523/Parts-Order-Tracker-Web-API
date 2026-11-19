using Microsoft.Identity.Web;

namespace PartsOrderTracker.Api.Services;

public sealed class ClaimsCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
  public string UserId =>
    httpContextAccessor.HttpContext?.User.GetObjectId()
    ?? throw new InvalidOperationException("No authenticated user with an object id (oid) claim.");

  public string UserName =>
    httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value ?? string.Empty;

  public string UserEmail =>
    httpContextAccessor.HttpContext?.User.FindFirst("preferred_username")?.Value
    ?? httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value
    ?? string.Empty;
}
