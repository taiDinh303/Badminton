using System.Security.Claims;

namespace Badminton.Core.Security;

/// <summary>Request identity boundary. Business code does not depend on HttpContext.</summary>
public interface ICurrentUser
{
    ClaimsPrincipal Principal { get; }
    int Id => int.TryParse(Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    bool IsInRole(string role) => Principal.IsInRole(role);
}
