using System.Text;
using Bizcord.Gateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;

namespace Bizcord.Gateway.Extensions;

public static class ServiceExtensions
{
    public static IServiceCollection AddJwtTokenService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(new JwtTokenService(GetSigningKey(configuration),
            GetRequired(configuration, "Jwt:Issuer"), GetRequired(configuration, "Jwt:Audience")));
        return services;
    }

    public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = GetSigningKey(configuration);
        var issuer = GetRequired(configuration, "Jwt:Issuer");
        var audience = GetRequired(configuration, "Jwt:Audience");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                // Preserve sub, role and scope for Ocelot's claim lookups.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    NameClaimType = "sub",
                    RoleClaimType = "role",
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();
        services.AddOcelot(configuration);
        return services;
    }

    private static string GetSigningKey(IConfiguration configuration) =>
        GetRequired(configuration, "Jwt:IssuerSigningKey");

    private static string GetRequired(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is required.");
}
