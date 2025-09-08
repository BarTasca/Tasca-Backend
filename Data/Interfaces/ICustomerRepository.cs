using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

/// <summary>
/// Provides methods to manage Customer entities.
/// </summary>
public interface ICustomerRepository
{
    /// <summary>
    /// Gets a customer by phone number.
    /// </summary>
    /// <param name="phone">The customer's phone number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The customer if found; otherwise, null.</returns>
    Task<Customer?> GetByPhoneAsync(string phone, CancellationToken ct = default);

    /// <summary>
    /// Adds a new customer.
    /// </summary>
    /// <param name="customer">The customer to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Customer customer, CancellationToken ct = default);

    /// <summary>
    /// Saves changes to the data store.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Lists customers eligible for anonymization.
    /// </summary>
    /// <param name="anonymizeBeforeUtc"></param>
    /// <param name="take"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<List<Customer>> ListForAnonymizationAsync(DateTime anonymizeBeforeUtc, int take, CancellationToken ct = default);

}
