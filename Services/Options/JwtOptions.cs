namespace BarTasca.Services.Options;

public sealed class JwtOptions
{
    public string Secret { get; init; } = default!;
    public string Issuer { get; init; } = default!;
    public string Audience { get; init; } = default!;
    public int StaffExpiresHours { get; init; } = 24;
    public int CustomerExpiresHours { get; init; } = 2;
}
