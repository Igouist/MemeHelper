namespace MemeHelper.Api.Configuration;

internal static class OptionsRegistration
{
    public static IServiceCollection AddMemeHelperOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // Telegram 缺了就不能跑；Backup 的 KeepCount 填錯會把剛做的快照一起刪掉。
        // 其他三個區段都有可用的預設值，不驗證。
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<BackupOptions>()
            .Bind(configuration.GetSection(BackupOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<SearchOptions>().Bind(configuration.GetSection(SearchOptions.SectionName));

        return services;
    }
}
