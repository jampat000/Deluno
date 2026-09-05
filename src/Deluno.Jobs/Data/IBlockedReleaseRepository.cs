using Deluno.Contracts;

namespace Deluno.Jobs.Data;

/// <summary>
/// The list of releases Deluno will not use again.
///
/// <para>Blocking is idempotent: the same release failing twice is one entry,
/// not two, and the first reason is kept. A person reading the list wants to
/// know what happened, and the second occurrence tells them nothing the first
/// did not.</para>
/// </summary>
public interface IBlockedReleaseRepository
{
    Task<BlockedRelease> BlockAsync(BlockedRelease release, CancellationToken cancellationToken);

    Task<IReadOnlyList<BlockedRelease>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The keys a search should skip. Returned as a set because a search asks
    /// this once and then tests every candidate against it.
    /// </summary>
    Task<IReadOnlySet<string>> ListKeysAsync(CancellationToken cancellationToken);

    Task<bool> UnblockAsync(string id, CancellationToken cancellationToken);
}
