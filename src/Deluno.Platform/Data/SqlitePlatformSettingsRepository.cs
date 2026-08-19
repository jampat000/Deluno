using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Deluno.Infrastructure.Storage;
using Deluno.Platform.Contracts;
using Deluno.Security;
using Deluno.Security.Contracts;
using static Deluno.Infrastructure.Storage.SqliteRecordHelpers;
using static Deluno.Contracts.DelunoValueNormalizers;
using Deluno.Quality.Contracts;
using Deluno.Quality.Data;
using Deluno.Connections.Contracts;

namespace Deluno.Platform.Data;

public sealed class SqlitePlatformSettingsRepository(
    IDelunoDatabaseConnectionFactory databaseConnectionFactory,
    TimeProvider timeProvider,
    ISecretProtector secretProtector)
    : IPlatformSettingsRepository
{
    // Normal Deluno installs use the managed metadata gateway. Direct provider
    // credentials remain a legacy/operator compatibility path, never a first-run
    // requirement for a media-library user.
    private const string ManagedMetadataBrokerUrl = "https://deluno-metadata-gateway.ejmdigital.workers.dev";

    private const string DownloadHealthRecordsSettingKey = "download-health.records.v1";
    private static readonly TimeSpan DownloadHealthStrikeWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan DownloadHealthRetention = TimeSpan.FromDays(90);
    public async Task<PlatformSettingsSnapshot> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var roots = await ReadRootsAsync(connection, cancellationToken);
        return CreateSnapshot(settings, roots);
    }

    public async Task<SetupProgressItem> GetSetupProgressAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var settings = await ReadSettingsAsync(connection, cancellationToken);
        return CreateSetupProgress(settings);
    }

    public async Task<SetupProgressItem> SaveSetupProgressAsync(
        UpdateSetupProgressRequest request,
        CancellationToken cancellationToken)
    {
        var updatedUtc = timeProvider.GetUtcNow();
        var lastCompletedStep = Math.Clamp(request.LastCompletedStep, 0, 4);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await UpsertSettingAsync(connection, transaction, "setup.lastCompletedStep", lastCompletedStep.ToString(CultureInfo.InvariantCulture), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "setup.isSkipped", request.IsSkipped ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "setup.isCompleted", request.IsCompleted ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "setup.updatedUtc", updatedUtc.ToString("O"), updatedUtc, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SetupProgressItem(lastCompletedStep, request.IsSkipped, request.IsCompleted, updatedUtc);
    }

    public async Task<SetupDraftItem> GetSetupDraftAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var settings = await ReadSettingsAsync(connection, cancellationToken);
        if (!settings.TryGetValue("setup.draft.v1", out var json) || string.IsNullOrWhiteSpace(json))
        {
            return new SetupDraftItem();
        }

        try
        {
            return JsonSerializer.Deserialize<SetupDraftItem>(json) ?? new SetupDraftItem();
        }
        catch (JsonException)
        {
            return new SetupDraftItem();
        }
    }

    public async Task<SetupDraftItem> SaveSetupDraftAsync(
        UpdateSetupDraftRequest request,
        CancellationToken cancellationToken)
    {
        var updatedUtc = timeProvider.GetUtcNow();
        var draft = new SetupDraftItem(
            Mode: NormalizeSetupChoice(request.Mode, "simple", "simple", "advanced"),
            MediaIntent: NormalizeSetupChoice(request.MediaIntent, "both", "movies", "tv", "both"),
            MovieRootPath: NormalizeSetupText(request.MovieRootPath),
            SeriesRootPath: NormalizeSetupText(request.SeriesRootPath),
            DownloadsPath: NormalizeSetupText(request.DownloadsPath),
            QualityPreset: NormalizeSetupChoice(request.QualityPreset, "", "", "balanced1080p", "premium4k"),
            FormatGoal: NormalizeSetupChoice(request.FormatGoal, "", "", "simpleClean", "balanced", "homeTheater", "storageSaver", "anime"),
            IndexerName: NormalizeSetupText(request.IndexerName),
            IndexerProtocol: NormalizeSetupChoice(request.IndexerProtocol, "torznab", "torznab", "newznab", "rss"),
            IndexerUrl: NormalizeSetupText(request.IndexerUrl),
            ClientName: NormalizeSetupText(request.ClientName),
            ClientProtocol: NormalizeSetupChoice(request.ClientProtocol, "qbittorrent", "qbittorrent", "sabnzbd", "transmission", "deluge", "nzbget", "utorrent"),
            ClientHost: NormalizeSetupText(request.ClientHost),
            ClientPort: NormalizeSetupText(request.ClientPort),
            MetadataProviderMode: NormalizeSetupChoice(request.MetadataProviderMode, "direct", "broker", "hybrid", "direct"),
            MetadataBrokerUrl: NormalizeSetupText(request.MetadataBrokerUrl),
            BackupEnabled: request.BackupEnabled,
            FirstTitleType: NormalizeSetupChoice(request.FirstTitleType, "movies", "movies", "tv"),
            FirstTitle: NormalizeSetupText(request.FirstTitle),
            FirstTitleYear: NormalizeSetupText(request.FirstTitleYear),
            FirstTitleMonitored: request.FirstTitleMonitored,
            UpdatedUtc: updatedUtc);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await UpsertSettingAsync(connection, transaction, "setup.draft.v1", JsonSerializer.Serialize(draft), updatedUtc, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return draft;
    }

    public async Task ClearSetupDraftAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM system_settings WHERE setting_key = @key;";
        AddParameter(command, "@key", "setup.draft.v1");
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DownloadHealthRecord>> RecordDownloadHealthObservationsAsync(
        IReadOnlyList<DownloadHealthObservation> observations,
        CancellationToken cancellationToken)
    {
        if (observations.Count == 0)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(DelunoDatabaseNames.Platform, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var records = ReadDownloadHealthRecords(settings).Where(record => now - record.LastObservedUtc <= DownloadHealthRetention).ToList();
        var touched = new List<DownloadHealthRecord>();

        foreach (var observation in observations)
        {
            if (string.IsNullOrWhiteSpace(observation.ClientId) || string.IsNullOrWhiteSpace(observation.QueueItemId) ||
                string.IsNullOrWhiteSpace(observation.ReleaseName) || string.IsNullOrWhiteSpace(observation.Kind))
            {
                continue;
            }

            var releaseKey = NormalizeDownloadReleaseKey(observation.ReleaseName);
            var index = records.FindIndex(record =>
                string.Equals(record.ClientId, observation.ClientId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(record.QueueItemId, observation.QueueItemId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(record.Kind, observation.Kind, StringComparison.OrdinalIgnoreCase));
            var record = index >= 0 ? records[index] : null;
            var strikes = record is null ? 1 : record.LastObservedUtc <= now - DownloadHealthStrikeWindow ? record.StrikeCount + 1 : record.StrikeCount;
            var updated = new DownloadHealthRecord(
                observation.ClientId.Trim(), observation.QueueItemId.Trim(), observation.ReleaseName.Trim(), releaseKey,
                observation.Kind.Trim(), observation.Severity.Trim(), SanitizeDownloadHealthEvidence(observation.Evidence),
                record?.FirstObservedUtc ?? now, now, strikes, record?.IgnoredUntilUtc);

            if (index >= 0) records[index] = updated; else records.Add(updated);
            touched.Add(updated);
        }

        await UpsertSettingAsync(connection, transaction, DownloadHealthRecordsSettingKey, JsonSerializer.Serialize(records), now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return touched;
    }

    public async Task<IReadOnlyList<DownloadHealthRecord>> ListDownloadHealthRecordsAsync(int take, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(DelunoDatabaseNames.Platform, cancellationToken);
        return ReadDownloadHealthRecords(await ReadSettingsAsync(connection, cancellationToken))
            .Where(record => now - record.LastObservedUtc <= DownloadHealthRetention)
            .OrderByDescending(record => record.LastObservedUtc)
            .Take(Math.Clamp(take, 1, 200))
            .ToArray();
    }

    public async Task<bool> IsDownloadReleaseBlockedAsync(string clientId, string releaseName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(releaseName)) return false;

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(DelunoDatabaseNames.Platform, cancellationToken);
        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var records = ReadDownloadHealthRecords(settings);
        var releaseKey = NormalizeDownloadReleaseKey(releaseName);
        var now = timeProvider.GetUtcNow();
        var threshold = ReadDownloadHealthStrikeThreshold(settings);
        if (string.Equals(GetValue(settings, "cleanup.blockReleaseAfterThreshold"), "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return records.Any(record =>
            string.Equals(record.ClientId, clientId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.ReleaseKey, releaseKey, StringComparison.Ordinal) &&
            record.BlocksCandidate(now, threshold));
    }

    public async Task<DownloadHealthRecord?> IgnoreDownloadHealthFindingAsync(
        string clientId,
        string queueItemId,
        string kind,
        int durationDays,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(DelunoDatabaseNames.Platform, cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var records = ReadDownloadHealthRecords(settings).ToList();
        var index = records.FindIndex(record =>
            string.Equals(record.ClientId, clientId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.QueueItemId, queueItemId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.Kind, kind, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return null;

        var updated = records[index] with { IgnoredUntilUtc = now.AddDays(Math.Clamp(durationDays, 1, 30)) };
        records[index] = updated;
        await UpsertSettingAsync(connection, transaction, DownloadHealthRecordsSettingKey, JsonSerializer.Serialize(records), now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<PlatformSettingsSnapshot> SaveAsync(
        UpdatePlatformSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var updatedUtc = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await UpsertSettingAsync(connection, transaction, "app.instanceName", NormalizeName(request.AppInstanceName) ?? "Deluno", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "jobs.autoStart", request.AutoStartJobs ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "notifications.enabled", request.EnableNotifications ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.renameOnImport", request.RenameOnImport ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.useHardlinks", request.UseHardlinks ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.cleanupEmptyFolders", request.CleanupEmptyFolders ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.removeCompletedDownloads", request.RemoveCompletedDownloads ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.unmonitorWhenCutoffMet", request.UnmonitorWhenCutoffMet ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.movieFolderFormat", NormalizeName(request.MovieFolderFormat) ?? "{Movie Title} ({Release Year})", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.seriesFolderFormat", NormalizeName(request.SeriesFolderFormat) ?? "{Series Title} ({Series Year})", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.episodeFileFormat", NormalizeName(request.EpisodeFileFormat) ?? "{Series Title} - S{season:00}E{episode:00} - {Episode Title}", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "host.bindAddress", NormalizeName(request.HostBindAddress) ?? "127.0.0.1", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "host.port", NormalizePositiveValue(request.HostPort, 5099).ToString(CultureInfo.InvariantCulture), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "host.urlBase", NormalizeName(request.UrlBase) ?? string.Empty, updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "security.requireAuthentication", "true", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "ui.theme", NormalizeUiTheme(request.UiTheme), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "ui.density", NormalizeUiDensity(request.UiDensity), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "ui.defaultMovieView", NormalizeUiView(request.DefaultMovieView), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "ui.defaultShowView", NormalizeUiView(request.DefaultShowView), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "metadata.nfoEnabled", request.MetadataNfoEnabled ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "metadata.artworkEnabled", request.MetadataArtworkEnabled ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "metadata.certificationCountry", NormalizeName(request.MetadataCertificationCountry) ?? "US", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "metadata.language", NormalizeName(request.MetadataLanguage) ?? "en", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "metadata.providerMode", NormalizeMetadataProviderMode(request.MetadataProviderMode), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "metadata.brokerUrl", NormalizeMetadataBrokerUrl(request.MetadataBrokerUrl) ?? string.Empty, updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "search.neverGrabPatterns", NormalizeNeverGrabPatterns(request.ReleaseNeverGrabPatterns), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "search.scoringMode", SearchScoringModes.Normalize(request.SearchScoringMode), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "media.importRecoveryRetentionDays", NormalizePositiveValue(request.ImportRecoveryRetentionDays ?? 30, 30).ToString(CultureInfo.InvariantCulture), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "cleanup.strikeThreshold", Math.Clamp(request.DownloadHealthStrikeThreshold ?? 3, 1, 20).ToString(CultureInfo.InvariantCulture), updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "cleanup.blockReleaseAfterThreshold", request.CleanupBlockReleaseAfterThreshold is false ? "false" : "true", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "cleanup.queueReplacementAfterThreshold", request.CleanupQueueReplacementAfterThreshold is false ? "false" : "true", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "cleanup.removeClientEntryAfterThreshold", request.CleanupRemoveClientEntryAfterThreshold == true ? "true" : "false", updatedUtc, cancellationToken);
        await UpsertSettingAsync(connection, transaction, "cleanup.purgePayloadAfterThreshold", request.CleanupPurgePayloadAfterThreshold == true ? "true" : "false", updatedUtc, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.MetadataTmdbApiKey))
        {
            await UpsertSettingAsync(
                connection,
                transaction,
                "metadata.tmdbApiKey",
                secretProtector.Protect("metadata:tmdb", request.MetadataTmdbApiKey.Trim()),
                updatedUtc,
                cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(request.MetadataOmdbApiKey))
        {
            await UpsertSettingAsync(
                connection,
                transaction,
                "metadata.omdbApiKey",
                secretProtector.Protect("metadata:omdb", request.MetadataOmdbApiKey.Trim()),
                updatedUtc,
                cancellationToken);
        }
        if (!string.IsNullOrWhiteSpace(request.MdbListApiKey))
        {
            await UpsertSettingAsync(
                connection,
                transaction,
                "intake.mdblistApiKey",
                secretProtector.Protect("intake:mdblist", request.MdbListApiKey.Trim()),
                updatedUtc,
                cancellationToken);
        }

        await UpsertRootAsync(connection, transaction, "movies", NormalizePath(request.MovieRootPath), updatedUtc, cancellationToken);
        await UpsertRootAsync(connection, transaction, "series", NormalizePath(request.SeriesRootPath), updatedUtc, cancellationToken);
        await UpsertRootAsync(connection, transaction, "downloads", NormalizePath(request.DownloadsPath), updatedUtc, cancellationToken);
        await UpsertRootAsync(connection, transaction, "downloads.incomplete", NormalizePath(request.IncompleteDownloadsPath), updatedUtc, cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var roots = await ReadRootsAsync(connection, cancellationToken);
        return CreateSnapshot(settings, roots);
    }

    public async Task<PlatformSettingsSnapshot> SetGlobalAutomationEnabledAsync(
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var updatedUtc = timeProvider.GetUtcNow();
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await UpsertSettingAsync(connection, transaction, "jobs.autoStart", isEnabled ? "true" : "false", updatedUtc, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var settings = await ReadSettingsAsync(connection, cancellationToken);
        var roots = await ReadRootsAsync(connection, cancellationToken);
        return CreateSnapshot(settings, roots);
    }

    public async Task<string?> GetMetadataProviderSecretAsync(string provider, CancellationToken cancellationToken)
    {
        var settingKey = provider.Trim().ToLowerInvariant() switch
        {
            "tmdb" => "metadata.tmdbApiKey",
            "omdb" => "metadata.omdbApiKey",
            "mdblist" => "intake.mdblistApiKey",
            _ => null
        };

        if (settingKey is null)
        {
            return null;
        }

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT setting_value FROM system_settings WHERE setting_key = @settingKey;";
        AddParameter(command, "@settingKey", settingKey);
        var stored = await command.ExecuteScalarAsync(cancellationToken) as string;
        var purpose = string.Equals(provider, "mdblist", StringComparison.OrdinalIgnoreCase) ? "intake:mdblist" : $"metadata:{provider.Trim().ToLowerInvariant()}";
        return secretProtector.Unprotect(purpose, stored);
    }

    public async Task<IReadOnlyList<LibraryItem>> ListLibrariesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        await BackfillLibraryQualityProfilesAsync(connection, cancellationToken);

        var items = new List<LibraryItem>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.id, l.name, l.media_type, l.purpose, l.root_path, l.downloads_path,
                l.quality_profile_id, q.name, q.cutoff_quality, q.upgrade_until_cutoff, q.upgrade_unknown_items,
                l.import_workflow, l.processor_name, l.processor_output_path, l.processor_timeout_minutes, l.processor_failure_mode,
                l.auto_search_enabled, l.missing_search_enabled, l.upgrade_search_enabled, l.search_interval_hours,
                l.retry_delay_hours, l.max_items_per_run, l.search_window_start_hour, l.search_window_end_hour,
                l.created_utc, l.updated_utc, l.default_policy_set_id, p.name
            FROM libraries l
            LEFT JOIN quality_profiles q ON q.id = l.quality_profile_id
            LEFT JOIN policy_sets p ON p.id = l.default_policy_set_id
            ORDER BY l.media_type ASC, l.name ASC;
            """;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadLibrary(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<TagItem>> ListTagsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var items = new List<TagItem>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, name, color, description, created_utc, updated_utc
            FROM tags
            ORDER BY name COLLATE NOCASE ASC;
            """;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadTag(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<DestinationRuleItem>> ListDestinationRulesAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var items = new List<DestinationRuleItem>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, name, media_type, match_kind, match_value, root_path, folder_template,
                priority, is_enabled, created_utc, updated_utc
            FROM destination_rules
            ORDER BY media_type ASC, priority ASC, name COLLATE NOCASE ASC;
            """;

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadDestinationRule(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<LibraryViewItem>> ListLibraryViewsAsync(
        string userId,
        string variant,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var items = new List<LibraryViewItem>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, user_id, variant, name, quick_filter, sort_field, sort_direction,
                view_mode, card_size, display_options_json, rules_json, created_utc, updated_utc
            FROM library_views
            WHERE user_id = @userId AND variant = @variant
            ORDER BY name COLLATE NOCASE ASC;
            """;
        AddParameter(command, "@userId", userId);
        AddParameter(command, "@variant", NormalizeLibraryViewVariant(variant));

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadLibraryView(reader));
        }

        return items;
    }

    public async Task<TagItem> CreateTagAsync(
        CreateTagRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var item = new TagItem(
            Id: Guid.CreateVersion7().ToString("N"),
            Name: NormalizeName(request.Name) ?? "New tag",
            Color: NormalizeTagColor(request.Color),
            Description: NormalizeName(request.Description) ?? string.Empty,
            CreatedUtc: now,
            UpdatedUtc: now);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO tags (
                id, name, color, description, created_utc, updated_utc
            )
            VALUES (
                @id, @name, @color, @description, @createdUtc, @updatedUtc
            );
            """;

        AddParameter(command, "@id", item.Id);
        AddParameter(command, "@name", item.Name);
        AddParameter(command, "@color", item.Color);
        AddParameter(command, "@description", item.Description);
        AddParameter(command, "@createdUtc", item.CreatedUtc.ToString("O"));
        AddParameter(command, "@updatedUtc", item.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return item;
    }

    public async Task<DestinationRuleItem> CreateDestinationRuleAsync(
        CreateDestinationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var item = new DestinationRuleItem(
            Id: Guid.CreateVersion7().ToString("N"),
            Name: NormalizeName(request.Name) ?? "New destination rule",
            MediaType: NormalizeMediaType(request.MediaType),
            MatchKind: NormalizeDestinationMatchKind(request.MatchKind),
            MatchValue: NormalizeName(request.MatchValue) ?? string.Empty,
            RootPath: NormalizePath(request.RootPath) ?? string.Empty,
            FolderTemplate: NormalizeName(request.FolderTemplate),
            Priority: NormalizePriorityValue(request.Priority),
            IsEnabled: request.IsEnabled,
            CreatedUtc: now,
            UpdatedUtc: now);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO destination_rules (
                id, name, media_type, match_kind, match_value, root_path, folder_template,
                priority, is_enabled, created_utc, updated_utc
            )
            VALUES (
                @id, @name, @mediaType, @matchKind, @matchValue, @rootPath, @folderTemplate,
                @priority, @isEnabled, @createdUtc, @updatedUtc
            );
            """;

        AddParameter(command, "@id", item.Id);
        AddParameter(command, "@name", item.Name);
        AddParameter(command, "@mediaType", item.MediaType);
        AddParameter(command, "@matchKind", item.MatchKind);
        AddParameter(command, "@matchValue", item.MatchValue);
        AddParameter(command, "@rootPath", item.RootPath);
        AddParameter(command, "@folderTemplate", item.FolderTemplate);
        AddParameter(command, "@priority", item.Priority);
        AddParameter(command, "@isEnabled", item.IsEnabled ? 1 : 0);
        AddParameter(command, "@createdUtc", item.CreatedUtc.ToString("O"));
        AddParameter(command, "@updatedUtc", item.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return item;
    }

    public async Task<LibraryViewItem> CreateLibraryViewAsync(
        string userId,
        CreateLibraryViewRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var item = new LibraryViewItem(
            Id: Guid.CreateVersion7().ToString("N"),
            UserId: userId,
            Variant: NormalizeLibraryViewVariant(request.Variant),
            Name: NormalizeName(request.Name) ?? "New view",
            QuickFilter: NormalizeName(request.QuickFilter) ?? "all",
            SortField: NormalizeName(request.SortField) ?? "title",
            SortDirection: NormalizeSortDirection(request.SortDirection),
            ViewMode: NormalizeUiView(request.ViewMode),
            CardSize: NormalizeCardSize(request.CardSize),
            DisplayOptionsJson: NormalizeJson(request.DisplayOptionsJson, "{}"),
            RulesJson: NormalizeJson(request.RulesJson, "[]"),
            CreatedUtc: now,
            UpdatedUtc: now);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO library_views (
                id, user_id, variant, name, quick_filter, sort_field, sort_direction,
                view_mode, card_size, display_options_json, rules_json, created_utc, updated_utc
            )
            VALUES (
                @id, @userId, @variant, @name, @quickFilter, @sortField, @sortDirection,
                @viewMode, @cardSize, @displayOptionsJson, @rulesJson, @createdUtc, @updatedUtc
            );
            """;
        AddParameter(command, "@id", item.Id);
        AddParameter(command, "@userId", item.UserId);
        AddParameter(command, "@variant", item.Variant);
        AddParameter(command, "@name", item.Name);
        AddParameter(command, "@quickFilter", item.QuickFilter);
        AddParameter(command, "@sortField", item.SortField);
        AddParameter(command, "@sortDirection", item.SortDirection);
        AddParameter(command, "@viewMode", item.ViewMode);
        AddParameter(command, "@cardSize", item.CardSize);
        AddParameter(command, "@displayOptionsJson", item.DisplayOptionsJson);
        AddParameter(command, "@rulesJson", item.RulesJson);
        AddParameter(command, "@createdUtc", item.CreatedUtc.ToString("O"));
        AddParameter(command, "@updatedUtc", item.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return item;
    }

    public async Task<TagItem?> UpdateTagAsync(
        string id,
        UpdateTagRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var current = await GetTagAsync(connection, id, cancellationToken);
        if (current is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE tags
            SET
                name = @name,
                color = @color,
                description = @description,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@name", NormalizeName(request.Name) ?? current.Name);
        AddParameter(command, "@color", NormalizeTagColor(request.Color ?? current.Color));
        AddParameter(command, "@description", NormalizeName(request.Description) ?? string.Empty);
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetTagAsync(connection, id, cancellationToken);
    }

    public async Task<DestinationRuleItem?> UpdateDestinationRuleAsync(
        string id,
        UpdateDestinationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var current = await GetDestinationRuleAsync(connection, id, cancellationToken);
        if (current is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE destination_rules
            SET
                name = @name,
                media_type = @mediaType,
                match_kind = @matchKind,
                match_value = @matchValue,
                root_path = @rootPath,
                folder_template = @folderTemplate,
                priority = @priority,
                is_enabled = @isEnabled,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@name", NormalizeName(request.Name) ?? current.Name);
        AddParameter(command, "@mediaType", NormalizeMediaType(request.MediaType ?? current.MediaType));
        AddParameter(command, "@matchKind", NormalizeDestinationMatchKind(request.MatchKind ?? current.MatchKind));
        AddParameter(command, "@matchValue", NormalizeName(request.MatchValue) ?? string.Empty);
        AddParameter(command, "@rootPath", NormalizePath(request.RootPath) ?? current.RootPath);
        AddParameter(command, "@folderTemplate", NormalizeName(request.FolderTemplate));
        AddParameter(command, "@priority", NormalizePriorityValue(request.Priority));
        AddParameter(command, "@isEnabled", request.IsEnabled ? 1 : 0);
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetDestinationRuleAsync(connection, id, cancellationToken);
    }

    public async Task<LibraryViewItem?> UpdateLibraryViewAsync(
        string userId,
        string id,
        UpdateLibraryViewRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE library_views
            SET name = @name,
                quick_filter = @quickFilter,
                sort_field = @sortField,
                sort_direction = @sortDirection,
                view_mode = @viewMode,
                card_size = @cardSize,
                display_options_json = @displayOptionsJson,
                rules_json = @rulesJson,
                updated_utc = @updatedUtc
            WHERE id = @id AND user_id = @userId;
            """;
        AddParameter(command, "@id", id);
        AddParameter(command, "@userId", userId);
        AddParameter(command, "@name", NormalizeName(request.Name) ?? "Updated view");
        AddParameter(command, "@quickFilter", NormalizeName(request.QuickFilter) ?? "all");
        AddParameter(command, "@sortField", NormalizeName(request.SortField) ?? "title");
        AddParameter(command, "@sortDirection", NormalizeSortDirection(request.SortDirection));
        AddParameter(command, "@viewMode", NormalizeUiView(request.ViewMode));
        AddParameter(command, "@cardSize", NormalizeCardSize(request.CardSize));
        AddParameter(command, "@displayOptionsJson", NormalizeJson(request.DisplayOptionsJson, "{}"));
        AddParameter(command, "@rulesJson", NormalizeJson(request.RulesJson, "[]"));
        AddParameter(command, "@updatedUtc", now.ToString("O"));

        var updated = await command.ExecuteNonQueryAsync(cancellationToken);
        if (updated <= 0)
        {
            return null;
        }

        return await GetLibraryViewAsync(connection, userId, id, cancellationToken);
    }

    public async Task<LibraryItem> CreateLibraryAsync(
        CreateLibraryRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var mediaType = NormalizeMediaType(request.MediaType);
        var item = new LibraryItem(
            Id: Guid.CreateVersion7().ToString("N"),
            Name: NormalizeName(request.Name) ?? "New library",
            MediaType: mediaType,
            Purpose: NormalizeName(request.Purpose) ?? "General",
            RootPath: NormalizePath(request.RootPath) ?? string.Empty,
            DownloadsPath: NormalizePath(request.DownloadsPath),
            QualityProfileId: null,
            QualityProfileName: null,
            CutoffQuality: null,
            UpgradeUntilCutoff: true,
            UpgradeUnknownItems: false,
            ImportWorkflow: NormalizeImportWorkflow(request.ImportWorkflow),
            ProcessorName: NormalizeName(request.ProcessorName),
            ProcessorOutputPath: NormalizePath(request.ProcessorOutputPath),
            ProcessorTimeoutMinutes: NormalizePositiveValue(request.ProcessorTimeoutMinutes, 360),
            ProcessorFailureMode: NormalizeProcessorFailureMode(request.ProcessorFailureMode),
            AutoSearchEnabled: request.AutoSearchEnabled,
            MissingSearchEnabled: request.MissingSearchEnabled,
            UpgradeSearchEnabled: request.UpgradeSearchEnabled,
            SearchIntervalHours: NormalizePositiveValue(request.SearchIntervalHours, 6),
            RetryDelayHours: NormalizePositiveValue(request.RetryDelayHours, 24),
            MaxItemsPerRun: NormalizePositiveValue(request.MaxItemsPerRun, 25),
            SearchWindowStartHour: null,
            SearchWindowEndHour: null,
            AutomationStatus: "idle",
            SearchRequested: false,
            LastSearchedUtc: null,
            NextSearchUtc: null,
            CreatedUtc: now,
            UpdatedUtc: now);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var qualityProfileId = await SqliteQualityRepository.ResolveQualityProfileIdAsync(
            connection,
            mediaType,
            NormalizeName(request.QualityProfileId),
            cancellationToken);
        var profile = qualityProfileId is null
            ? null
            : await SqliteQualityRepository.GetQualityProfileAsync(connection, qualityProfileId, cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO libraries (
                id, name, media_type, purpose, root_path, downloads_path, quality_profile_id,
                import_workflow, processor_name, processor_output_path, processor_timeout_minutes, processor_failure_mode,
                auto_search_enabled,
                missing_search_enabled, upgrade_search_enabled, search_interval_hours,
                retry_delay_hours, max_items_per_run,
                search_window_start_hour, search_window_end_hour,
                created_utc, updated_utc
            )
            VALUES (
                @id, @name, @mediaType, @purpose, @rootPath, @downloadsPath, @qualityProfileId,
                @importWorkflow, @processorName, @processorOutputPath, @processorTimeoutMinutes, @processorFailureMode,
                @autoSearchEnabled,
                @missingSearchEnabled, @upgradeSearchEnabled, @searchIntervalHours,
                @retryDelayHours, @maxItemsPerRun,
                @searchWindowStartHour, @searchWindowEndHour,
                @createdUtc, @updatedUtc
            );
            """;

        AddParameter(command, "@id", item.Id);
        AddParameter(command, "@name", item.Name);
        AddParameter(command, "@mediaType", item.MediaType);
        AddParameter(command, "@purpose", item.Purpose);
        AddParameter(command, "@rootPath", item.RootPath);
        AddParameter(command, "@downloadsPath", item.DownloadsPath);
        AddParameter(command, "@qualityProfileId", qualityProfileId);
        AddParameter(command, "@importWorkflow", item.ImportWorkflow);
        AddParameter(command, "@processorName", item.ProcessorName);
        AddParameter(command, "@processorOutputPath", item.ProcessorOutputPath);
        AddParameter(command, "@processorTimeoutMinutes", item.ProcessorTimeoutMinutes);
        AddParameter(command, "@processorFailureMode", item.ProcessorFailureMode);
        AddParameter(command, "@autoSearchEnabled", item.AutoSearchEnabled ? 1 : 0);
        AddParameter(command, "@missingSearchEnabled", item.MissingSearchEnabled ? 1 : 0);
        AddParameter(command, "@upgradeSearchEnabled", item.UpgradeSearchEnabled ? 1 : 0);
        AddParameter(command, "@searchIntervalHours", item.SearchIntervalHours);
        AddParameter(command, "@retryDelayHours", item.RetryDelayHours);
        AddParameter(command, "@maxItemsPerRun", item.MaxItemsPerRun);
        AddParameter(command, "@searchWindowStartHour", item.SearchWindowStartHour);
        AddParameter(command, "@searchWindowEndHour", item.SearchWindowEndHour);
        AddParameter(command, "@createdUtc", item.CreatedUtc.ToString("O"));
        AddParameter(command, "@updatedUtc", item.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return item with
        {
            QualityProfileId = profile?.Id,
            QualityProfileName = profile?.Name,
            CutoffQuality = profile?.CutoffQuality,
            UpgradeUntilCutoff = profile?.UpgradeUntilCutoff ?? true,
            UpgradeUnknownItems = profile?.UpgradeUnknownItems ?? false
        };
    }

    public async Task<LibraryItem?> UpdateLibraryAutomationAsync(
        string id,
        UpdateLibraryAutomationRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                UPDATE libraries
                SET
                    auto_search_enabled = @autoSearchEnabled,
                    missing_search_enabled = @missingSearchEnabled,
                    upgrade_search_enabled = @upgradeSearchEnabled,
                    search_interval_hours = @searchIntervalHours,
                    retry_delay_hours = @retryDelayHours,
                    max_items_per_run = @maxItemsPerRun,
                    search_window_start_hour = @searchWindowStartHour,
                    search_window_end_hour = @searchWindowEndHour,
                    updated_utc = @updatedUtc
                WHERE id = @id;
                """;

            AddParameter(command, "@id", id);
            AddParameter(command, "@autoSearchEnabled", request.AutoSearchEnabled ? 1 : 0);
            AddParameter(command, "@missingSearchEnabled", request.MissingSearchEnabled ? 1 : 0);
            AddParameter(command, "@upgradeSearchEnabled", request.UpgradeSearchEnabled ? 1 : 0);
            AddParameter(command, "@searchIntervalHours", NormalizePositiveValue(request.SearchIntervalHours, 6));
            AddParameter(command, "@retryDelayHours", NormalizePositiveValue(request.RetryDelayHours, 24));
            AddParameter(command, "@maxItemsPerRun", NormalizePositiveValue(request.MaxItemsPerRun, 25));
            AddParameter(command, "@searchWindowStartHour", NormalizeSearchWindowHour(request.SearchWindowStartHour));
            AddParameter(command, "@searchWindowEndHour", NormalizeSearchWindowHour(request.SearchWindowEndHour));
            AddParameter(command, "@updatedUtc", now.ToString("O"));

            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return null;
            }
        }

        return await GetLibraryAsync(connection, id, cancellationToken);
    }

    public async Task<LibraryItem?> UpdateLibraryDetailsAsync(
        string id,
        UpdateLibraryDetailsRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var library = await GetLibraryAsync(connection, id, cancellationToken);
        if (library is null)
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE libraries
            SET
                name = @name,
                root_path = @rootPath,
                downloads_path = @downloadsPath,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@name", NormalizeName(request.Name) ?? library.Name);
        AddParameter(command, "@rootPath", NormalizePath(request.RootPath) ?? library.RootPath);
        AddParameter(command, "@downloadsPath", NormalizePath(request.DownloadsPath));
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetLibraryAsync(connection, id, cancellationToken);
    }

    public async Task<LibraryItem?> UpdateLibraryQualityProfileAsync(
        string id,
        UpdateLibraryQualityProfileRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var library = await GetLibraryAsync(connection, id, cancellationToken);
        if (library is null)
        {
            return null;
        }

        var qualityProfileId = await SqliteQualityRepository.ResolveQualityProfileIdAsync(
            connection,
            library.MediaType,
            NormalizeName(request.QualityProfileId),
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE libraries
            SET
                quality_profile_id = @qualityProfileId,
                default_policy_set_id = NULL,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@qualityProfileId", qualityProfileId);
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetLibraryAsync(connection, id, cancellationToken);
    }

    public async Task<LibraryItem?> UpdateLibraryMediaPlanAsync(
        string id,
        UpdateLibraryMediaPlanRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var library = await GetLibraryAsync(connection, id, cancellationToken);
        if (library is null)
        {
            return null;
        }

        var policySetId = NormalizeName(request.PolicySetId);
        PolicySetItem? policySet = null;
        if (!string.IsNullOrWhiteSpace(policySetId))
        {
            policySet = await SqliteQualityRepository.GetPolicySetAsync(connection, policySetId, cancellationToken);
            if (policySet is null || !string.Equals(policySet.MediaType, library.MediaType, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE libraries
            SET
                default_policy_set_id = @policySetId,
                quality_profile_id = CASE
                    WHEN @qualityProfileId IS NULL THEN quality_profile_id
                    ELSE @qualityProfileId
                END,
                search_interval_hours = COALESCE(@searchIntervalHours, search_interval_hours),
                retry_delay_hours = COALESCE(@retryDelayHours, retry_delay_hours),
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@policySetId", policySet?.Id);
        AddParameter(command, "@qualityProfileId", policySet?.QualityProfileId);
        AddParameter(command, "@searchIntervalHours", policySet?.SearchIntervalOverrideHours);
        AddParameter(command, "@retryDelayHours", policySet?.RetryDelayOverrideHours);
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return await GetLibraryAsync(connection, id, cancellationToken);
    }

    public async Task<int> ApplyMediaPlanToAssignedLibrariesAsync(
        string policySetId,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var policySet = await SqliteQualityRepository.GetPolicySetAsync(connection, policySetId, cancellationToken);
        if (policySet is null)
        {
            return 0;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE libraries
            SET
                quality_profile_id = CASE
                    WHEN @qualityProfileId IS NULL THEN quality_profile_id
                    ELSE @qualityProfileId
                END,
                search_interval_hours = COALESCE(@searchIntervalHours, search_interval_hours),
                retry_delay_hours = COALESCE(@retryDelayHours, retry_delay_hours),
                updated_utc = @updatedUtc
            WHERE default_policy_set_id = @policySetId;
            """;

        AddParameter(command, "@policySetId", policySet.Id);
        AddParameter(command, "@qualityProfileId", policySet.QualityProfileId);
        AddParameter(command, "@searchIntervalHours", policySet.SearchIntervalOverrideHours);
        AddParameter(command, "@retryDelayHours", policySet.RetryDelayOverrideHours);
        AddParameter(command, "@updatedUtc", timeProvider.GetUtcNow().ToString("O"));
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LibraryItem?> UpdateLibraryWorkflowAsync(
        string id,
        UpdateLibraryWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var workflow = NormalizeImportWorkflow(request.ImportWorkflow);
        var processorOutputPath = NormalizePath(request.ProcessorOutputPath);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE libraries
            SET
                import_workflow = @importWorkflow,
                processor_name = @processorName,
                processor_output_path = @processorOutputPath,
                processor_timeout_minutes = @processorTimeoutMinutes,
                processor_failure_mode = @processorFailureMode,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;

        AddParameter(command, "@id", id);
        AddParameter(command, "@importWorkflow", workflow);
        AddParameter(command, "@processorName", NormalizeName(request.ProcessorName));
        AddParameter(command, "@processorOutputPath", processorOutputPath);
        AddParameter(command, "@processorTimeoutMinutes", NormalizePositiveValue(request.ProcessorTimeoutMinutes, 360));
        AddParameter(command, "@processorFailureMode", NormalizeProcessorFailureMode(request.ProcessorFailureMode));
        AddParameter(command, "@updatedUtc", now.ToString("O"));

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        return await GetLibraryAsync(connection, id, cancellationToken);
    }

    public async Task<LibraryRoutingSnapshot?> GetLibraryRoutingAsync(string libraryId, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var library = await GetLibraryAsync(connection, libraryId, cancellationToken);
        if (library is null)
        {
            return null;
        }

        var sources = await ReadLibrarySourceLinksAsync(connection, libraryId, cancellationToken);
        var downloadClients = await ReadLibraryDownloadClientLinksAsync(connection, libraryId, cancellationToken);
        return new LibraryRoutingSnapshot(library.Id, library.Name, sources, downloadClients);
    }

    public async Task<LibraryRoutingSnapshot?> SaveLibraryRoutingAsync(
        string libraryId,
        UpdateLibraryRoutingRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        var library = await GetLibraryAsync(connection, libraryId, cancellationToken);
        if (library is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        using (var deleteSources = connection.CreateCommand())
        {
            deleteSources.Transaction = transaction;
            deleteSources.CommandText = "DELETE FROM library_source_links WHERE library_id = @libraryId;";
            AddParameter(deleteSources, "@libraryId", libraryId);
            await deleteSources.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var source in request.Sources ?? [])
        {
            using var insertSource = connection.CreateCommand();
            insertSource.Transaction = transaction;
            insertSource.CommandText =
                """
                INSERT INTO library_source_links (
                    id, library_id, indexer_id, priority, required_tags, excluded_tags, created_utc, updated_utc
                )
                VALUES (
                    @id, @libraryId, @indexerId, @priority, @requiredTags, @excludedTags, @createdUtc, @updatedUtc
                );
                """;

            AddParameter(insertSource, "@id", Guid.CreateVersion7().ToString("N"));
            AddParameter(insertSource, "@libraryId", libraryId);
            AddParameter(insertSource, "@indexerId", source.IndexerId);
            AddParameter(insertSource, "@priority", source.Priority is >= 1 ? source.Priority.Value : 100);
            AddParameter(insertSource, "@requiredTags", NormalizeCsv(source.RequiredTags));
            AddParameter(insertSource, "@excludedTags", NormalizeCsv(source.ExcludedTags));
            AddParameter(insertSource, "@createdUtc", now.ToString("O"));
            AddParameter(insertSource, "@updatedUtc", now.ToString("O"));
            await insertSource.ExecuteNonQueryAsync(cancellationToken);
        }

        using (var deleteClients = connection.CreateCommand())
        {
            deleteClients.Transaction = transaction;
            deleteClients.CommandText = "DELETE FROM library_download_client_links WHERE library_id = @libraryId;";
            AddParameter(deleteClients, "@libraryId", libraryId);
            await deleteClients.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var client in request.DownloadClients ?? [])
        {
            using var insertClient = connection.CreateCommand();
            insertClient.Transaction = transaction;
            insertClient.CommandText =
                """
                INSERT INTO library_download_client_links (
                    id, library_id, download_client_id, priority, created_utc, updated_utc
                )
                VALUES (
                    @id, @libraryId, @downloadClientId, @priority, @createdUtc, @updatedUtc
                );
                """;

            AddParameter(insertClient, "@id", Guid.CreateVersion7().ToString("N"));
            AddParameter(insertClient, "@libraryId", libraryId);
            AddParameter(insertClient, "@downloadClientId", client.DownloadClientId);
            AddParameter(insertClient, "@priority", client.Priority is >= 1 ? client.Priority.Value : 100);
            AddParameter(insertClient, "@createdUtc", now.ToString("O"));
            AddParameter(insertClient, "@updatedUtc", now.ToString("O"));
            await insertClient.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        var sources = await ReadLibrarySourceLinksAsync(connection, libraryId, cancellationToken);
        var downloadClients = await ReadLibraryDownloadClientLinksAsync(connection, libraryId, cancellationToken);
        return new LibraryRoutingSnapshot(library.Id, library.Name, sources, downloadClients);
    }

    public async Task<bool> DeleteLibraryAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM libraries WHERE id = @id;";
        AddParameter(command, "@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteTagAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM tags WHERE id = @id;";
        AddParameter(command, "@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteDestinationRuleAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM destination_rules WHERE id = @id;";
        AddParameter(command, "@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteLibraryViewAsync(string userId, string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM library_views WHERE id = @id AND user_id = @userId;";
        AddParameter(command, "@id", id);
        AddParameter(command, "@userId", userId);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static PlatformSettingsSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string> settings,
        IReadOnlyDictionary<string, string> roots)
    {
        var brokerUrl = NormalizeMetadataBrokerUrl(GetValue(settings, "metadata.brokerUrl")) ?? ManagedMetadataBrokerUrl;

        return new PlatformSettingsSnapshot(
            AppInstanceName: GetValue(settings, "app.instanceName") ?? "Deluno",
            MovieRootPath: GetValue(roots, "movies"),
            SeriesRootPath: GetValue(roots, "series"),
            DownloadsPath: GetValue(roots, "downloads"),
            IncompleteDownloadsPath: GetValue(roots, "downloads.incomplete"),
            AutoStartJobs: string.Equals(GetValue(settings, "jobs.autoStart"), "true", StringComparison.OrdinalIgnoreCase),
            EnableNotifications: string.Equals(GetValue(settings, "notifications.enabled"), "true", StringComparison.OrdinalIgnoreCase),
            RenameOnImport: !string.Equals(GetValue(settings, "media.renameOnImport"), "false", StringComparison.OrdinalIgnoreCase),
            UseHardlinks: !string.Equals(GetValue(settings, "media.useHardlinks"), "false", StringComparison.OrdinalIgnoreCase),
            CleanupEmptyFolders: string.Equals(GetValue(settings, "media.cleanupEmptyFolders"), "true", StringComparison.OrdinalIgnoreCase),
            RemoveCompletedDownloads: string.Equals(GetValue(settings, "media.removeCompletedDownloads"), "true", StringComparison.OrdinalIgnoreCase),
            UnmonitorWhenCutoffMet: string.Equals(GetValue(settings, "media.unmonitorWhenCutoffMet"), "true", StringComparison.OrdinalIgnoreCase),
            MovieFolderFormat: GetValue(settings, "media.movieFolderFormat") ?? "{Movie Title} ({Release Year})",
            SeriesFolderFormat: GetValue(settings, "media.seriesFolderFormat") ?? "{Series Title} ({Series Year})",
            EpisodeFileFormat: GetValue(settings, "media.episodeFileFormat") ?? "{Series Title} - S{season:00}E{episode:00} - {Episode Title}",
            HostBindAddress: GetValue(settings, "host.bindAddress") ?? "127.0.0.1",
            HostPort: int.TryParse(GetValue(settings, "host.port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hostPort) ? hostPort : 5099,
            UrlBase: GetValue(settings, "host.urlBase") ?? string.Empty,
            RequireAuthentication: true,
            UiTheme: NormalizeUiTheme(GetValue(settings, "ui.theme")),
            UiDensity: NormalizeUiDensity(GetValue(settings, "ui.density")),
            DefaultMovieView: NormalizeUiView(GetValue(settings, "ui.defaultMovieView")),
            DefaultShowView: NormalizeUiView(GetValue(settings, "ui.defaultShowView")),
            MetadataNfoEnabled: string.Equals(GetValue(settings, "metadata.nfoEnabled"), "true", StringComparison.OrdinalIgnoreCase),
            MetadataArtworkEnabled: !string.Equals(GetValue(settings, "metadata.artworkEnabled"), "false", StringComparison.OrdinalIgnoreCase),
            MetadataCertificationCountry: NormalizeName(GetValue(settings, "metadata.certificationCountry")) ?? "US",
            MetadataLanguage: NormalizeName(GetValue(settings, "metadata.language")) ?? "en",
            MetadataProviderMode: NormalizeMetadataProviderMode(GetValue(settings, "metadata.providerMode") ?? "broker"),
            MetadataBrokerUrl: brokerUrl,
            MetadataBrokerConfigured: !string.IsNullOrWhiteSpace(brokerUrl),
            MetadataTmdbApiKeyConfigured: !string.IsNullOrWhiteSpace(GetValue(settings, "metadata.tmdbApiKey")),
            MetadataOmdbApiKeyConfigured: !string.IsNullOrWhiteSpace(GetValue(settings, "metadata.omdbApiKey")),
            ReleaseNeverGrabPatterns: NormalizeNeverGrabPatterns(GetValue(settings, "search.neverGrabPatterns")),
            SearchScoringMode: SearchScoringModes.Normalize(GetValue(settings, "search.scoringMode")),
            ImportRecoveryRetentionDays: int.TryParse(GetValue(settings, "media.importRecoveryRetentionDays"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var retentionDays) && retentionDays > 0 ? retentionDays : 30,
            UpdatedUtc: DateTimeOffset.UtcNow,
            MdbListApiKeyConfigured: !string.IsNullOrWhiteSpace(GetValue(settings, "intake.mdblistApiKey")),
            DownloadHealthStrikeThreshold: ReadDownloadHealthStrikeThreshold(settings),
            CleanupBlockReleaseAfterThreshold: !string.Equals(GetValue(settings, "cleanup.blockReleaseAfterThreshold"), "false", StringComparison.OrdinalIgnoreCase),
            CleanupQueueReplacementAfterThreshold: !string.Equals(GetValue(settings, "cleanup.queueReplacementAfterThreshold"), "false", StringComparison.OrdinalIgnoreCase),
            CleanupRemoveClientEntryAfterThreshold: string.Equals(GetValue(settings, "cleanup.removeClientEntryAfterThreshold"), "true", StringComparison.OrdinalIgnoreCase),
            CleanupPurgePayloadAfterThreshold: string.Equals(GetValue(settings, "cleanup.purgePayloadAfterThreshold"), "true", StringComparison.OrdinalIgnoreCase));
    }

    private static int ReadDownloadHealthStrikeThreshold(IReadOnlyDictionary<string, string> settings)
        => int.TryParse(GetValue(settings, "cleanup.strikeThreshold"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold)
            ? Math.Clamp(threshold, 1, 20)
            : 3;

    private static SetupProgressItem CreateSetupProgress(IReadOnlyDictionary<string, string> settings)
    {
        var lastCompletedStep = int.TryParse(GetValue(settings, "setup.lastCompletedStep"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Clamp(parsed, 0, 4)
            : 0;
        var isSkipped = string.Equals(GetValue(settings, "setup.isSkipped"), "true", StringComparison.OrdinalIgnoreCase);
        var isCompleted = string.Equals(GetValue(settings, "setup.isCompleted"), "true", StringComparison.OrdinalIgnoreCase);
        var updatedUtc = DateTimeOffset.TryParse(
            GetValue(settings, "setup.updatedUtc"),
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsedUpdatedUtc)
            ? parsedUpdatedUtc
            : DateTimeOffset.MinValue;
        return new SetupProgressItem(lastCompletedStep, isSkipped, isCompleted, updatedUtc);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadSettingsAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT setting_key, setting_value FROM system_settings;";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return values;
    }

    private static IReadOnlyList<DownloadHealthRecord> ReadDownloadHealthRecords(IReadOnlyDictionary<string, string> settings)
    {
        if (!settings.TryGetValue(DownloadHealthRecordsSettingKey, out var json) || string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<DownloadHealthRecord>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string NormalizeDownloadReleaseKey(string releaseName)
    {
        var builder = new StringBuilder(releaseName.Length);
        foreach (var character in releaseName.Trim().ToUpperInvariant())
        {
            if (char.IsLetterOrDigit(character)) builder.Append(character);
        }
        return builder.ToString();
    }

    private static string SanitizeDownloadHealthEvidence(string evidence)
        => evidence.TrimStart().StartsWith("Import source:", StringComparison.OrdinalIgnoreCase)
            ? "Import source: [redacted path]"
            : evidence.Trim();

    private static async Task<IReadOnlyDictionary<string, string>> ReadRootsAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT root_key, root_path FROM root_paths;";

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        return values;
    }

    private static async Task UpsertSettingAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string key,
        string value,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO system_settings (setting_key, setting_value, updated_utc)
            VALUES (@key, @value, @updatedUtc)
            ON CONFLICT(setting_key) DO UPDATE SET
                setting_value = excluded.setting_value,
                updated_utc = excluded.updated_utc;
            """;

        AddParameter(command, "@key", key);
        AddParameter(command, "@value", value);
        AddParameter(command, "@updatedUtc", updatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertRootAsync(
        System.Data.Common.DbConnection connection,
        System.Data.Common.DbTransaction transaction,
        string key,
        string? value,
        DateTimeOffset updatedUtc,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;

        if (string.IsNullOrWhiteSpace(value))
        {
            command.CommandText = "DELETE FROM root_paths WHERE root_key = @key;";
            AddParameter(command, "@key", key);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return;
        }

        command.CommandText =
            """
            INSERT INTO root_paths (root_key, root_path, updated_utc)
            VALUES (@key, @value, @updatedUtc)
            ON CONFLICT(root_key) DO UPDATE SET
                root_path = excluded.root_path,
                updated_utc = excluded.updated_utc;
            """;

        AddParameter(command, "@key", key);
        AddParameter(command, "@value", value);
        AddParameter(command, "@updatedUtc", updatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureSeedLibrariesAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await SqliteQualityRepository.EnsureSeedQualityProfilesAsync(connection, cancellationToken);
        await BackfillLibraryQualityProfilesAsync(connection, cancellationToken);

        var count = 0;

        using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM libraries;";
            var scalar = await countCommand.ExecuteScalarAsync(cancellationToken);
            count = Convert.ToInt32(scalar ?? 0, CultureInfo.InvariantCulture);
        }

        if (count > 0)
        {
            return;
        }

        var roots = await ReadRootsAsync(connection, cancellationToken);
        var downloadsPath = GetValue(roots, "downloads");
        var now = DateTimeOffset.UtcNow;
        var defaultMovieProfileId = await SqliteQualityRepository.ResolveQualityProfileIdAsync(connection, "movies", null, cancellationToken);
        var defaultTvProfileId = await SqliteQualityRepository.ResolveQualityProfileIdAsync(connection, "tv", null, cancellationToken);

        var seeds = new List<LibraryItem>();
        var movieRoot = GetValue(roots, "movies");
        if (!string.IsNullOrWhiteSpace(movieRoot))
        {
            seeds.Add(new LibraryItem(
                Id: Guid.CreateVersion7().ToString("N"),
                Name: "Movies / Main",
                MediaType: "movies",
                Purpose: "Everyday library",
                RootPath: movieRoot,
                DownloadsPath: downloadsPath,
                QualityProfileId: defaultMovieProfileId,
                QualityProfileName: "Movies / Standard",
                CutoffQuality: "WEB 1080p",
                UpgradeUntilCutoff: true,
                UpgradeUnknownItems: false,
                ImportWorkflow: "standard",
                ProcessorName: null,
                ProcessorOutputPath: null,
                ProcessorTimeoutMinutes: 360,
                ProcessorFailureMode: "block",
                AutoSearchEnabled: true,
                MissingSearchEnabled: true,
                UpgradeSearchEnabled: true,
                SearchIntervalHours: 6,
                RetryDelayHours: 24,
                MaxItemsPerRun: 25,
                SearchWindowStartHour: null,
                SearchWindowEndHour: null,
                AutomationStatus: "idle",
                SearchRequested: false,
                LastSearchedUtc: null,
                NextSearchUtc: null,
                CreatedUtc: now,
                UpdatedUtc: now));
        }

        var tvRoot = GetValue(roots, "series");
        if (!string.IsNullOrWhiteSpace(tvRoot))
        {
            seeds.Add(new LibraryItem(
                Id: Guid.CreateVersion7().ToString("N"),
                Name: "TV Shows / Main",
                MediaType: "tv",
                Purpose: "General shows",
                RootPath: tvRoot,
                DownloadsPath: downloadsPath,
                QualityProfileId: defaultTvProfileId,
                QualityProfileName: "TV Shows / Standard",
                CutoffQuality: "WEB 1080p",
                UpgradeUntilCutoff: true,
                UpgradeUnknownItems: false,
                ImportWorkflow: "standard",
                ProcessorName: null,
                ProcessorOutputPath: null,
                ProcessorTimeoutMinutes: 360,
                ProcessorFailureMode: "block",
                AutoSearchEnabled: true,
                MissingSearchEnabled: true,
                UpgradeSearchEnabled: true,
                SearchIntervalHours: 6,
                RetryDelayHours: 24,
                MaxItemsPerRun: 25,
                SearchWindowStartHour: null,
                SearchWindowEndHour: null,
                AutomationStatus: "idle",
                SearchRequested: false,
                LastSearchedUtc: null,
                NextSearchUtc: null,
                CreatedUtc: now,
                UpdatedUtc: now));
        }

        foreach (var item in seeds)
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO libraries (
                    id, name, media_type, purpose, root_path, downloads_path, quality_profile_id,
                    import_workflow, processor_name, processor_output_path, processor_timeout_minutes, processor_failure_mode,
                    auto_search_enabled,
                    missing_search_enabled, upgrade_search_enabled, search_interval_hours,
                    retry_delay_hours, max_items_per_run,
                    search_window_start_hour, search_window_end_hour,
                    created_utc, updated_utc
                )
                VALUES (
                    @id, @name, @mediaType, @purpose, @rootPath, @downloadsPath, @qualityProfileId,
                    @importWorkflow, @processorName, @processorOutputPath, @processorTimeoutMinutes, @processorFailureMode,
                    @autoSearchEnabled,
                    @missingSearchEnabled, @upgradeSearchEnabled, @searchIntervalHours,
                    @retryDelayHours, @maxItemsPerRun,
                    @searchWindowStartHour, @searchWindowEndHour,
                    @createdUtc, @updatedUtc
                );
                """;

            AddParameter(command, "@id", item.Id);
            AddParameter(command, "@name", item.Name);
            AddParameter(command, "@mediaType", item.MediaType);
            AddParameter(command, "@purpose", item.Purpose);
            AddParameter(command, "@rootPath", item.RootPath);
            AddParameter(command, "@downloadsPath", item.DownloadsPath);
            AddParameter(command, "@qualityProfileId", item.QualityProfileId);
            AddParameter(command, "@importWorkflow", item.ImportWorkflow);
            AddParameter(command, "@processorName", item.ProcessorName);
            AddParameter(command, "@processorOutputPath", item.ProcessorOutputPath);
            AddParameter(command, "@processorTimeoutMinutes", item.ProcessorTimeoutMinutes);
            AddParameter(command, "@processorFailureMode", item.ProcessorFailureMode);
            AddParameter(command, "@autoSearchEnabled", item.AutoSearchEnabled ? 1 : 0);
            AddParameter(command, "@missingSearchEnabled", item.MissingSearchEnabled ? 1 : 0);
            AddParameter(command, "@upgradeSearchEnabled", item.UpgradeSearchEnabled ? 1 : 0);
            AddParameter(command, "@searchIntervalHours", item.SearchIntervalHours);
            AddParameter(command, "@retryDelayHours", item.RetryDelayHours);
            AddParameter(command, "@maxItemsPerRun", item.MaxItemsPerRun);
            AddParameter(command, "@searchWindowStartHour", item.SearchWindowStartHour);
            AddParameter(command, "@searchWindowEndHour", item.SearchWindowEndHour);
            AddParameter(command, "@createdUtc", item.CreatedUtc.ToString("O"));
            AddParameter(command, "@updatedUtc", item.UpdatedUtc.ToString("O"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<LibraryItem?> GetLibraryAsync(
        System.Data.Common.DbConnection connection,
        string id,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.id, l.name, l.media_type, l.purpose, l.root_path, l.downloads_path,
                l.quality_profile_id, q.name, q.cutoff_quality, q.upgrade_until_cutoff, q.upgrade_unknown_items,
                l.import_workflow, l.processor_name, l.processor_output_path, l.processor_timeout_minutes, l.processor_failure_mode,
                l.auto_search_enabled, l.missing_search_enabled, l.upgrade_search_enabled, l.search_interval_hours,
                l.retry_delay_hours, l.max_items_per_run, l.search_window_start_hour, l.search_window_end_hour,
                l.created_utc, l.updated_utc, l.default_policy_set_id, p.name
            FROM libraries l
            LEFT JOIN quality_profiles q ON q.id = l.quality_profile_id
            LEFT JOIN policy_sets p ON p.id = l.default_policy_set_id
            WHERE l.id = @id
            LIMIT 1;
            """;

        AddParameter(command, "@id", id);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadLibrary(reader);
    }

    private static async Task BackfillLibraryQualityProfilesAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        var pendingLibraries = new List<(string LibraryId, string MediaType)>();
        var assignments = new List<(string LibraryId, string ProfileId)>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                """
                SELECT id, media_type
                FROM libraries
                WHERE quality_profile_id IS NULL;
                """;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                pendingLibraries.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach (var library in pendingLibraries)
        {
            var profileId = await SqliteQualityRepository.ResolveQualityProfileIdAsync(
                connection,
                library.MediaType,
                null,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(profileId))
            {
                assignments.Add((library.LibraryId, profileId));
            }
        }

        foreach (var assignment in assignments)
        {
            using var update = connection.CreateCommand();
            update.CommandText =
                """
                UPDATE libraries
                SET quality_profile_id = @qualityProfileId
                WHERE id = @id;
                """;
            AddParameter(update, "@id", assignment.LibraryId);
            AddParameter(update, "@qualityProfileId", assignment.ProfileId);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<TagItem?> GetTagAsync(
        System.Data.Common.DbConnection connection,
        string id,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, name, color, description, created_utc, updated_utc
            FROM tags
            WHERE id = @id
            LIMIT 1;
            """;
        AddParameter(command, "@id", id);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadTag(reader) : null;
    }

    private static async Task<DestinationRuleItem?> GetDestinationRuleAsync(
        System.Data.Common.DbConnection connection,
        string id,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, name, media_type, match_kind, match_value, root_path, folder_template,
                priority, is_enabled, created_utc, updated_utc
            FROM destination_rules
            WHERE id = @id
            LIMIT 1;
            """;
        AddParameter(command, "@id", id);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDestinationRule(reader) : null;
    }

    private static async Task<LibraryViewItem?> GetLibraryViewAsync(
        System.Data.Common.DbConnection connection,
        string userId,
        string id,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                id, user_id, variant, name, quick_filter, sort_field, sort_direction,
                view_mode, card_size, display_options_json, rules_json, created_utc, updated_utc
            FROM library_views
            WHERE user_id = @userId AND id = @id
            LIMIT 1;
            """;
        AddParameter(command, "@userId", userId);
        AddParameter(command, "@id", id);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadLibraryView(reader) : null;
    }

    private static string NormalizeSetupText(string? value)
        => NormalizeName(value) ?? string.Empty;

    private static string NormalizeSetupChoice(string? value, string fallback, params string[] allowed)
    {
        var normalized = NormalizeSetupText(value);
        return allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase) ? normalized : fallback;
    }

    private static string NormalizeDestinationMatchKind(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "tag" => "tag",
            "language" => "language",
            "quality" => "quality",
            "anime" => "anime",
            "certification" => "certification",
            "library" => "library",
            _ => "genre"
        };
    }

    private static string NormalizeNeverGrabPatterns(string? value)
    {
        var defaultPatterns = new[] { "cam", "camrip", "telesync", "telecine", "workprint", "screener", "sample", "trailer", "extras" };
        var raw = string.IsNullOrWhiteSpace(value)
            ? defaultPatterns
            : value.Split([',', '\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return string.Join(
            "\n",
            raw
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeUiTheme(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "light" => "light",
            "dark" => "dark",
            _ => "system"
        };
    }

    private static string NormalizeUiDensity(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "compact" => "compact",
            "spacious" => "spacious",
            "expanded" => "expanded",
            _ => "comfortable"
        };
    }

    private static string NormalizeUiView(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "list" => "list",
            _ => "grid"
        };
    }

    private static string NormalizeImportWorkflow(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "refine-before-import" or "refine" or "processor" or "processing" => "refine-before-import",
            _ => "standard"
        };
    }

    private static string NormalizeProcessorFailureMode(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "import-original" or "fallback-original" or "fallback" => "import-original",
            "manual-review" or "review" => "manual-review",
            _ => "block"
        };
    }

    private static string NormalizeMetadataProviderMode(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "broker" or "cloud" or "managed" => "broker",
            "hybrid" or "broker-first" or "brokerfirst" => "hybrid",
            "direct" or "direct-only" or "directonly" or "self-hosted" => "direct",
            _ => "direct"
        };
    }

    private static string? NormalizeMetadataBrokerUrl(string? value)
    {
        var normalized = NormalizeName(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.TrimEnd('/');
    }

    private static string NormalizeCardSize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "sm" => "sm",
            "lg" => "lg",
            _ => "md"
        };
    }

    private static string NormalizeLibraryViewVariant(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "shows" => "shows",
            _ => "movies"
        };
    }

    private static string NormalizeSortDirection(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized == "desc" ? "desc" : "asc";
    }

    private static string NormalizeJson(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(value);
            return value;
        }
        catch
        {
            return fallback;
        }
    }

    private static int? NormalizeSearchWindowHour(int? value)
        => value is null ? null : Math.Clamp(value.Value, 0, 23);

    private static string NormalizeTagColor(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "emerald" => "emerald",
            "teal" => "teal",
            "blue" => "blue",
            "violet" => "violet",
            "amber" => "amber",
            "rose" => "rose",
            _ => "slate"
        };
    }

    private static int NormalizePositiveValue(int? value, int fallback)
    {
        return value is > 0 ? value.Value : fallback;
    }

    private static int NormalizePriorityValue(int value)
    {
        return value <= 0 ? 100 : value;
    }

    private static async Task<IReadOnlyList<LibrarySourceLinkItem>> ReadLibrarySourceLinksAsync(
        System.Data.Common.DbConnection connection,
        string libraryId,
        CancellationToken cancellationToken)
    {
        var items = new List<LibrarySourceLinkItem>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.id, l.library_id, l.indexer_id, i.name, l.priority, l.required_tags, l.excluded_tags, l.created_utc, l.updated_utc
            FROM library_source_links l
            INNER JOIN indexer_sources i ON i.id = l.indexer_id
            WHERE l.library_id = @libraryId
            ORDER BY l.priority ASC, i.name ASC;
            """;

        AddParameter(command, "@libraryId", libraryId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LibrarySourceLinkItem(
                Id: reader.GetString(0),
                LibraryId: reader.GetString(1),
                IndexerId: reader.GetString(2),
                IndexerName: reader.GetString(3),
                Priority: reader.GetInt32(4),
                RequiredTags: reader.GetString(5),
                ExcludedTags: reader.GetString(6),
                CreatedUtc: ParseTimestamp(reader.GetString(7)),
                UpdatedUtc: ParseTimestamp(reader.GetString(8))));
        }

        return items;
    }

    private static LibraryItem ReadLibrary(System.Data.Common.DbDataReader reader)
    {
        return new LibraryItem(
            Id: reader.GetString(0),
            Name: reader.GetString(1),
            MediaType: reader.GetString(2),
            Purpose: reader.GetString(3),
            RootPath: reader.GetString(4),
            DownloadsPath: reader.IsDBNull(5) ? null : reader.GetString(5),
            QualityProfileId: reader.IsDBNull(6) ? null : reader.GetString(6),
            QualityProfileName: reader.IsDBNull(7) ? null : reader.GetString(7),
            CutoffQuality: reader.IsDBNull(8) ? null : reader.GetString(8),
            UpgradeUntilCutoff: reader.IsDBNull(9) || reader.GetInt64(9) == 1,
            UpgradeUnknownItems: !reader.IsDBNull(10) && reader.GetInt64(10) == 1,
            ImportWorkflow: reader.IsDBNull(11) ? "standard" : NormalizeImportWorkflow(reader.GetString(11)),
            ProcessorName: reader.IsDBNull(12) ? null : reader.GetString(12),
            ProcessorOutputPath: reader.IsDBNull(13) ? null : reader.GetString(13),
            ProcessorTimeoutMinutes: reader.IsDBNull(14) ? 360 : reader.GetInt32(14),
            ProcessorFailureMode: reader.IsDBNull(15) ? "block" : NormalizeProcessorFailureMode(reader.GetString(15)),
            AutoSearchEnabled: reader.GetInt64(16) == 1,
            MissingSearchEnabled: reader.GetInt64(17) == 1,
            UpgradeSearchEnabled: reader.GetInt64(18) == 1,
            SearchIntervalHours: reader.GetInt32(19),
            RetryDelayHours: reader.GetInt32(20),
            MaxItemsPerRun: reader.GetInt32(21),
            SearchWindowStartHour: reader.IsDBNull(22) ? null : reader.GetInt32(22),
            SearchWindowEndHour: reader.IsDBNull(23) ? null : reader.GetInt32(23),
            AutomationStatus: "idle",
            SearchRequested: false,
            LastSearchedUtc: null,
            NextSearchUtc: null,
            CreatedUtc: ParseTimestamp(reader.GetString(24)),
            UpdatedUtc: ParseTimestamp(reader.GetString(25)),
            DefaultPolicySetId: reader.IsDBNull(26) ? null : reader.GetString(26),
            DefaultPolicySetName: reader.IsDBNull(27) ? null : reader.GetString(27));
    }

    private static TagItem ReadTag(System.Data.Common.DbDataReader reader)
    {
        return new TagItem(
            Id: reader.GetString(0),
            Name: reader.GetString(1),
            Color: reader.GetString(2),
            Description: reader.GetString(3),
            CreatedUtc: ParseTimestamp(reader.GetString(4)),
            UpdatedUtc: ParseTimestamp(reader.GetString(5)));
    }

    private static DestinationRuleItem ReadDestinationRule(System.Data.Common.DbDataReader reader)
    {
        return new DestinationRuleItem(
            Id: reader.GetString(0),
            Name: reader.GetString(1),
            MediaType: reader.GetString(2),
            MatchKind: reader.GetString(3),
            MatchValue: reader.GetString(4),
            RootPath: reader.GetString(5),
            FolderTemplate: reader.IsDBNull(6) ? null : reader.GetString(6),
            Priority: reader.GetInt32(7),
            IsEnabled: reader.GetInt64(8) == 1,
            CreatedUtc: ParseTimestamp(reader.GetString(9)),
            UpdatedUtc: ParseTimestamp(reader.GetString(10)));
    }

    private static LibraryViewItem ReadLibraryView(System.Data.Common.DbDataReader reader)
    {
        return new LibraryViewItem(
            Id: reader.GetString(0),
            UserId: reader.GetString(1),
            Variant: reader.GetString(2),
            Name: reader.GetString(3),
            QuickFilter: reader.GetString(4),
            SortField: reader.GetString(5),
            SortDirection: reader.GetString(6),
            ViewMode: reader.GetString(7),
            CardSize: reader.GetString(8),
            DisplayOptionsJson: reader.GetString(9),
            RulesJson: reader.GetString(10),
            CreatedUtc: ParseTimestamp(reader.GetString(11)),
            UpdatedUtc: ParseTimestamp(reader.GetString(12)));
    }

    private static async Task<IReadOnlyList<LibraryDownloadClientLinkItem>> ReadLibraryDownloadClientLinksAsync(
        System.Data.Common.DbConnection connection,
        string libraryId,
        CancellationToken cancellationToken)
    {
        var items = new List<LibraryDownloadClientLinkItem>();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT
                l.id, l.library_id, l.download_client_id, d.name, l.priority, l.created_utc, l.updated_utc
            FROM library_download_client_links l
            INNER JOIN download_clients d ON d.id = l.download_client_id
            WHERE l.library_id = @libraryId
            ORDER BY l.priority ASC, d.name ASC;
            """;

        AddParameter(command, "@libraryId", libraryId);

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new LibraryDownloadClientLinkItem(
                Id: reader.GetString(0),
                LibraryId: reader.GetString(1),
                DownloadClientId: reader.GetString(2),
                DownloadClientName: reader.GetString(3),
                Priority: reader.GetInt32(4),
                CreatedUtc: ParseTimestamp(reader.GetString(5)),
                UpdatedUtc: ParseTimestamp(reader.GetString(6))));
        }

        return items;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : null;

    public async Task<IReadOnlyList<ProcessorConnectionItem>> ListProcessorConnectionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, name, provider, submission_url, auth_header_name, secret_value, is_enabled, health_status, last_health_message, last_health_test_utc, created_utc, updated_utc FROM processor_connections ORDER BY name COLLATE NOCASE;";
        var items = new List<ProcessorConnectionItem>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadProcessorConnection(reader));
        }

        return items;
    }

    public async Task<ProcessorConnectionItem?> GetProcessorConnectionAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        return await GetProcessorConnectionAsync(connection, id, cancellationToken);
    }

    public async Task<ProcessorConnectionItem?> FindProcessorConnectionByNameAsync(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, name, provider, submission_url, auth_header_name, secret_value, is_enabled, health_status, last_health_message, last_health_test_utc, created_utc, updated_utc FROM processor_connections WHERE name = @name COLLATE NOCASE LIMIT 1;";
        AddParameter(command, "@name", name.Trim());
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProcessorConnection(reader) : null;
    }

    public async Task<ProcessorConnectionItem> CreateProcessorConnectionAsync(
        CreateProcessorConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var item = new ProcessorConnectionItem(
            Guid.CreateVersion7().ToString("N"),
            NormalizeName(request.Name) ?? "Processor connection",
            NormalizeProcessorConnectionProvider(request.Provider),
            NormalizeProcessorConnectionUrl(request.SubmissionUrl) ?? string.Empty,
            NormalizeProcessorAuthHeaderName(request.AuthHeaderName),
            string.IsNullOrWhiteSpace(request.Secret) ? null : request.Secret.Trim(),
            request.IsEnabled,
            "unknown",
            null,
            null,
            now,
            now);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO processor_connections (
                id, name, provider, submission_url, auth_header_name, secret_value, is_enabled,
                health_status, last_health_message, last_health_test_utc, created_utc, updated_utc
            ) VALUES (
                @id, @name, @provider, @submissionUrl, @authHeaderName, @secretValue, @isEnabled,
                @healthStatus, NULL, NULL, @createdUtc, @updatedUtc
            );
            """;
        AddParameter(command, "@id", item.Id);
        AddParameter(command, "@name", item.Name);
        AddParameter(command, "@provider", item.Provider);
        AddParameter(command, "@submissionUrl", item.SubmissionUrl);
        AddParameter(command, "@authHeaderName", item.AuthHeaderName);
        AddParameter(command, "@secretValue", item.Secret is null ? null : secretProtector.Protect($"processor-connection:{item.Id}", item.Secret));
        AddParameter(command, "@isEnabled", item.IsEnabled ? 1 : 0);
        AddParameter(command, "@healthStatus", item.HealthStatus);
        AddParameter(command, "@createdUtc", item.CreatedUtc.ToString("O"));
        AddParameter(command, "@updatedUtc", item.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return item;
    }

    public async Task<ProcessorConnectionItem?> UpdateProcessorConnectionAsync(
        string id,
        UpdateProcessorConnectionRequest request,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        var existing = await GetProcessorConnectionAsync(connection, id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var name = NormalizeName(request.Name) ?? existing.Name;
        var provider = NormalizeProcessorConnectionProvider(request.Provider ?? existing.Provider);
        var submissionUrl = NormalizeProcessorConnectionUrl(request.SubmissionUrl) ?? existing.SubmissionUrl;
        var authHeaderName = NormalizeProcessorAuthHeaderName(request.AuthHeaderName ?? existing.AuthHeaderName);
        var secret = string.IsNullOrWhiteSpace(request.Secret) ? existing.Secret : request.Secret.Trim();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE processor_connections
            SET name = @name,
                provider = @provider,
                submission_url = @submissionUrl,
                auth_header_name = @authHeaderName,
                secret_value = @secretValue,
                is_enabled = @isEnabled,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;
        AddParameter(command, "@id", id.Trim());
        AddParameter(command, "@name", name);
        AddParameter(command, "@provider", provider);
        AddParameter(command, "@submissionUrl", submissionUrl);
        AddParameter(command, "@authHeaderName", authHeaderName);
        AddParameter(command, "@secretValue", secret is null ? null : secretProtector.Protect($"processor-connection:{id.Trim()}", secret));
        AddParameter(command, "@isEnabled", request.IsEnabled ? 1 : 0);
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return await GetProcessorConnectionAsync(connection, id, cancellationToken);
    }

    public async Task<bool> DeleteProcessorConnectionAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM processor_connections WHERE id = @id;";
        AddParameter(command, "@id", id.Trim());
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<ProcessorConnectionItem?> RecordProcessorConnectionHealthAsync(
        string id,
        string status,
        string? message,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE processor_connections
            SET health_status = @status,
                last_health_message = @message,
                last_health_test_utc = @testedUtc,
                updated_utc = @updatedUtc
            WHERE id = @id;
            """;
        var now = timeProvider.GetUtcNow();
        AddParameter(command, "@id", id.Trim());
        AddParameter(command, "@status", NormalizeProcessorConnectionHealth(status));
        AddParameter(command, "@message", NormalizeName(message));
        AddParameter(command, "@testedUtc", now.ToString("O"));
        AddParameter(command, "@updatedUtc", now.ToString("O"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            return null;
        }

        return await GetProcessorConnectionAsync(connection, id, cancellationToken);
    }

    public async Task<ProcessorHandoffItem> EnsureProcessorHandoffAsync(
        CreateProcessorHandoffRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var sourcePath = request.SourcePath.Trim();
        var sourceKey = BuildProcessorSourceKey(sourcePath);

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                INSERT INTO processor_handoffs (
                    id, library_id, media_type, client_id, queue_item_id, release_name, source_path, source_key,
                    processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc
                ) VALUES (
                    @id, @libraryId, @mediaType, @clientId, @queueItemId, @releaseName, @sourcePath, @sourceKey,
                    @processorName, 'waiting', NULL, NULL, NULL, @createdUtc, @updatedUtc
                )
                ON CONFLICT(library_id, source_key) DO NOTHING;
                """;
            AddParameter(insert, "@id", Guid.CreateVersion7().ToString("N"));
            AddParameter(insert, "@libraryId", request.LibraryId.Trim());
            AddParameter(insert, "@mediaType", request.MediaType.Trim().ToLowerInvariant());
            AddParameter(insert, "@clientId", request.ClientId.Trim());
            AddParameter(insert, "@queueItemId", request.QueueItemId.Trim());
            AddParameter(insert, "@releaseName", request.ReleaseName.Trim());
            AddParameter(insert, "@sourcePath", sourcePath);
            AddParameter(insert, "@sourceKey", sourceKey);
            AddParameter(insert, "@processorName", NormalizeName(request.ProcessorName));
            AddParameter(insert, "@createdUtc", now.ToString("O"));
            AddParameter(insert, "@updatedUtc", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        return (await FindProcessorHandoffAsync(request.LibraryId, null, sourcePath, cancellationToken))
            ?? throw new InvalidOperationException("Processor hand-off could not be created or loaded.");
    }

    public async Task<ProcessorHandoffItem?> FindProcessorHandoffAsync(
        string libraryId,
        string? handoffId,
        string? sourcePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(libraryId) || (string.IsNullOrWhiteSpace(handoffId) && string.IsNullOrWhiteSpace(sourcePath)))
        {
            return null;
        }

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = !string.IsNullOrWhiteSpace(handoffId)
            ? "SELECT id, library_id, media_type, client_id, queue_item_id, release_name, source_path, processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc FROM processor_handoffs WHERE id = @id AND library_id = @libraryId LIMIT 1;"
            : "SELECT id, library_id, media_type, client_id, queue_item_id, release_name, source_path, processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc FROM processor_handoffs WHERE library_id = @libraryId AND source_key = @sourceKey LIMIT 1;";
        AddParameter(command, "@libraryId", libraryId.Trim());
        if (!string.IsNullOrWhiteSpace(handoffId)) AddParameter(command, "@id", handoffId.Trim());
        else AddParameter(command, "@sourceKey", BuildProcessorSourceKey(sourcePath!));
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProcessorHandoff(reader) : null;
    }

    public async Task<ProcessorHandoffItem?> GetProcessorHandoffAsync(
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, library_id, media_type, client_id, queue_item_id, release_name, source_path, processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc FROM processor_handoffs WHERE id = @id LIMIT 1;";
        AddParameter(command, "@id", id.Trim());
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProcessorHandoff(reader) : null;
    }

    public async Task<ProcessorHandoffItem?> UpdateProcessorHandoffAsync(
        string id,
        string status,
        string? outputPath,
        string? importJobId,
        string? failureMessage,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using (var update = connection.CreateCommand())
        {
            update.CommandText =
                """
                UPDATE processor_handoffs
                SET status = @status,
                    output_path = COALESCE(@outputPath, output_path),
                    import_job_id = COALESCE(@importJobId, import_job_id),
                    failure_message = @failureMessage,
                    updated_utc = @updatedUtc
                WHERE id = @id;
                """;
            AddParameter(update, "@id", id.Trim());
            AddParameter(update, "@status", NormalizeProcessorHandoffStatus(status));
            AddParameter(update, "@outputPath", NormalizePath(outputPath));
            AddParameter(update, "@importJobId", NormalizeName(importJobId));
            AddParameter(update, "@failureMessage", NormalizeName(failureMessage));
            AddParameter(update, "@updatedUtc", timeProvider.GetUtcNow().ToString("O"));
            if (await update.ExecuteNonQueryAsync(cancellationToken) == 0) return null;
        }

        await using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, library_id, media_type, client_id, queue_item_id, release_name, source_path, processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc FROM processor_handoffs WHERE id = @id LIMIT 1;";
        AddParameter(select, "@id", id.Trim());
        using var reader = await select.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProcessorHandoff(reader) : null;
    }

    public async Task<IReadOnlyList<ProcessorHandoffItem>> ListProcessorHandoffsAsync(
        string? libraryId,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);
        using var command = connection.CreateCommand();
        command.CommandText = string.IsNullOrWhiteSpace(libraryId)
            ? "SELECT id, library_id, media_type, client_id, queue_item_id, release_name, source_path, processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc FROM processor_handoffs ORDER BY updated_utc DESC LIMIT @take;"
            : "SELECT id, library_id, media_type, client_id, queue_item_id, release_name, source_path, processor_name, status, output_path, import_job_id, failure_message, created_utc, updated_utc FROM processor_handoffs WHERE library_id = @libraryId ORDER BY updated_utc DESC LIMIT @take;";
        AddParameter(command, "@take", Math.Clamp(take, 1, 200));
        if (!string.IsNullOrWhiteSpace(libraryId)) AddParameter(command, "@libraryId", libraryId.Trim());
        var items = new List<ProcessorHandoffItem>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) items.Add(ReadProcessorHandoff(reader));
        return items;
    }

    public async Task<MigrationAuditReport> RecordMigrationAuditReportAsync(
        MigrationAuditReport report,
        CancellationToken cancellationToken)
    {
        var persisted = report with
        {
            Id = Guid.CreateVersion7().ToString("N"),
            AppliedUtc = timeProvider.GetUtcNow()
        };
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO migration_audit_reports (
                id, source_kind, source_name, applied_utc, preflight_report_json, result_report_json, applied_items_json
            ) VALUES (
                @id, @sourceKind, @sourceName, @appliedUtc, @preflightReportJson, @resultReportJson, @appliedItemsJson
            );
            """;
        AddParameter(command, "@id", persisted.Id);
        AddParameter(command, "@sourceKind", persisted.SourceKind);
        AddParameter(command, "@sourceName", persisted.SourceName);
        AddParameter(command, "@appliedUtc", persisted.AppliedUtc.ToString("O"));
        AddParameter(command, "@preflightReportJson", JsonSerializer.Serialize(persisted.PreflightReport));
        AddParameter(command, "@resultReportJson", JsonSerializer.Serialize(persisted.ResultReport));
        AddParameter(command, "@appliedItemsJson", JsonSerializer.Serialize(persisted.Applied));
        await command.ExecuteNonQueryAsync(cancellationToken);
        return persisted;
    }

    public async Task<IReadOnlyList<MigrationAuditReport>> ListMigrationAuditReportsAsync(
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, source_kind, source_name, applied_utc, preflight_report_json, result_report_json, applied_items_json
            FROM migration_audit_reports
            ORDER BY applied_utc DESC
            LIMIT @take;
            """;
        AddParameter(command, "@take", Math.Clamp(take, 1, 100));
        var reports = new List<MigrationAuditReport>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            reports.Add(ReadMigrationAuditReport(reader));
        }

        return reports;
    }

    public async Task<MigrationAuditReport?> GetMigrationAuditReportAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await databaseConnectionFactory.OpenConnectionAsync(
            DelunoDatabaseNames.Platform,
            cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, source_kind, source_name, applied_utc, preflight_report_json, result_report_json, applied_items_json
            FROM migration_audit_reports
            WHERE id = @id
            LIMIT 1;
            """;
        AddParameter(command, "@id", id);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMigrationAuditReport(reader) : null;
    }

    private static MigrationAuditReport ReadMigrationAuditReport(System.Data.Common.DbDataReader reader)
    {
        var preflight = JsonSerializer.Deserialize<MigrationReport>(reader.GetString(4))
            ?? throw new InvalidOperationException("Stored migration preflight report could not be read.");
        var result = JsonSerializer.Deserialize<MigrationReport>(reader.GetString(5))
            ?? throw new InvalidOperationException("Stored migration result report could not be read.");
        var applied = JsonSerializer.Deserialize<IReadOnlyList<MigrationAppliedItem>>(reader.GetString(6)) ?? [];
        return new MigrationAuditReport(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            ParseTimestamp(reader.GetString(3)),
            preflight,
            result,
            applied);
    }

    private static ProcessorHandoffItem ReadProcessorHandoff(System.Data.Common.DbDataReader reader)
        => new(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
            reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11), ParseTimestamp(reader.GetString(12)), ParseTimestamp(reader.GetString(13)));

    private ProcessorConnectionItem ReadProcessorConnection(System.Data.Common.DbDataReader reader)
    {
        var id = reader.GetString(0);
        return new ProcessorConnectionItem(
            id,
            reader.GetString(1),
            NormalizeProcessorConnectionProvider(reader.GetString(2)),
            reader.GetString(3),
            NormalizeProcessorAuthHeaderName(reader.GetString(4)),
            reader.IsDBNull(5) ? null : secretProtector.Unprotect($"processor-connection:{id}", reader.GetString(5)),
            reader.GetInt64(6) == 1,
            NormalizeProcessorConnectionHealth(reader.GetString(7)),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : ParseTimestamp(reader.GetString(9)),
            ParseTimestamp(reader.GetString(10)),
            ParseTimestamp(reader.GetString(11)));
    }

    private async Task<ProcessorConnectionItem?> GetProcessorConnectionAsync(
        System.Data.Common.DbConnection connection,
        string id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, name, provider, submission_url, auth_header_name, secret_value, is_enabled, health_status, last_health_message, last_health_test_utc, created_utc, updated_utc FROM processor_connections WHERE id = @id LIMIT 1;";
        AddParameter(command, "@id", id.Trim());
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProcessorConnection(reader) : null;
    }

    private static string BuildProcessorSourceKey(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant())));

    private static string NormalizeProcessorHandoffStatus(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "submitted" or "accepted" or "started" or "waiting" or "completed" or "failed" or "timed-out" => status.Trim().ToLowerInvariant(),
            _ => "waiting"
        };

    private static string NormalizeProcessorConnectionProvider(string? value)
        => value?.Trim().ToLowerInvariant() switch
        {
            "fileflows" or "fileflows-webhook" => "fileflows-webhook",
            _ => "generic-webhook"
        };

    private static string NormalizeProcessorAuthHeaderName(string? value)
        => string.IsNullOrWhiteSpace(value) ? "Authorization" : value.Trim();

    private static string? NormalizeProcessorConnectionUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        return uri.ToString().TrimEnd('/');
    }

    private static string NormalizeProcessorConnectionHealth(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "healthy" or "degraded" or "unreachable" => status.Trim().ToLowerInvariant(),
            _ => "unknown"
        };

}

