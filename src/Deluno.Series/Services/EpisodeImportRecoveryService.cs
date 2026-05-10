using Deluno.Series.Data;

namespace Deluno.Series.Services;

public sealed class EpisodeImportRecoveryService(ISeriesCatalogRepository repository) : IEpisodeImportRecoveryService
{
    public async Task<IReadOnlyList<string>> FindEpisodesNeedingRecoveryAsync(
        string libraryId,
        CancellationToken cancellationToken)
    {
        // TODO: Phase 5 - Implement episode recovery detection
        // This should:
        // 1. Find episodes with file but quality below target
        // 2. Score by import age (older = higher priority)
        // 3. Return episodeIds needing re-download
        return await Task.FromResult<IReadOnlyList<string>>(new List<string>());
    }

    public async Task<int> RecoveryPriorityAsync(string episodeId, CancellationToken cancellationToken)
    {
        // TODO: Phase 5 - Calculate recovery priority score
        // Episodes imported longer ago should have higher priority
        return await Task.FromResult(0);
    }
}
