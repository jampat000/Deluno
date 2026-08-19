namespace Deluno.Quality;

/// <summary>
/// Applies a policy set's fields to every library that has it as their
/// default policy set. Implemented in Deluno.Platform, which still owns
/// Libraries (ADR-001 Step 1 has not extracted it yet) -- this interface
/// exists so Quality's endpoints can trigger the side effect on save
/// without Quality referencing Platform, which would create a circular
/// project reference (Platform already references Quality).
/// </summary>
public interface IPolicySetLibraryApplier
{
    Task<int> ApplyToAssignedLibrariesAsync(string policySetId, CancellationToken cancellationToken);
}
