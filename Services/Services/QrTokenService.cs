using System.Security.Cryptography;
using System.Text;
using BarTasca.DTOs.Qr;
using BarTasca.Services.Options;
using BarTasca.Services.Interfaces;
using Microsoft.Extensions.Options; 

namespace BarTasca.Services.Services;

public class QrTokenService : IQrTokenService
{
    private readonly QrOptions _opt;

    public QrTokenService(IOptions<QrOptions> options)
    {
        _opt = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(_opt.Secret))
            throw new InvalidOperationException("QR Secret is not configured");

        if (_opt.RotationMinutes <= 0)
            throw new InvalidOperationException("QR RotationMinutes must be > 0");
    }

    public QrTokenDto GetCurrentToken(DateTime? nowUtc = null)
    {
        var now = nowUtc ?? DateTime.UtcNow;
        var slot = GetSlot(now, _opt.RotationMinutes);

        var token = BuildToken(slot);
        var expiresAt = GetSlotEndUtc(slot, _opt.RotationMinutes);

        return new QrTokenDto
        {
            Token = token,
            ExpiresAtUtc = expiresAt
        };
    }

    public QrValidationResultDto Validate(string token, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new QrValidationResultDto { IsValid = false, IsExpired = true };

        if (!TryParseToken(token, out var slotFromToken, out var sigFromToken))
            return new QrValidationResultDto { IsValid = false, IsExpired = true };

        var now = nowUtc ?? DateTime.UtcNow;
        var currentSlot = GetSlot(now, _opt.RotationMinutes);

        var isInWindow = slotFromToken == currentSlot || slotFromToken == (currentSlot - 1);
        if (!isInWindow)
            return new QrValidationResultDto { IsValid = false, IsExpired = true };

        var expectedSig = ComputeSignatureBytes(slotFromToken);
        var ok = FixedTimeEquals(sigFromToken, expectedSig);

        return new QrValidationResultDto
        {
            IsValid = ok,
            IsExpired = !ok ? true : false
        };
    }

    private string BuildToken(long slot)
    {
        var sig = ComputeSignatureBytes(slot);
        var sigB64 = Base64UrlEncode(sig);

        var payload = $"{slot}.{sigB64}";
        return Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
    }

    private bool TryParseToken(string token, out long slot, out byte[] signature)
    {
        slot = 0;
        signature = Array.Empty<byte>();

        byte[] raw;
        try
        {
            raw = Base64UrlDecode(token);
        }
        catch
        {
            return false;
        }

        var s = Encoding.UTF8.GetString(raw);
        var idx = s.IndexOf('.');
        if (idx <= 0 || idx == s.Length - 1) return false;

        var slotStr = s.Substring(0, idx);
        var sigStr = s.Substring(idx + 1);

        if (!long.TryParse(slotStr, out slot)) return false;

        try
        {
            signature = Base64UrlDecode(sigStr);
            if (signature.Length == 0) return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    private byte[] ComputeSignatureBytes(long slot)
    {
        var key = Encoding.UTF8.GetBytes(_opt.Secret);
        var msg = Encoding.UTF8.GetBytes(slot.ToString());

        using var h = new HMACSHA256(key);
        return h.ComputeHash(msg);
    }

    private static bool FixedTimeEquals(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static long GetSlot(DateTime nowUtc, int rotationMinutes)
    {
        var seconds = (long)(nowUtc - DateTime.UnixEpoch).TotalSeconds;
        var window = rotationMinutes * 60L;
        return seconds / window;
    }

    private static DateTime GetSlotEndUtc(long slot, int rotationMinutes)
    {
        var window = rotationMinutes * 60L;
        var endSeconds = ((slot + 1) * window);
        return DateTime.UnixEpoch.AddSeconds(endSeconds);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        var s = Convert.ToBase64String(bytes);
        return s.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
