using Deluno.Quality;

namespace Deluno.Platform.Data;

/// <summary>
/// Adapts <see cref="IPlatformSettingsRepository"/>'s library mutation to
/// the port Deluno.Quality declares, so Quality can trigger it without a
/// circular project reference. Delete this once Libraries moves out of
/// Platform and Quality can depend on Deluno.Libraries directly.
/// </summary>
public sealed class PolicySetLibraryApplier(IPlatformSettingsRepository repository) : IPolicySetLibraryApplier
{
    public Task<int> ApplyToAssignedLibrariesAsync(string policySetId, CancellationToken cancellationToken)
        => repository.ApplyMediaPlanToAssignedLibrariesAsync(policySetId, cancellationToken);
}
