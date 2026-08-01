using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AI_Dashboard.Application.Common.Interfaces;
using AI_Dashboard.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AI_Dashboard.Infrastructure.Auth;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    public JwtTokenGenerator(IOptions<JwtOptions> options) => _options = options.Value;

    public string GenerateToken(User user)
    {
        // tenant_id and user_id are the two claims every downstream query depends on -
        // ICurrentUserService reads exactly these two back out.
        var claims = new[]
        {
            new Claim("user_id", user.Id.ToString()),
            new Claim("tenant_id", user.TenantId.ToString()),
            new Claim("name", user.Name),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}