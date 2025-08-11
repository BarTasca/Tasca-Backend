using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task AddAsync(Customer customer, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
