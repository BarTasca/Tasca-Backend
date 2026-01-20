using BarTasca.DTOs.ServiceState;

namespace BarTasca.Services.Interfaces
{
    public interface IServiceStateService
    {
        /// <summary>
        /// Gets the current service state.
        /// </summary>
        /// <param name="ct">Cancellation token</param>
        /// <returns>The current service state</returns>
        Task<ServiceStateDto> GetAsync(CancellationToken ct = default);

        /// <summary>
        /// Sets the service state to open or closed.
        /// </summary>
        /// <param name="dto">Service state update data</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>True if the operation was successful; otherwise, false.</returns>
        Task<bool> SetOpenAsync(UpdateServiceStateDto dto, CancellationToken ct = default);
    }
}
