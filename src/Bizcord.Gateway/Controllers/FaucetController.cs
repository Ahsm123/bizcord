using Bizcord.Gateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bizcord.Gateway.Controllers;

[ApiController]
[Route("[controller]")]
public class FaucetController(JwtTokenService jwtTokenService) : ControllerBase
{
    [HttpPost]
    public IActionResult Dispense([FromBody] CreateTokenOptions options)
    {
        var token = jwtTokenService.CreateToken(options);
        return Ok(token);
    }
}
