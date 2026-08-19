using System.Data.Common;
using System.Text.Json;
using Deluno.Contracts;
using Deluno.Infrastructure.Storage;
using static Deluno.Infrastructure.Storage.SqliteRecordHelpers;

namespace Deluno.Libraries.Data;

public sealed class SqliteLibraryImportRunsRepository(
    IDelunoDatabaseConnectionFactory databaseConnectionFactory,
    TimeProvider timeProvider)
    : ILibraryImportRunsRepository
{
    /// <summary>How many titles are kept as a sample of what was found.</summary>
    private const int SampleTitleLimit = 8;

    private const string SelectColumns =
        """
        SELECT
            id, library_id, media_type, root_path, status, estimated_total,
            processed_count, imported_count, skipped_count, deferred_count,
            cursor_key, sample_titles, last_error,
            created_utc, started_utc, updated_utc, completed_utc
        FROM library_import_runs
        """;

    private const string ActiveRunPredicate =
        " WHERE library_id = @libraryId AND status IN ('queued', 'running', 'paused') LIMIT 1;";

    public async Task<LibraryImportRunItem> CreateOrGetActiveAsync(
        string libraryId,
        string libraryName,
        string mediaType,
        string rootPath,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var existing = await ReadOneAsync(
            connection,
            SelectColumns + ActiveRunPredicate,
            command => AddParameter(command, "@libraryId", libraryId),
            libraryName,
            cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var now = timeProvider.GetUtcNow();
        var id = Guid.CreateVersion7().ToString("N");

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                INSERT INTO library_import_runs (
                    id, library_id, media_type, root_path, status, estimated_total,
                    processed_count, imported_count, skipped_count, deferred_count,
                    cursor_key, sample_titles, last_error, created_utc, started_utc, updated_utc, completed_utc
                )
                VALUES (
                    @id, @libraryId, @mediaType, @rootPath, 'queued', 0,
                    0, 0, 0, 0,
                    NULL, NULL, NULL, @createdUtc, NULL, @updatedUtc, NULL
                );
                """;
            AddParameter(insert, "@id", id);
            AddParameter(insert, "@libraryId", libraryId);
            AddParameter(insert, "@mediaType", mediaType);
            AddParameter(insert, "@rootPath", rootPath);
            AddParameter(insert, "@createdUtc", now.ToString("O"));
            AddParameter(insert, "@updatedUtc", now.ToString("O"));

            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (DbException)
            {
                // The partial unique index rejected the insert, so another
                // caller won the race. Their run is the run.
                var raced = await ReadOneAsync(
                    connection,
                    SelectColumns + ActiveRunPredicate,
                    command => AddParameter(command, "@libraryId", libraryId),
                    libraryName,
                    cancellationToken);

                if (raced is not null)
                {
                    return raced;
                }

                throw;
            }
        }

        return (await ReadOneAsync(
            connection,
            SelectColumns + " WHERE id = @id;",
            command => AddParameter(command, "@id", id),
            libraryName,
            cancellationToken))!;
    }

    public async Task<LibraryImportRunItem?> GetAsync(string runId, string libraryName, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        return await ReadOneAsync(
            connection,
            SelectColumns + " WHERE id = @id;",
            command => AddParameter(command, "@id", runId),
            libraryName,
            cancellationToken);
    }

    public async Task<LibraryImportRunItem?> GetActiveForLibraryAsync(
        string libraryId,
        string libraryName,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        return await ReadOneAsync(
            connection,
            SelectColumns + ActiveRunPredicate,
            command => AddParameter(command, "@libraryId", libraryId),
            libraryName,
            cancellationToken);
    }

    public async Task<LibraryImportRunItem?> GetLatestForLibraryAsync(
        string libraryId,
        string libraryName,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        return await ReadOneAsync(
            connection,
            SelectColumns + " WHERE library_id = @libraryId ORDER BY created_utc DESC LIMIT 1;",
            command => AddParameter(command, "@libraryId", libraryId),
            libraryName,
            cancellationToken);
    }

    public async Task<bool> MarkRunningAsync(string runId, int estimatedTotal, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE library_import_runs
            SET status = 'running',
                estimated_total = @estimatedTotal,
                started_utc = COALESCE(started_utc, @now),
                updated_utc = @now,
                last_error = NULL
            WHERE id = @id
              AND status IN ('queued', 'running', 'paused');
            """;
        AddParameter(command, "@id", runId);
        AddParameter(command, "@estimatedTotal", estimatedTotal);
        AddParameter(command, "@now", now.ToString("O"));

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task RecordSliceAsync(
        string runId,
        string? cursor,
        int processedDelta,
        int importedDelta,
        int skippedDelta,
        int deferredDelta,
        IReadOnlyList<string> sampleTitles,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        string? mergedSamples = null;
        if (sampleTitles.Count > 0)
        {
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT sample_titles FROM library_import_runs WHERE id = @id;";
            AddParameter(read, "@id", runId);
            var current = ParseSamples(await read.ExecuteScalarAsync(cancellationToken) as string);

            if (current.Count < SampleTitleLimit)
            {
                mergedSamples = JsonSerializer.Serialize(current
                    .Concat(sampleTitles)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(SampleTitleLimit)
                    .ToArray());
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE library_import_runs
            SET cursor_key = @cursor,
                processed_count = processed_count + @processedDelta,
                imported_count = imported_count + @importedDelta,
                skipped_count = skipped_count + @skippedDelta,
                deferred_count = deferred_count + @deferredDelta,
                sample_titles = COALESCE(@sampleTitles, sample_titles),
                updated_utc = @now
            WHERE id = @id;
            """;
        AddParameter(command, "@id", runId);
        AddParameter(command, "@cursor", cursor);
        AddParameter(command, "@processedDelta", processedDelta);
        AddParameter(command, "@importedDelta", importedDelta);
        AddParameter(command, "@skippedDelta", skippedDelta);
        AddParameter(command, "@deferredDelta", deferredDelta);
        AddParameter(command, "@sampleTitles", mergedSamples);
        AddParameter(command, "@now", now.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> TrySetStatusAsync(
        string runId,
        string status,
        IReadOnlyList<string> allowedCurrentStatuses,
        string? lastError,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var terminal = !LibraryImportRunStatuses.IsActive(status);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        // Only the parameter names are generated, never the values, so the IN
        // list stays fully parameterised.
        var placeholders = string.Join(", ", allowedCurrentStatuses.Select((_, index) => "@allowed" + index));

        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE library_import_runs " +
            "SET status = @status, " +
            "    last_error = @lastError, " +
            "    completed_utc = CASE WHEN @terminal = 1 THEN @now ELSE NULL END, " +
            "    updated_utc = @now " +
            "WHERE id = @id " +
            "  AND status IN (" + placeholders + ");";
        AddParameter(command, "@id", runId);
        AddParameter(command, "@status", status);
        AddParameter(command, "@lastError", lastError);
        AddParameter(command, "@terminal", terminal ? 1 : 0);
        AddParameter(command, "@now", now.ToString("O"));
        for (var index = 0; index < allowedCurrentStatuses.Count; index++)
        {
            AddParameter(command, "@allowed" + index, allowedCurrentStatuses[index]);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task RecordIssueAsync(
        string runId,
        string libraryId,
        string sourcePath,
        string kind,
        string detail,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO library_import_issues (
                id, run_id, library_id, source_path, kind, detail, created_utc
            )
            VALUES (
                @id, @runId, @libraryId, @sourcePath, @kind, @detail, @createdUtc
            );
            """;
        AddParameter(command, "@id", Guid.CreateVersion7().ToString("N"));
        AddParameter(command, "@runId", runId);
        AddParameter(command, "@libraryId", libraryId);
        AddParameter(command, "@sourcePath", sourcePath);
        AddParameter(command, "@kind", kind);
        AddParameter(command, "@detail", detail);
        AddParameter(command, "@createdUtc", now.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryImportIssueItem>> ListIssuesAsync(
        string runId,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, run_id, library_id, source_path, kind, detail, created_utc
            FROM library_import_issues
            WHERE run_id = @runId
            ORDER BY created_utc ASC, id ASC
            LIMIT @take;
            """;
        AddParameter(command, "@runId", runId);
        AddParameter(command, "@take", Math.Clamp(take, 1, 500));

        var items = new List<LibraryImportIssueItem>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LibraryImportIssueItem(
                Id: reader.GetString(0),
                RunId: reader.GetString(1),
                LibraryId: reader.GetString(2),
                SourcePath: reader.GetString(3),
                Kind: reader.GetString(4),
                Detail: reader.GetString(5),
                CreatedUtc: ParseTimestamp(reader.GetString(6))));
        }

        return items;
    }

    private static async Task<LibraryImportRunItem?> ReadOneAsync(
        DbConnection connection,
        string sql,
        Action<DbCommand> bind,
        string libraryName,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        bind(command);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LibraryImportRunItem(
            Id: reader.GetString(0),
            LibraryId: reader.GetString(1),
            LibraryName: libraryName,
            MediaType: reader.GetString(2),
            RootPath: reader.GetString(3),
            Status: reader.GetString(4),
            EstimatedTotal: reader.GetInt32(5),
            ProcessedCount: reader.GetInt32(6),
            ImportedCount: reader.GetInt32(7),
            SkippedCount: reader.GetInt32(8),
            DeferredCount: reader.GetInt32(9),
            Cursor: reader.IsDBNull(10) ? null : reader.GetString(10),
            SampleTitles: ParseSamples(reader.IsDBNull(11) ? null : reader.GetString(11)),
            LastError: reader.IsDBNull(12) ? null : reader.GetString(12),
            CreatedUtc: ParseTimestamp(reader.GetString(13)),
            StartedUtc: reader.IsDBNull(14) ? null : ParseTimestamp(reader.GetString(14)),
            UpdatedUtc: ParseTimestamp(reader.GetString(15)),
            CompletedUtc: reader.IsDBNull(16) ? null : ParseTimestamp(reader.GetString(16)));
    }

    private static IReadOnlyList<string> ParseSamples(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
