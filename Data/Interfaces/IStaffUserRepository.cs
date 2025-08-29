using BarTasca.Models;

namespace BarTasca.Data.Interfaces
{
    public interface IStaffUserRepository
    {
        Task<StaffUser?> GetByEmailAsync(string email, CancellationToken ct = default);
        Task AddAsync(StaffUser user, CancellationToken ct = default);
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
