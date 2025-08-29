using BarTasca.Data.Interfaces;
using BarTasca.Models;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Data.Repositories
{
    public class StaffUserRepository : IStaffUserRepository
    {
        private readonly ColaDbContext _db;
        public StaffUserRepository(ColaDbContext db) => _db = db;

        public Task<StaffUser?> GetByEmailAsync(string email, CancellationToken ct = default)
            => _db.StaffUsers.FirstOrDefaultAsync(u => u.Email == email, ct);

        public Task AddAsync(StaffUser user, CancellationToken ct = default)
            => _db.StaffUsers.AddAsync(user, ct).AsTask();

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);
    }
}
