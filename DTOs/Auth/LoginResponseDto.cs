namespace BarTasca.DTOs.Auth
{
    public sealed class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
    }
}
