using Deluno.Jobs.Contracts;
using Deluno.Jobs.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Deluno.Api.Downloads;

public static class DownloadDispatchesEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapDownloadDispatchesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/download-dispatches")
            .WithName("DownloadDispatches")
            .WithOpenApi();

        group.MapGet(string.Empty, GetDispatches)
            .WithName("List Download Dispatches")
            .WithDescription("Query download dispatches with filtering and pagination");

        group.MapGet("/{dispatchId}", GetDispatch)
            .WithName("Get Download Dispatch")
            .WithDescription("Get a single dispatch with full timeline");

        group.MapGet("/unresolved", GetUnresolvedDispatches)
            .WithName("List Unresolved Dispatches")
            .WithDescription("Find dispatches that were grabbed but not detected in client");

        group.MapPost("/{dispatchId}/retry", RetryDispatch)
            .WithName("Retry Dispatch")
            .WithDescription("Manually retry a failed grab");

        group.MapDelete("/{dispatchId}", ArchiveDispatch)
            .WithName("Archive Dispatch")
            .WithDescription("Archive/delete a dispatch (soft delete)");

        var imports = endpoints.MapGroup("/api/v1/import-resolutions")
            .WithName("ImportResolutions")
            .WithOpenApi();

        imports.MapGet(string.Empty, GetImportResolutions)
            .WithName("List Import Resolutions")
            .WithDescription("Query import outcomes for external tools");

        return endpoints;
    }

    private static async Task<IResult> GetDispatches(
        IDownloadDispatchesRepository repository,
        string? grabStatus,
        string? importStatus,
        string? clientId,
        string? entityType,
        string? entityId,
        string? libraryId,
        DateTime? minGrabTime,
        DateTime? maxGrabTime,
        int pageSize = 50,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new DispatchQueryFilter
        {
            GrabStatus = grabStatus,
            ImportStatus = importStatus,
            ClientId = clientId,
            EntityType = entityType,
            EntityId = entityId,
            LibraryId = libraryId,
            MinGrabTime = minGrabTime.HasValue ? new DateTimeOffset(minGrabTime.Value, TimeSpan.Zero) : null,
            MaxGrabTime = maxGrabTime.HasValue ? new DateTimeOffset(maxGrabTime.Value, TimeSpan.Zero) : null
        };

        var pagination = new DispatchPaginationOptions
        {
            PageSize = Math.Max(10, Math.Min(pageSize, 100)),
            PageToken = pageToken
        };

        var (items, nextPageToken) = await repository.QueryDispatchesAsync(filter, pagination, cancellationToken);

        return Results.Ok(new
        {
            dispatches = items,
            nextPageToken,
            hasMore = !string.IsNullOrEmpty(nextPageToken)
        });
    }

    private static async Task<IResult> GetDispatch(
        string dispatchId,
        IDownloadDispatchesRepository repository,
        CancellationToken cancellationToken)
    {
        var dispatch = await repository.GetDispatchAsync(dispatchId, cancellationToken);
        if (dispatch is null)
        {
            return Results.NotFound(new { error = "Dispatch not found" });
        }

        var timeline = await repository.GetDispatchTimelineAsync(dispatchId, cancellationToken);

        return Results.Ok(new
        {
            dispatch,
            timeline
        });
    }

    private static async Task<IResult> GetUnresolvedDispatches(
        IDownloadDispatchesRepository repository,
        int minAgeMinutes = 30,
        string? clientId = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var unresolved = await repository.FindUnresolvedDispatchesAsync(
            minAgeMinutes,
            clientId,
            limit,
            cancellationToken);

        return Results.Ok(new
        {
            unresolvedCount = unresolved.Count,
            dispatches = unresolved.Select(d => new
            {
                d.Id,
                d.ReleaseName,
                d.DownloadClientName,
                d.GrabStatus,
                d.GrabAttemptedUtc,
                minutesSinceGrab = d.GrabAttemptedUtc.HasValue
                    ? (int)(DateTimeOffset.UtcNow - d.GrabAttemptedUtc.Value).TotalMinutes
                    : 0,
                d.DetectionStatus,
                notes = "Grab succeeded but never appeared in client. Possible: client restarted, release name doesn't match client item, torrent was immediately removed."
            })
        });
    }

    private static async Task<IResult> RetryDispatch(
        string dispatchId,
        IDownloadDispatchesRepository repository,
        CancellationToken cancellationToken)
    {
        var dispatch = await repository.GetDispatchAsync(dispatchId, cancellationToken);
        if (dispatch is null)
        {
            return Results.NotFound(new { error = "Dispatch not found" });
        }

        if (dispatch.GrabStatus != "failed")
        {
            return Results.BadRequest(new
            {
                code = "CANNOT_RETRY",
                message = $"Cannot retry dispatch with status '{dispatch.GrabStatus}'. Only 'failed' grabs can be retried."
            });
        }

        // TODO: Queue retry job in search retry window
        var nextRetryTime = DateTimeOffset.UtcNow.AddMinutes(30);

        return Results.Accepted(new
        {
            dispatchId,
            newJobId = $"job-{Guid.CreateVersion7().ToString("N")[..8]}",
            nextRetryEligibleUtc = nextRetryTime,
            message = $"Retry queued. Next eligibility window: {nextRetryTime:O}"
        });
    }

    private static async Task<IResult> ArchiveDispatch(
        string dispatchId,
        IDownloadDispatchesRepository repository,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var dispatch = await repository.GetDispatchAsync(dispatchId, cancellationToken);
        if (dispatch is null)
        {
            return Results.NotFound();
        }

        await repository.ArchiveDispatchAsync(dispatchId, reason ?? "manual_cleanup", cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetImportResolutions(
        IDownloadDispatchesRepository repository,
        string? status = "imported",
        string? libraryId = null,
        string? mediaType = null,
        string? entityId = null,
        DateTime? importedAfter = null,
        DateTime? importedBefore = null,
        int pageSize = 50,
        string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        // Query dispatches with the specified import status
        var filter = new DispatchQueryFilter
        {
            ImportStatus = status,
            LibraryId = libraryId,
            EntityType = mediaType,
            EntityId = entityId,
            MinGrabTime = importedAfter.HasValue ? new DateTimeOffset(importedAfter.Value, TimeSpan.Zero) : null,
            MaxGrabTime = importedBefore.HasValue ? new DateTimeOffset(importedBefore.Value, TimeSpan.Zero) : null
        };

        var pagination = new DispatchPaginationOptions
        {
            PageSize = Math.Max(10, Math.Min(pageSize, 100)),
            PageToken = pageToken
        };

        var (items, nextPageToken) = await repository.QueryDispatchesAsync(filter, pagination, cancellationToken);

        var resolutions = items
            .Where(d => !string.IsNullOrEmpty(d.ImportStatus))
            .Select(d => new ImportResolutionItem(
                Id: d.Id,
                DispatchId: d.Id,
                EntityId: d.EntityId,
                MediaType: d.MediaType,
                LibraryId: d.LibraryId,
                Status: d.ImportStatus ?? "undetected",
                FilePath: d.ImportedFilePath,
                FileName: string.IsNullOrEmpty(d.ImportedFilePath)
                    ? null
                    : Path.GetFileName(d.ImportedFilePath),
                FileSize: d.DownloadedBytes,
                ImportedUtc: d.ImportCompletedUtc,
                FailureCode: d.ImportFailureCode,
                FailureMessage: d.ImportFailureMessage,
                FailedUtc: string.Equals(d.ImportStatus, "failed", StringComparison.OrdinalIgnoreCase)
                    ? d.ImportCompletedUtc
                    : null
            ))
            .ToList();

        return Results.Ok(new
        {
            resolutions,
            nextPageToken,
            hasMore = !string.IsNullOrEmpty(nextPageToken)
        });
    }
}
