using BarTasca.Data.Interfaces;
using BarTasca.Models;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Data.Repositories
{
    public class ServiceStateRepository : IServiceStateRepository
    {
        private readonly ColaDbContext _db;
        private const int SingletonId = 1;
        public ServiceStateRepository(ColaDbContext db)
        {
            _db = db;
        }

        public async Task<ServiceState> GetAsync(CancellationToken ct = default)
        {
            var state = await _db.ServiceStates.FindAsync([SingletonId], ct);

            if (state is not null) return state;

            state = new ServiceState
            {
                Id = SingletonId,
                IsOpen = false,
                UpdatedAt = DateTime.UtcNow
            };

                _db.ServiceStates.Add(state);

            try
            {
                await _db.SaveChangesAsync(ct);
                return state;
            }
            catch (DbUpdateException)
            {
                var existing = await _db.ServiceStates.FindAsync([SingletonId], ct);
                if (existing is not null) return existing;
                throw;
            }

        }
        public async Task<bool> SetOpenAsync(bool isOpen, CancellationToken ct = default)
        {
            var affectedRows = await _db.ServiceStates
                .Where(s => s.Id == SingletonId && s.IsOpen != isOpen)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(s => s.IsOpen, isOpen)
                        .SetProperty(s => s.UpdatedAt, DateTime.UtcNow),
                    ct
                );

            return affectedRows > 0;
        }
    }
}
