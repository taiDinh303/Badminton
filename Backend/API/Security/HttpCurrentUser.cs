using System.Security.Claims;
using Badminton.Core.Security;

namespace Badminton.API.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public ClaimsPrincipal Principal => accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
}
