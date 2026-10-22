using System.Security.Claims;

namespace Badminton.Core.Security;

public interface ICurrentUser
{
    ClaimsPrincipal Principal { get; }
    int Id => int.TryParse(Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    bool IsInRole(string role) => Principal.IsInRole(role);
}
