using BarTasca.Models;

namespace BarTasca.Data.Interfaces
{
    public interface IServiceStateRepository
    {
        /// <summary>
        /// Gets the current service state.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The current service state</returns>
        Task<ServiceState> GetAsync(CancellationToken ct = default);
        /// <summary>
        /// Sets the service state to open or closed.
        /// </summary>
        /// <param name="isOpen">True to set the service as open; false to set it as closed.</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> SetOpenAsync(bool isOpen, CancellationToken ct = default);
    }
}
