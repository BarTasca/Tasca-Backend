namespace BarTasca.Infrastructure.Options;

public sealed class TwilioOptions
{
    public string? Sid { get; init; }
    public string? Token { get; init; }
    public string? From { get; init; }
    public bool Enabled =>
        !string.IsNullOrWhiteSpace(Sid) &&
        !string.IsNullOrWhiteSpace(Token) &&
        !string.IsNullOrWhiteSpace(From);
}
