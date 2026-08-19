using Deluno.Platform.Contracts;
using Deluno.Quality.Contracts;
using Deluno.Connections.Contracts;

namespace Deluno.Platform.Data;

public interface IPlatformSettingsRepository
{
    Task<PlatformSettingsSnapshot> GetAsync(CancellationToken cancellationToken);

    Task<SetupProgressItem> GetSetupProgressAsync(CancellationToken cancellationToken);

    Task<SetupProgressItem> SaveSetupProgressAsync(
        UpdateSetupProgressRequest request,
        CancellationToken cancellationToken);

    Task<SetupDraftItem> GetSetupDraftAsync(CancellationToken cancellationToken);

    Task<SetupDraftItem> SaveSetupDraftAsync(
        UpdateSetupDraftRequest request,
        CancellationToken cancellationToken);

    Task ClearSetupDraftAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadHealthRecord>> RecordDownloadHealthObservationsAsync(
        IReadOnlyList<DownloadHealthObservation> observations,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DownloadHealthRecord>> ListDownloadHealthRecordsAsync(
        int take,
        CancellationToken cancellationToken);

    Task<bool> IsDownloadReleaseBlockedAsync(
        string clientId,
        string releaseName,
        CancellationToken cancellationToken);

    Task<DownloadHealthRecord?> IgnoreDownloadHealthFindingAsync(
        string clientId,
        string queueItemId,
        string kind,
        int durationDays,
        CancellationToken cancellationToken);

    Task<string?> GetMetadataProviderSecretAsync(string provider, CancellationToken cancellationToken);

    Task<PlatformSettingsSnapshot> SaveAsync(
        UpdatePlatformSettingsRequest request,
        CancellationToken cancellationToken);

    Task<PlatformSettingsSnapshot> SetGlobalAutomationEnabledAsync(
        bool isEnabled,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LibraryItem>> ListLibrariesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TagItem>> ListTagsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DestinationRuleItem>> ListDestinationRulesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<LibraryViewItem>> ListLibraryViewsAsync(string userId, string variant, CancellationToken cancellationToken);

    Task<TagItem> CreateTagAsync(
        CreateTagRequest request,
        CancellationToken cancellationToken);

    Task<DestinationRuleItem> CreateDestinationRuleAsync(
        CreateDestinationRuleRequest request,
        CancellationToken cancellationToken);
    Task<LibraryViewItem> CreateLibraryViewAsync(
        string userId,
        CreateLibraryViewRequest request,
        CancellationToken cancellationToken);

    Task<TagItem?> UpdateTagAsync(
        string id,
        UpdateTagRequest request,
        CancellationToken cancellationToken);

    Task<DestinationRuleItem?> UpdateDestinationRuleAsync(
        string id,
        UpdateDestinationRuleRequest request,
        CancellationToken cancellationToken);
    Task<LibraryViewItem?> UpdateLibraryViewAsync(
        string userId,
        string id,
        UpdateLibraryViewRequest request,
        CancellationToken cancellationToken);

    Task<LibraryItem> CreateLibraryAsync(
        CreateLibraryRequest request,
        CancellationToken cancellationToken);

    Task<LibraryItem?> UpdateLibraryAutomationAsync(
        string id,
        UpdateLibraryAutomationRequest request,
        CancellationToken cancellationToken);

    Task<LibraryItem?> UpdateLibraryDetailsAsync(
        string id,
        UpdateLibraryDetailsRequest request,
        CancellationToken cancellationToken);

    Task<LibraryItem?> UpdateLibraryQualityProfileAsync(
        string id,
        UpdateLibraryQualityProfileRequest request,
        CancellationToken cancellationToken);

    Task<LibraryItem?> UpdateLibraryMediaPlanAsync(
        string id,
        UpdateLibraryMediaPlanRequest request,
        CancellationToken cancellationToken);

    Task<int> ApplyMediaPlanToAssignedLibrariesAsync(
        string policySetId,
        CancellationToken cancellationToken);

    Task<LibraryItem?> UpdateLibraryWorkflowAsync(
        string id,
        UpdateLibraryWorkflowRequest request,
        CancellationToken cancellationToken);

    Task<LibraryRoutingSnapshot?> GetLibraryRoutingAsync(string libraryId, CancellationToken cancellationToken);

    Task<LibraryRoutingSnapshot?> SaveLibraryRoutingAsync(
        string libraryId,
        UpdateLibraryRoutingRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteLibraryAsync(string id, CancellationToken cancellationToken);

    Task<bool> DeleteTagAsync(string id, CancellationToken cancellationToken);

    Task<bool> DeleteDestinationRuleAsync(string id, CancellationToken cancellationToken);
    Task<bool> DeleteLibraryViewAsync(string userId, string id, CancellationToken cancellationToken);

    Task<MigrationAuditReport> RecordMigrationAuditReportAsync(
        MigrationAuditReport report,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MigrationAuditReport>> ListMigrationAuditReportsAsync(
        int take,
        CancellationToken cancellationToken);

    Task<MigrationAuditReport?> GetMigrationAuditReportAsync(
        string id,
        CancellationToken cancellationToken);

    Task<ProcessorHandoffItem> EnsureProcessorHandoffAsync(
        CreateProcessorHandoffRequest request,
        CancellationToken cancellationToken);

    Task<ProcessorHandoffItem?> FindProcessorHandoffAsync(
        string libraryId,
        string? handoffId,
        string? sourcePath,
        CancellationToken cancellationToken);

    Task<ProcessorHandoffItem?> GetProcessorHandoffAsync(
        string id,
        CancellationToken cancellationToken);

    Task<ProcessorHandoffItem?> UpdateProcessorHandoffAsync(
        string id,
        string status,
        string? outputPath,
        string? importJobId,
        string? failureMessage,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessorHandoffItem>> ListProcessorHandoffsAsync(
        string? libraryId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessorConnectionItem>> ListProcessorConnectionsAsync(CancellationToken cancellationToken);

    Task<ProcessorConnectionItem?> GetProcessorConnectionAsync(string id, CancellationToken cancellationToken);

    Task<ProcessorConnectionItem?> FindProcessorConnectionByNameAsync(string? name, CancellationToken cancellationToken);

    Task<ProcessorConnectionItem> CreateProcessorConnectionAsync(
        CreateProcessorConnectionRequest request,
        CancellationToken cancellationToken);

    Task<ProcessorConnectionItem?> UpdateProcessorConnectionAsync(
        string id,
        UpdateProcessorConnectionRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteProcessorConnectionAsync(string id, CancellationToken cancellationToken);

    Task<ProcessorConnectionItem?> RecordProcessorConnectionHealthAsync(
        string id,
        string status,
        string? message,
        CancellationToken cancellationToken);

}
