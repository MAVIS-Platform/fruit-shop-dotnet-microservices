using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApiGateway.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ApiGateway.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    IOptions<JwtOptions> jwtOptions,
    IOptions<DemoAuthOptions> demoAuthOptions) : ControllerBase
{
    private static readonly Dictionary<string, (string UserId, string Role)> DemoUsers =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["customer@freshfruit.vn"] = ("customer-01", "Customer"),
            ["staff@freshfruit.vn"] = ("staff-01", "Staff"),
            ["admin@freshfruit.vn"] = ("admin-01", "Admin")
        };

    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType<DemoTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult IssueToken([FromBody] DemoLoginRequest request)
    {
        if (!DemoUsers.TryGetValue(request.Email, out var user) ||
            !string.Equals(request.Password, demoAuthOptions.Value.Password, StringComparison.Ordinal))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid Credentials",
                Detail = "Email or password is invalid for the demo authentication endpoint.",
                Instance = HttpContext.Request.Path
            });
        }

        var jwt = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId),
            new(JwtRegisteredClaimNames.Email, request.Email.ToLowerInvariant()),
            new("role", user.Role),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(jwt.ExpirationMinutes);
        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return Ok(new DemoTokenResponse(
            accessToken,
            "Bearer",
            jwt.ExpirationMinutes * 60,
            user.UserId,
            user.Role));
    }
}

public sealed record DemoLoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public sealed record DemoTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    string UserId,
    string Role);
