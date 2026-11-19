using System.ComponentModel.DataAnnotations;

namespace ApiGateway.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(5, 1440)]
    public int ExpirationMinutes { get; init; } = 120;
}

public sealed class DemoAuthOptions
{
    public const string SectionName = "DemoAuth";

    [Required]
    [MinLength(6)]
    public string Password { get; init; } = string.Empty;
}
