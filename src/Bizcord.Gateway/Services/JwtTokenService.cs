using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;

namespace Bizcord.Gateway.Services;

public class CreateTokenOptions
{
    public Dictionary<string, string> Claims { get; set; } = new();
}

// iss and aud are set from config so the downstream API can check who issued the token and that it was meant for it.
// aud lists every service behind the gateway, and each service only accepts tokens that name it.
public class JwtTokenService(string authenticationProviderKey, string issuer, IReadOnlyList<string> audiences)
{
    public AuthenticationToken CreateToken(CreateTokenOptions options)
    {
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authenticationProviderKey));
        var signingCredentials = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);

        var claims = audiences.Select(a => new Claim(JwtRegisteredClaimNames.Aud, a)).ToList();
        foreach (var claim in options.Claims)
        {
            if (claim.Key == "sub")
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Sub, claim.Value));
                continue;
            }
            claims.Add(new Claim(claim.Key, claim.Value));
        }

        var tokenOptions = new JwtSecurityToken(
            issuer: issuer,
            signingCredentials: signingCredentials,
            expires: DateTime.UtcNow.AddHours(1),
            claims: claims
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(tokenOptions);
        var authToken = new AuthenticationToken
        {
            Value = tokenString
        };

        return authToken;
    }
}
