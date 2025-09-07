namespace BarTasca.Services.Interfaces
{
    public interface ITicketAuthService
    {
        Task<string?> GenerateTokenAsync(string publicId, CancellationToken ct = default);
    }
}
