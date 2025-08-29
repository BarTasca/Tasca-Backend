using BarTasca.DTOs.Auth;

namespace BarTasca.Services.Interfaces
{
    public interface IStaffAuthService
    {
        Task<LoginResponseDto?> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
        string HashPassword(string password);
        bool VerifyPassword(string password, string storedHashBase64);
    }
}
