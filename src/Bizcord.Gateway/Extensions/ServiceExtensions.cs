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
            GetRequired(configuration, "Jwt:Issuer"), GetAudiences(configuration)));
        return services;
    }

    public static IServiceCollection AddGatewayServices(this IServiceCollection services, IConfiguration configuration)
    {
        var signingKey = GetSigningKey(configuration);
        var issuer = GetRequired(configuration, "Jwt:Issuer");
        var audiences = GetAudiences(configuration);
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
                    ValidAudiences = audiences,
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

    // Every service behind the gateway checks that the token is meant for it, so a faucet token names all of them
    private static string[] GetAudiences(IConfiguration configuration)
    {
        var audiences = configuration.GetSection("Jwt:Audiences").Get<string[]>();
        return audiences is { Length: > 0 } ? audiences : throw new InvalidOperationException("Jwt:Audiences is required.");
    }

    private static string GetSigningKey(IConfiguration configuration) =>
        GetRequired(configuration, "Jwt:IssuerSigningKey");

    private static string GetRequired(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException($"{key} is required.");
}
