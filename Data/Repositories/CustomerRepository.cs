using BarTasca.Data.Interfaces;
using BarTasca.Models;
using Microsoft.EntityFrameworkCore;

namespace BarTasca.Data.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly ColaDbContext _db;

    public CustomerRepository(ColaDbContext db) => _db = db;

    public Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default)
        => _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Phone == phone, ct);

    public async Task AddAsync(Customer customer, CancellationToken ct = default)
        => await _db.Customers.AddAsync(customer, ct);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    public Task<List<Customer>> ListForAnonymizationAsync(DateTime anonymizeBeforeUtc, int take, CancellationToken ct = default) =>
        _db.Customers
              .AsTracking()
              .Where(c => !c.IsAnonymized && c.CreatedAt <= anonymizeBeforeUtc)
              .OrderBy(c => c.Id)
              .Take(take)
              .ToListAsync(ct);

    public async Task UpdateNameAsync(int id, string newName, CancellationToken ct = default)
    {
        var customer = await _db.Customers.FindAsync(new object[] { id }, ct);
        if (customer != null)
        {
            customer.FullName = newName;
            await _db.SaveChangesAsync(ct);
        }
    }
}
