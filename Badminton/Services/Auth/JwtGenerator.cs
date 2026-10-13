using M.Core.Store;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Services
{
    /// <summary>
    /// Builds signed JWTs for the auth endpoints. The key/issuer/audience come
    /// from configuration (JwtSettings). This is encapsulation: the exact claim
    /// layout + signing algorithm live in ONE place the API layer just calls.
    /// </summary>
    public class JwtGenerator
    {
        private readonly JwtSettings _settings;

        public JwtGenerator(IOptions<JwtSettings> settings)
        {
            _settings = settings.Value;
        }

        public (string access, string refresh, int expiresInMinutes) Generate(
            int userId, string email, string fullName, string role)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = DateTime.UtcNow;

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new(JwtRegisteredClaimNames.Email, email),
                new(ClaimTypes.Name, fullName),
                new(ClaimTypes.Role, role)
            };

            var accessToken = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(_settings.ExpireMinutes),
                signingCredentials: creds);

            // Refresh token: a random 128-byte value, 7 day lifetime.
            var refreshBytes = new byte[128];
            RandomNumberGenerator.Fill(refreshBytes);
            var refreshToken = Convert.ToBase64String(refreshBytes);

            return (
                new JwtSecurityTokenHandler().WriteToken(accessToken),
                refreshToken,
                _settings.ExpireMinutes);
        }
    }
}
