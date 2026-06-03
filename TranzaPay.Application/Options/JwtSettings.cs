namespace TranzaPay.Application.Options;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TranzaPay";
    public string Audience { get; set; } = "TranzaPay";
    public string Secret { get; set; } = "replace-this-development-secret-with-at-least-32-characters";
    public int ExpirationMinutes { get; set; } = 120;
}
