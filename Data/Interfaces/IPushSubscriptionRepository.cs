using BarTasca.Models;

namespace BarTasca.Data.Interfaces;

public interface IPushSubscriptionRepository
{
    /// <summary>
    /// Gets a push subscription by its endpoint hash.
    /// </summary>
    /// <param name="endpointHash">Hash of the subscription endpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The push subscription if found; otherwise, null.</returns>
    Task<PushSubscription?> GetByEndpointHashAsync(byte[] endpointHash, CancellationToken ct = default);

    /// <summary>
    /// Lists active push subscriptions for a given ticket ID.
    /// </summary>
    /// <param name="ticketId">Ticket ID to filter subscriptions.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of active push subscriptions for the specified ticket.</returns>
    Task<IReadOnlyList<PushSubscription>> ListActiveByTicketIdAsync(int ticketId, CancellationToken ct = default);

    /// <summary>
    /// Creates or updates a push subscription for a given ticket. If a subscription with the same endpoint hash exists, it will be updated; otherwise, a new subscription will be created.
    /// </summary>
    /// <param name="ticketId"Ticket ID to associate with the subscription.</param>
    /// <param name="endpoint" Subscription endpoint URL.</param>
    /// <param name="p256dh" Client public key for encryption.</param>
    /// <param name="auth" Client authentication secret.</param>
    /// <param name="ct" Cancellation token.</param>
    /// <returns>Result indicating whether a subscription was created or updated.</returns>
    Task<UpsertPushSubscriptionResult> UpsertAsync(
        int ticketId,
        string endpoint,
        string p256dh,
        string auth,
        CancellationToken ct = default);

    /// <summary>
    /// Deactivates a push subscription by its endpoint. This method sets the subscription's IsActive property to false, effectively disabling it without deleting the record from the database.
    /// </summary>
    /// <param name="endpoint" Subscription endpoint URL to identify which subscription to deactivate.</param>
    /// <param name="ct" Cancellation token.</param>
    /// <returns>True if a subscription was found and deactivated; otherwise, false.</returns>
    Task<bool> DeactivateByEndpointAsync(string endpoint, CancellationToken ct = default);

    /// <summary>
    /// Deactivates all push subscriptions associated with a specific ticket ID. This method sets the IsActive property to false for all subscriptions linked to the given ticket, effectively disabling them without deleting the records from the database.
    /// </summary>
    /// <param name="ticketId" Ticket ID to identify which subscriptions to deactivate.</param> 
    /// <param name="ct" Cancellation token.</param>
    /// <returns>Number of subscriptions that were found and deactivated.</returns>
    Task<int> DeactivateByTicketIdAsync(int ticketId, CancellationToken ct = default);

    /// <summary>
    /// Saves changes to the data store. This method should be called after performing any create, update, or deactivate operations to persist the changes to the database.
    /// </summary>
    /// <param name="ct" Cancellation token.</param>
    /// <returns>Number of affected records.</returns>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Deactivates a push subscription by its endpoint hash. This method sets the subscription's IsActive property to false, effectively disabling it without deleting the record from the database.
    /// </summary>
    /// <param name="endpointHash" Hash of the subscription endpoint to identify which subscription to deactivate.</param>
    /// <param name="ct" Cancellation token.</param>    
    /// <returns>True if a subscription was found and deactivated; otherwise, false.</returns>
    Task<bool> DeactivateByEndpointHashAsync(byte[] endpointHash, CancellationToken ct = default);
}

public enum UpsertPushSubscriptionResult
{
    Created = 1,
    Updated = 2
}