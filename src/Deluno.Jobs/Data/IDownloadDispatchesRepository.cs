using Deluno.Jobs.Contracts;

namespace Deluno.Jobs.Data;

public interface IDownloadDispatchesRepository
{
    /// <summary>
    /// Get a single dispatch by ID with all grab, detection, and import outcome data.
    /// </summary>
    Task<DownloadDispatchItem?> GetDispatchAsync(
        string dispatchId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Record a grab outcome (called after DownloadClientGrabService.GrabAsync).
    /// </summary>
    Task<DownloadDispatchItem> RecordGrabAsync(
        string dispatchId,
        string grabStatus,
        int? grabResponseCode,
        string? grabMessage,
        string? grabFailureCode,
        string? grabResponseJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Record detection of a download in the client queue (from polling).
    /// </summary>
    Task<DownloadDispatchItem> RecordDetectionAsync(
        string dispatchId,
        string? torrentHashOrItemId,
        long? downloadedBytes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Record import outcome (success or failure).
    /// </summary>
    Task<DownloadDispatchItem> RecordImportOutcomeAsync(
        string dispatchId,
        string importStatus,
        string? importedFilePath,
        string? importFailureCode,
        string? importFailureMessage,
        CancellationToken cancellationToken);

    /// <summary>
    /// Query dispatches with filtering and pagination.
    /// </summary>
    Task<(IReadOnlyList<DownloadDispatchItem> Items, string? NextPageToken)> QueryDispatchesAsync(
        DispatchQueryFilter filter,
        DispatchPaginationOptions pagination,
        CancellationToken cancellationToken);

    /// <summary>
    /// Find dispatches that were grabbed but not detected in client.
    /// </summary>
    Task<IReadOnlyList<DownloadDispatchItem>> FindUnresolvedDispatchesAsync(
        int minAgeMinutes,
        string? clientId,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>
    /// Get full timeline of events for a dispatch.
    /// </summary>
    Task<IReadOnlyList<DispatchTimelineEvent>> GetDispatchTimelineAsync(
        string dispatchId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Record a timeline event for a dispatch.
    /// </summary>
    Task<DispatchTimelineEvent> RecordTimelineEventAsync(
        string dispatchId,
        string eventType,
        string? detailsJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Update circuit breaker state for a client.
    /// </summary>
    Task<DownloadDispatchItem> SetCircuitBreakerAsync(
        string dispatchId,
        DateTimeOffset? openUntilUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Archive/delete a dispatch (soft delete).
    /// </summary>
    Task ArchiveDispatchAsync(
        string dispatchId,
        string reason,
        CancellationToken cancellationToken);
}

public class DispatchQueryFilter
{
    public string? Status { get; set; }
    public string? GrabStatus { get; set; }
    public string? ImportStatus { get; set; }
    public string? ClientId { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? LibraryId { get; set; }
    public DateTimeOffset? MinGrabTime { get; set; }
    public DateTimeOffset? MaxGrabTime { get; set; }
}

public class DispatchPaginationOptions
{
    public int PageSize { get; set; } = 50;
    public string? PageToken { get; set; }
}
