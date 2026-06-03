using System.Security.Claims;
using TranzaPay.Application.Interfaces;

namespace TranzaPay.Api.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Role => httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role);

    public string? IpAddress => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
