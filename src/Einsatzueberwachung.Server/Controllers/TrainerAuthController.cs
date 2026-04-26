using System.Security.Claims;
using Einsatzueberwachung.Server.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Einsatzueberwachung.Server.Controllers;

[ApiController]
[Route("api/trainer-auth")]
[IgnoreAntiforgeryToken]
public sealed class TrainerAuthController : ControllerBase
{
    private readonly IOptionsMonitor<TrainerAuthOptions> _options;

    public TrainerAuthController(IOptionsMonitor<TrainerAuthOptions> options)
    {
        _options = options;
    }

    [HttpGet("status")]
    public IActionResult Status()
    {
        var isTrainer = User?.Identity?.IsAuthenticated == true && User.IsInRole("Trainer");
        return Ok(new { authenticated = isTrainer });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] TrainerLoginRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Passwort fehlt." });
        }

        var options = _options.CurrentValue;
        if (!string.Equals(request.Password.Trim(), options.Password, StringComparison.Ordinal))
        {
            return Unauthorized(new { error = "Ungueltiges Trainer-Passwort." });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "trainer"),
            new(ClaimTypes.Name, "Trainer"),
            new(ClaimTypes.Role, "Trainer")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(Math.Max(1, options.SessionHours))
            });

        return Ok(new { authenticated = true });
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout([FromQuery] string? returnUrl = null)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return LocalRedirect("/einstellungen");
    }

    public sealed class TrainerLoginRequest
    {
        public string Password { get; set; } = string.Empty;
    }
}
