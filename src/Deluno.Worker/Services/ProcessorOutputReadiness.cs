namespace Deluno.Worker.Services;

/// <summary>
/// Prevents the folder watcher from treating a file that is still being copied
/// into the processor output folder as an importable result.
/// </summary>
public static class ProcessorOutputReadiness
{
    private static readonly TimeSpan MinimumStableAge = TimeSpan.FromSeconds(2);

    public static bool IsReady(string path)
    {
        try
        {
            var before = new FileInfo(path);
            if (!before.Exists || before.Length == 0 || DateTime.UtcNow - before.LastWriteTimeUtc < MinimumStableAge)
            {
                return false;
            }

            // A processor that still has the file open exclusively will fail
            // here. FileShare.Read still permits safe inspection when the
            // processor is finished but another service has a read handle open.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length != before.Length)
                {
                    return false;
                }
            }

            var after = new FileInfo(path);
            return after.Exists &&
                   after.Length == before.Length &&
                   after.LastWriteTimeUtc == before.LastWriteTimeUtc;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
