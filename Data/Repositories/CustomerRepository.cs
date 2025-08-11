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
}
