namespace BarTasca.Services.Options;

public sealed class WebPushOptions
{
    public string? VapidPublicKey { get; init; }
    public string? VapidPrivateKey { get; init; }
    public string? Subject { get; init; }
    public string? PublicAppBaseUrl { get; init; }

    public bool Enabled =>
        !string.IsNullOrWhiteSpace(VapidPublicKey) &&
        !string.IsNullOrWhiteSpace(VapidPrivateKey) &&
        !string.IsNullOrWhiteSpace(Subject);
}