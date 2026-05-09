using Deluno.Jobs.Contracts;
using Deluno.Jobs.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Deluno.Jobs;

public static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddDelunoJobsModule(this IServiceCollection services)
    {
        services.AddSingleton<SqliteJobStore>();
        services.AddSingleton<IJobScheduler>(provider => provider.GetRequiredService<SqliteJobStore>());
        services.AddSingleton<IJobQueueRepository>(provider => provider.GetRequiredService<SqliteJobStore>());
        services.AddSingleton<IActivityFeedRepository>(provider => provider.GetRequiredService<SqliteJobStore>());
        services.AddSingleton<SqliteDownloadDispatchesRepository>();
        services.AddSingleton<IDownloadDispatchesRepository>(provider =>
            provider.GetRequiredService<SqliteDownloadDispatchesRepository>());
        services.AddSingleton<SqliteImportResolutionsRepository>();
        services.AddSingleton<IImportResolutionsRepository>(provider =>
            provider.GetRequiredService<SqliteImportResolutionsRepository>());
        services.AddSingleton<IDispatchRecoveryHandler>(provider =>
            new CompositeDispatchRecoveryHandler(provider.GetServices<IDispatchRecoveryHandler>().Where(h => h is not CompositeDispatchRecoveryHandler).ToList()));
        services.AddSingleton<DownloadDispatchPollingService>();
        services.AddSingleton<IDownloadDispatchPollingService>(provider =>
        {
            var inner = provider.GetRequiredService<DownloadDispatchPollingService>();
            var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CircuitBreakerDownloadDispatchPollingService>>();
            return new CircuitBreakerDownloadDispatchPollingService(inner, logger);
        });
        services.AddHostedService<JobsSchemaInitializer>();
        services.AddHostedService<DownloadDispatchPollingHostedService>();
        return services;
    }
}
