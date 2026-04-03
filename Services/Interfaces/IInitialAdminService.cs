namespace BarTasca.Services.Interfaces
{
    public interface IInitialAdminService
    {
        Task EnsureInitialAdminAsync(CancellationToken ct = default);
    }
}