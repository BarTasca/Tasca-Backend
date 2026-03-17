using System.Text.Json;
using BarTasca.Models;
using BarTasca.Services.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebPush;
using DbPushSubscription = BarTasca.Models.PushSubscription;
using LibPushSubscription = WebPush.PushSubscription;

namespace BarTasca.Infrastructure.Notifications;

public interface IWebPushSender
{
    Task<WebPushSendResult> SendAsync(
        IReadOnlyList<DbPushSubscription> subs,
        WebPushPayload payload,
        CancellationToken ct = default);
}

public sealed record WebPushPayload(
    string Title,
    string Body,
    string Url,
    string Type
);

public sealed record WebPushSendResult(
    int Attempted,
    int Sent,
    IReadOnlyList<byte[]> InvalidEndpointHashes
);

public sealed class WebPushSender : IWebPushSender
{
    private readonly WebPushClient _client = new();
    private readonly WebPushOptions _opts;
    private readonly ILogger<WebPushSender> _logger;

    public WebPushSender(IOptions<WebPushOptions> opts, ILogger<WebPushSender> logger)
    {
        _opts = opts?.Value ?? throw new InvalidOperationException("WebPush options not set");
        _logger = logger;
    }

    public async Task<WebPushSendResult> SendAsync(
        IReadOnlyList<DbPushSubscription> subs,
        WebPushPayload payload,
        CancellationToken ct = default)
    {
        if (!_opts.Enabled) return new WebPushSendResult(subs.Count, 0, Array.Empty<byte[]>());
        if (subs.Count == 0) return new WebPushSendResult(0, 0, Array.Empty<byte[]>());

        var vapid = new VapidDetails(_opts.Subject!, _opts.VapidPublicKey!, _opts.VapidPrivateKey!);

        var json = JsonSerializer.Serialize(new
        {
            title = payload.Title,
            body = payload.Body,
            url = payload.Url,
            type = payload.Type
        });

        int sent = 0;
        var invalid = new List<byte[]>();

        foreach (var s in subs)
        {
            if (ct.IsCancellationRequested) break;

            var sub = new LibPushSubscription(s.Endpoint, s.P256dh, s.Auth);

            try
            {
                await _client.SendNotificationAsync(sub, json, vapid, ct);
                sent++;
            }
            catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.Gone or System.Net.HttpStatusCode.NotFound)
            {
                if (s.EndpointHash is not null)
                {
                    invalid.Add(s.EndpointHash);
                }
                _logger.LogInformation("WebPush subscription invalid (will deactivate). status={Status} endpointHashLen={Len}",
                    (int)ex.StatusCode, s.EndpointHash?.Length ?? 0);
            }
            catch (WebPushException ex)
            {
                _logger.LogWarning(ex, "WebPush send failed. status={Status}", (int)ex.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WebPush send failed (unexpected).");
            }
        }

        return new WebPushSendResult(subs.Count, sent, invalid);
    }
}