using Deluno.Realtime;

namespace Deluno.Persistence.Tests.Support;

internal sealed class NullRealtimeEventPublisher : IRealtimeEventPublisher
{
    public Task PublishHealthChangedAsync(
        string source,
        string status,
        string message,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishDownloadProgressAsync(
        string id,
        string title,
        double progress,
        double speedMbps,
        string? eta,
        string status,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishActivityEventAddedAsync(
        string id,
        string message,
        string category,
        string severity,
        string createdUtc,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishQueueItemAddedAsync(
        string id,
        string title,
        string type,
        string status,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishQueueItemRemovedAsync(string id, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishQueueItemStatusChangedAsync(
        string id,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishSearchRunCompletedAsync(
        string libraryId,
        string libraryName,
        string mediaType,
        int plannedCount,
        int queuedCount,
        int skippedCount,
        string completedUtc,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    public Task PublishImportStateChangedAsync(
        string jobId,
        string state,
        string? entityType,
        string? entityId,
        string? title,
        string? errorMessage,
        string changedUtc,
        CancellationToken cancellationToken)
        => Task.CompletedTask;
}
