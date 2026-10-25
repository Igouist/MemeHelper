namespace MemeHelper.Api.Configuration;

internal static class OptionsRegistration
{
    public static IServiceCollection AddMemeHelperOptions(this IServiceCollection services, IConfiguration configuration)
    {
        // 只有 Telegram 區段驗證：其他三個都有可用的預設值，缺了也能跑。
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection(TelegramOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<StorageOptions>().Bind(configuration.GetSection(StorageOptions.SectionName));
        services.AddOptions<SearchOptions>().Bind(configuration.GetSection(SearchOptions.SectionName));

        return services;
    }
}
