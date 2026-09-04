namespace Deluno.Recovery.Policies;

/// <summary>
/// Whether a download that is still being shared is actually costing the user
/// any disk, and how to say so in plain words (#288).
///
/// After an import a title exists twice — once in the library, once in the
/// download client. Whether that is two copies or one depends entirely on
/// arrangement: on the same drive, with single-copy links in use, both names
/// point at one set of file data and sharing is free. On different drives it
/// cannot be, and every shared download is holding its own gigabytes.
///
/// Deluno has to know which, because otherwise the dashboard either frightens
/// people who are losing nothing or quietly lets a drive fill.
/// </summary>
public static class SharingFootprint
{
    /// <summary>
    /// True when the download client's copy and the library's are one set of
    /// file data. Unknown paths answer false: a guess that understates disk use
    /// is the one that lets a drive fill up silently.
    /// </summary>
    public static bool SharesOneCopy(string? downloadPath, string? libraryPath, bool useHardlinks)
        => useHardlinks && OnSameDrive(downloadPath, libraryPath) == true;

    /// <summary>
    /// What to tell someone about where their two copies live — and only when
    /// there is something to tell. Null means the arrangement is already the
    /// good one, or that Deluno cannot see enough to make a claim, and in both
    /// cases inventing a sentence would be noise.
    /// </summary>
    public static string? Describe(string? downloadPath, string? libraryPath, bool useHardlinks)
    {
        var sameDrive = OnSameDrive(downloadPath, libraryPath);
        if (sameDrive is null)
        {
            return null;
        }

        if (sameDrive == false)
        {
            var downloadDrive = DescribeDrive(downloadPath);
            var libraryDrive = DescribeDrive(libraryPath);
            return $"Your downloads land on {downloadDrive} and your library is on {libraryDrive}, so each one Deluno is still sharing takes its own space.";
        }

        return useHardlinks
            ? null
            : "These are on the same drive as your library, so sharing could cost nothing — turn on \"Keep seeding without a second copy\" under Media Management.";
    }

    /// <summary>
    /// Null when either path is missing or unreadable — which is a different
    /// answer from "no", and the caller has to be able to tell them apart.
    /// </summary>
    private static bool? OnSameDrive(string? first, string? second)
    {
        var firstRoot = RootOf(first);
        var secondRoot = RootOf(second);
        if (firstRoot is null || secondRoot is null)
        {
            return null;
        }

        return string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Which volume a path is on — which is not what <see cref="Path.GetPathRoot(string?)"/>
    /// answers, on either host.
    ///
    /// <para><b>A Windows path read by Linux.</b> <c>Path</c> answers for the
    /// machine it runs on, so on Linux <c>C:\Deluno</c> is a relative path and
    /// <c>GetFullPath</c> makes it <c>/somewhere/C:\Deluno</c> with root
    /// <c>/</c>. Two different drives then compared equal, and this reported
    /// that a download on <c>C:</c> and a library on <c>D:</c> were one set of
    /// file data. Deluno stores these paths, and the machine reading one back
    /// is not always the one that wrote it, so a drive letter is recognised by
    /// its shape.</para>
    ///
    /// <para><b>And a Linux path read by Linux.</b> Every POSIX path has root
    /// <c>/</c>, so that answer cannot tell two volumes apart at all — and in
    /// the container image, <c>/downloads</c> and <c>/media</c> being separate
    /// mounts is the normal arrangement, not an exotic one. Hardlinks do not
    /// cross a mount, so taking <c>/</c> at its word claimed every pair shared
    /// one copy: understating disk use, which this file exists to avoid. The
    /// volume is the mount point, so that is what is compared.</para>
    /// </summary>
    private static string? RootOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (WindowsRootOf(path) is { } windowsRoot)
        {
            return windowsRoot;
        }

        try
        {
            var full = Path.GetFullPath(path);
            if (MountPointOf(full, MountPoints()) is { } mountPoint)
            {
                return mountPoint;
            }

            var root = Path.GetPathRoot(full);
            return string.IsNullOrWhiteSpace(root) ? null : root;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }

    /// <summary>
    /// <c>C:\</c> from <c>C:\Deluno\Movies</c>, or <c>\\server\share</c> from a
    /// UNC path — read from the path's shape, so the answer does not change
    /// with the host. Null when the path is not Windows-shaped.
    /// </summary>
    internal static string? WindowsRootOf(string path)
    {
        if (path.Length >= 2 && path[1] == ':' && char.IsLetter(path[0]))
        {
            return string.Concat(char.ToUpperInvariant(path[0]), ":\\");
        }

        // Backslashes only. A UNC path written by Windows uses them, and a
        // POSIX path is allowed to begin with "//" — reading that as a share
        // would invent a volume out of a leading slash.
        if (path.Length > 2 && path[0] == '\\' && path[1] == '\\')
        {
            // A share is the volume: \\server\share. Anything deeper is a
            // folder on it.
            var parts = path[2..].Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            return parts.Length >= 2 ? $@"\\{parts[0]}\{parts[1]}" : null;
        }

        return null;
    }

    /// <summary>
    /// The longest mount point that contains <paramref name="fullPath"/>.
    ///
    /// <para>Separated from the enumeration so the choice can be tested without
    /// a machine that has the mounts on it: given <c>/</c> and <c>/media</c>,
    /// <c>/media/Movies</c> is on <c>/media</c>, and that is the whole rule.
    /// </para>
    /// </summary>
    internal static string? MountPointOf(string fullPath, IEnumerable<string> mountPoints)
    {
        string? best = null;
        foreach (var mountPoint in mountPoints)
        {
            if (string.IsNullOrEmpty(mountPoint) || !PathStartsWith(fullPath, mountPoint))
            {
                continue;
            }

            if (best is null || mountPoint.Length > best.Length)
            {
                best = mountPoint;
            }
        }

        return best;
    }

    private static bool PathStartsWith(string fullPath, string mountPoint)
    {
        var trimmed = mountPoint.TrimEnd('/');
        if (trimmed.Length == 0)
        {
            // The root mount contains everything.
            return fullPath.StartsWith('/');
        }

        return fullPath.Equals(trimmed, StringComparison.Ordinal) ||
               fullPath.StartsWith(trimmed + "/", StringComparison.Ordinal);
    }

    /// <summary>
    /// Where this machine has things mounted. Best effort: a machine that will
    /// not say falls back to the path root, which is what this did before.
    /// </summary>
    private static IEnumerable<string> MountPoints()
    {
        DriveInfo[] drives;
        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        var mountPoints = new List<string>(drives.Length);
        foreach (var drive in drives)
        {
            try
            {
                mountPoints.Add(drive.RootDirectory.FullName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A mount that cannot be described is one this cannot use.
            }
        }

        return mountPoints;
    }

    /// <summary>"C:" rather than "C:\" — how a person says it.</summary>
    private static string DescribeDrive(string? path)
    {
        var root = RootOf(path);
        if (root is null)
        {
            return "another drive";
        }

        // Both separators, not the host's: this root may be a Windows one that
        // a Linux container is reading back.
        var trimmed = root.TrimEnd('/', '\\');
        return string.IsNullOrWhiteSpace(trimmed) ? root : trimmed;
    }
}
