using BarTasca.DTOs.Qr;

namespace BarTasca.Services.Interfaces;

public interface IQrTokenService
{
    QrTokenDto GetCurrentToken(DateTime? nowUtc = null);
    QrValidationResultDto Validate(string token, DateTime? nowUtc = null);
}
