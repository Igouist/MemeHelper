using MemeHelper.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MemeHelper.Tests.Configuration;

public class OptionsValidationTests
{
    [Fact]
    public void AddMemeHelperOptions_缺少BotToken_解析時應拋出OptionsValidationException()
    {
        using var provider = Build(("Telegram:AllowedUserIds:0", "1"));

        var act = () => provider.GetRequiredService<IOptions<TelegramOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*BotToken*");
    }

    [Fact]
    public void AddMemeHelperOptions_缺少AllowedUserIds_解析時應拋出OptionsValidationException()
    {
        using var provider = Build(("Telegram:BotToken", "123:abc"));

        var act = () => provider.GetRequiredService<IOptions<TelegramOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().WithMessage("*AllowedUserIds*");
    }

    [Fact]
    public void AddMemeHelperOptions_未設定Search區段_MaxListResults應為20()
    {
        using var provider = Build();

        var options = provider.GetRequiredService<IOptions<SearchOptions>>().Value;

        options.MaxListResults.Should().Be(20);
    }

    private static ServiceProvider Build(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.Select(s => new KeyValuePair<string, string?>(s.Key, s.Value)))
            .Build();

        var services = new ServiceCollection();
        services.AddMemeHelperOptions(configuration);
        return services.BuildServiceProvider();
    }
}
